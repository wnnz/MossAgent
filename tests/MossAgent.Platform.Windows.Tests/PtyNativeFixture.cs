using System.Diagnostics;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Terminal;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class PtyNativeFixture : IAsyncDisposable
{
    private readonly List<Process> _processes = [];
    private readonly ToolExecutor _executor;
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    public static TimeSpan Deadline => TimeSpan.FromSeconds(20);
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", "pty-" + Guid.NewGuid().ToString("N"));
    public PtyTerminalSessionManager Manager { get; } = new();

    public PtyNativeFixture()
    {
        Directory.CreateDirectory(Root);
        _executor = new ToolExecutor(new ToolRegistry([
            new TerminalStartTool(Manager, new AuthorizedPathResolver()),
            new TerminalWriteTool(Manager, new AuthorizedPathResolver()), new TerminalStopTool(Manager)]),
            new PolicyApprovalService((_, _, _) => throw new InvalidOperationException("FullAccess unexpectedly requested approval.")));
    }

    public async Task StartAsync(Guid task) => await ExecuteAsync(task, "terminal.start", new { });
    public async Task StopAsync(Guid task) => await ExecuteAsync(task, "terminal.stop", new { });
    public async Task WriteAsync(Guid task, string command) => await ExecuteAsync(task, "terminal.write", new { input = command + "\r" });

    private async Task ExecuteAsync(Guid task, string tool, object arguments)
    {
        var result = await _executor.ExecuteAsync(new ToolRequest(Guid.NewGuid().ToString("N"), tool,
            JsonSerializer.SerializeToElement(arguments)),
            new ToolExecutionContext(task, Root, [Root], ApprovalPolicy.FullAccess, Timeout: Deadline), Token)
            .WaitAsync(Deadline, Token);
        Assert.True(result.IsSuccess, $"{tool}: {result.ErrorCode}: {result.Summary}");
    }

    public static string PublishPid(string name) =>
        $"[IO.File]::WriteAllText('{name}.tmp', [string]$PID); [IO.File]::Move('{name}.tmp', '{name}', $true); [IO.File]::WriteAllText('{name}.ready', '')";

    public async Task CreateChildScriptAsync()
    {
        var script = PublishPid("child.pid") + "; Start-Sleep -Seconds 90; [IO.File]::WriteAllText('late.txt', 'late')";
        await File.WriteAllTextAsync(Path.Combine(Root, "child.ps1"), script, Token);
    }

    public static string LaunchChild =>
        "Start-Process pwsh -WindowStyle Hidden -WorkingDirectory (Get-Location).Path -ArgumentList '-NoProfile','-File','child.ps1'";

    public async Task<Process> BindAsync(string name)
    {
        await WaitForFileAsync(name + ".ready");
        var pid = int.Parse(await File.ReadAllTextAsync(Path.Combine(Root, name), Token));
        var process = Process.GetProcessById(pid);
        _ = process.Handle;
        _processes.Add(process);
        return process;
    }

    public async Task WaitForFileAsync(string name)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancellation.CancelAfter(Deadline);
        while (!File.Exists(Path.Combine(Root, name))) await Task.Delay(25, cancellation.Token);
    }

    public async Task<string> ReadPublishedFileAsync(string name)
    {
        await WaitForFileAsync(name + ".ready");
        return await File.ReadAllTextAsync(Path.Combine(Root, name), Token);
    }

    public static async Task AssertExitedAsync(Process process)
    {
        await process.WaitForExitAsync(Token).WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.True(process.HasExited);
    }

    public async ValueTask DisposeAsync()
    {
        var closing = Manager.DisposeAsync().AsTask();
        try { await closing.WaitAsync(Deadline, CancellationToken.None); }
        finally
        {
            foreach (var process in _processes)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
                }
                finally { process.Dispose(); }
            }
            await closing.WaitAsync(Deadline, CancellationToken.None);
            await WindowsProcessTestDirectory.DeleteAsync(Root);
        }
    }
}
