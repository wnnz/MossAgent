using System.Diagnostics;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Platform.Windows.Processes;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class GitOwnershipFixture : IAsyncDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    public string Repository => Path.Combine(Root, "repository");
    public GitCommandRunner Runner { get; } = new(new WindowsTaskProcessLauncher());
    public Process? Descendant { get; private set; }
    private Process? _hook;
    public Task? Execution { get; private set; }
    public Task<ToolResult>? ToolExecution { get; private set; }
    public int Approvals { get; private set; }

    public async Task InitializeAsync(CancellationToken token)
    {
        Directory.CreateDirectory(Repository);
        var context = new ToolExecutionContext(Guid.NewGuid(), Repository, [Root], ApprovalPolicy.FullAccess);
        foreach (var arguments in new string[][]
        {
            ["init"], ["config", "core.autocrlf", "false"], ["config", "user.name", "Git ownership QA"],
            ["config", "user.email", "qa@example.invalid"]
        })
            Assert.True((await Runner.RunAsync(Repository, arguments, context, token)).IsSuccess);
        await File.WriteAllTextAsync(Path.Combine(Repository, "baseline.txt"), "synthetic", token);
        Assert.True((await Runner.RunAsync(Repository, ["add", "baseline.txt"], context, token)).IsSuccess);
        Assert.True((await Runner.RunAsync(Repository, ["commit", "-m", "synthetic baseline"], context, token)).IsSuccess);
        var escapedRoot = Root.Replace("'", "''", StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(Root, "child.ps1"),
            $"Set-Location -LiteralPath '{escapedRoot}'; "
            + "[Environment]::CurrentDirectory = (Get-Location).Path; "
            + "[IO.File]::WriteAllText('child.pid.tmp', [string]$PID); "
            + "[IO.File]::Move('child.pid.tmp', 'child.pid', $true); Start-Sleep -Seconds 30; "
            + "[IO.File]::WriteAllText('late.txt', 'late')", token);
        await File.WriteAllTextAsync(Path.Combine(Root, "hook.ps1"),
            $"Set-Location -LiteralPath '{escapedRoot}'; "
            + "[Environment]::CurrentDirectory = (Get-Location).Path; "
            + "[IO.File]::WriteAllText('hook.pid.tmp', [string]$PID); "
            + "[IO.File]::Move('hook.pid.tmp', 'hook.pid', $true); "
            + "Start-Process pwsh -WindowStyle Hidden -WorkingDirectory (Get-Location).Path "
            + "-ArgumentList '-NoLogo','-NoProfile','-NonInteractive','-File','child.ps1'; "
            + "while (!(Test-Path 'release-hook')) { Start-Sleep -Milliseconds 20 }; "
            + "[Console]::Out.Write('hook-ok')", token);
        var hookPath = Path.Combine(Root, "hook.ps1").Replace('\\', '/');
        await File.WriteAllTextAsync(Path.Combine(Repository, ".git", "hooks", "post-checkout"),
            $"#!/bin/sh\nexec pwsh -NoLogo -NoProfile -NonInteractive -File \"{hookPath}\"\n", token);
    }

    public Task StartAsync(bool infrastructure, CancellationToken token, ApprovalPolicy policy = ApprovalPolicy.AskEveryTime,
        TimeProvider? clock = null)
    {
        if (infrastructure)
        {
            var project = new ProjectProfile(Guid.NewGuid(), "synthetic", Repository, [Root], DateTimeOffset.UnixEpoch);
            Execution = new GitWorktreeService(new AppDataPaths(Path.Combine(Root, "data")),
                new GitProcessRunner(new WindowsTaskProcessLauncher(), clock))
                .CreateAsync(project, Guid.NewGuid(), token);
        }
        else
        {
            var tool = new GitCreateWorktreeTool(new AuthorizedPathResolver(), Runner);
            var executor = new ToolExecutor(new ToolRegistry([tool]), new PolicyApprovalService((_, _, _) =>
            {
                Approvals++;
                return ValueTask.FromResult(true);
            }), timeProvider: clock);
            ToolExecution = executor.ExecuteAsync(new ToolRequest("git-ownership", tool.Descriptor.Name,
                JsonSerializer.SerializeToElement(new { path = Path.Combine(Root, "tool-worktree"), branch = "HEAD" })),
                new ToolExecutionContext(Guid.NewGuid(), Repository, [Root], policy, Timeout: TimeSpan.FromSeconds(15)), token);
            Execution = ToolExecution;
        }
        return Execution;
    }

    public async Task BindDescendantAsync(CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var path = Path.Combine(Root, "child.pid");
        string content;
        do
        {
            await Task.Delay(20, deadline.Token);
            content = File.Exists(path) ? await File.ReadAllTextAsync(path, deadline.Token) : string.Empty;
            if (Execution!.IsCompleted)
            {
                await Execution;
                throw new InvalidOperationException("Git completed before the hook published its descendant.");
            }
        } while (!int.TryParse(content, out _));
        Descendant = Process.GetProcessById(int.Parse(content));
        _ = Descendant.Handle;
        _hook = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(Path.Combine(Root, "hook.pid"), token)));
        _ = _hook.Handle;
    }

    public Task ReleaseAsync(CancellationToken token) => File.WriteAllTextAsync(Path.Combine(Root, "release-hook"), "go", token);

    public async ValueTask DisposeAsync()
    {
        if (Descendant is not null)
        {
            if (!Descendant.HasExited) Descendant.Kill(entireProcessTree: true);
            await Descendant.WaitForExitAsync(CancellationToken.None);
            Descendant.Dispose();
        }
        await ReleaseAsync(CancellationToken.None);
        if (_hook is not null)
        {
            await _hook.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(20));
            _hook.Dispose();
        }
        if (Execution is not null)
        {
            try { await Execution.WaitAsync(TimeSpan.FromSeconds(20)); }
            catch (OperationCanceledException) { }
            catch (TimeoutException) when (Execution.IsFaulted) { }
        }
        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        await WindowsProcessTestDirectory.DeleteAsync(Root);
    }
}
