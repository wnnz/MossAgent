using System.Diagnostics;
using System.Text.Json;
using MossAgent.Application.Git;
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
    public Guid WorktreeTaskId { get; } = Guid.NewGuid();
    public string ManagedWorktreePath => Path.Combine(Root, "data", "worktrees", WorktreeTaskId.ToString("N"));
    public GitCommandRunner Runner { get; } = new(new WindowsTaskProcessLauncher());
    // Repository setup deliberately uses the infrastructure runner: GitProcessRunner has
    // no task context and is intentionally not sandboxed, so arranging the repo does not
    // depend on whether git's install directory is package-readable on this machine.
    private GitProcessRunner SetupGit { get; } = new(new WindowsTaskProcessLauncher());
    public Process? Descendant { get; private set; }
    private Process? _hook;
    public Task? Execution { get; private set; }
    public Task<ToolResult>? ToolExecution { get; private set; }
    public int Approvals { get; private set; }

    public async Task InitializeAsync(CancellationToken token)
    {
        Directory.CreateDirectory(Repository);
        foreach (var arguments in new string[][]
        {
            ["init"], ["config", "core.autocrlf", "false"], ["config", "user.name", "Git ownership QA"],
            ["config", "user.email", "qa@example.invalid"]
        })
            Assert.Equal(0, (await SetupGit.RunAsync(Repository, arguments, token)).ExitCode);
        await File.WriteAllTextAsync(Path.Combine(Repository, "baseline.txt"), "synthetic", token);
        foreach (var arguments in new string[][] { ["add", "baseline.txt"], ["commit", "-m", "synthetic baseline"] })
            Assert.Equal(0, (await SetupGit.RunAsync(Repository, arguments, token)).ExitCode);
        var escapedRoot = Root.Replace("'", "''", StringComparison.Ordinal).Replace('\\', '/');
        // Hook scripts stay container-safe: powershell.exe (AppContainer-capable inbox host),
        // absolute paths and [IO.File]/Process.Start — provider cmdlets like Set-Location,
        // Test-Path and Start-Process throw DriveNotFound inside an unregistered container.
        await File.WriteAllTextAsync(Path.Combine(Root, "child.ps1"),
            $"[Environment]::CurrentDirectory = '{escapedRoot}'; "
            + $"[IO.File]::WriteAllText('{escapedRoot}/child.pid.tmp', [string]$PID); "
            + $"if ([IO.File]::Exists('{escapedRoot}/child.pid')) {{ [IO.File]::Delete('{escapedRoot}/child.pid') }}; "
            + $"[IO.File]::Move('{escapedRoot}/child.pid.tmp', '{escapedRoot}/child.pid'); Start-Sleep -Seconds 30; "
            + $"[IO.File]::WriteAllText('{escapedRoot}/late.txt', 'late')", token);
        await File.WriteAllTextAsync(Path.Combine(Root, "hook.ps1"),
            $"[Environment]::CurrentDirectory = '{escapedRoot}'; "
            + $"[IO.File]::WriteAllText('{escapedRoot}/hook.pid.tmp', [string]$PID); "
            + $"if ([IO.File]::Exists('{escapedRoot}/hook.pid')) {{ [IO.File]::Delete('{escapedRoot}/hook.pid') }}; "
            + $"[IO.File]::Move('{escapedRoot}/hook.pid.tmp', '{escapedRoot}/hook.pid'); "
            + $"$null = [Diagnostics.Process]::Start('powershell.exe', '-NoProfile -File \"{escapedRoot}/child.ps1\"'); "
            + $"while (![IO.File]::Exists('{escapedRoot}/release-hook')) {{ Start-Sleep -Milliseconds 20 }}; "
            + "[Console]::Out.Write('hook-ok')", token);
        var hookPath = Path.Combine(Root, "hook.ps1").Replace('\\', '/');
        await File.WriteAllTextAsync(Path.Combine(Repository, ".git", "hooks", "post-checkout"),
            $"#!/bin/sh\nexec powershell -NoLogo -NoProfile -NonInteractive -File \"{hookPath}\"\n", token);
    }

    public Task StartAsync(bool infrastructure, CancellationToken token, ApprovalPolicy policy = ApprovalPolicy.AskEveryTime,
        TimeProvider? clock = null)
    {
        if (infrastructure)
        {
            var project = new ProjectProfile(Guid.NewGuid(), "synthetic", Repository, [Root], DateTimeOffset.UnixEpoch);
            Execution = new GitWorktreeService(new AppDataPaths(Path.Combine(Root, "data")),
                new GitProcessRunner(new WindowsTaskProcessLauncher(), clock), clock)
                .CreateAsync(project, WorktreeTaskId, token);
        }
        else
        {
            MossAgent.Testing.SandboxedGitSupport.RequireAsync().GetAwaiter().GetResult();
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
            catch (GitWorktreeCreationException failure) when (Execution.IsFaulted
                && failure.InnerException is OperationCanceledException or TimeoutException) { }
        }
        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        await WindowsProcessTestDirectory.DeleteAsync(Root);
    }
}
