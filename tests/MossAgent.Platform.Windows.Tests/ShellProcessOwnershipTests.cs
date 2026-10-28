using System.Diagnostics;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Processes;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Processes;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class ShellProcessOwnershipTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CommandExitOrCancellation_LeavesNoBackgroundDescendants(bool intermediate, bool cancel)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Process? descendant = null;
        Process? middle = null;
        Task<ToolResult>? execution = null;
        try
        {
            var child = $"[IO.File]::WriteAllText('{root}\\child.pid', [string]$PID); "
                + $"Start-Sleep -Seconds 30; [IO.File]::WriteAllText('{root}\\late.txt', 'late')";
            await File.WriteAllTextAsync(Path.Combine(root, "child.ps1"), child, TestContext.Current.CancellationToken);
            var command = Launch(Path.Combine(root, "child.ps1"));
            if (intermediate)
            {
                await File.WriteAllTextAsync(Path.Combine(root, "middle.ps1"),
                    command + $"; [IO.File]::WriteAllText('{root}\\middle.pid', [string]$PID); "
                    + $"while (!(Test-Path '{root}\\release-middle')) {{ Start-Sleep -Milliseconds 20 }}",
                    TestContext.Current.CancellationToken);
                command = Launch(Path.Combine(root, "middle.ps1"));
            }
            command += $"; while (!(Test-Path '{root}\\release-parent')) {{ Start-Sleep -Milliseconds 20 }}; "
                + "[Console]::Out.Write('parent-ok')";
            var tool = new ShellRunTool(new AuthorizedPathResolver(), new WindowsTaskProcessLauncher());
            var executor = new ToolExecutor(new ToolRegistry([tool]),
                new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)));
            execution = executor.ExecuteAsync(new ToolRequest("ownership", "shell.run",
                JsonSerializer.SerializeToElement(new { command })),
                new ToolExecutionContext(Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess,
                    Timeout: TimeSpan.FromSeconds(15)), cancellation.Token);
            descendant = await BindAsync(root, "child.pid", execution);
            if (intermediate)
            {
                middle = await BindAsync(root, "middle.pid", execution);
                await File.WriteAllTextAsync(Path.Combine(root, "release-middle"), "go", TestContext.Current.CancellationToken);
                await middle.WaitForExitAsync(TestContext.Current.CancellationToken)
                    .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            Assert.False(descendant.HasExited);
            if (cancel) cancellation.Cancel();
            else await File.WriteAllTextAsync(Path.Combine(root, "release-parent"), "go", TestContext.Current.CancellationToken);
            var result = await execution;

            if (cancel) Assert.Equal("tool_cancelled", result.ErrorCode);
            else
            {
                Assert.True(result.IsSuccess, result.Summary);
                Assert.Equal("parent-ok", result.Content);
            }
            Assert.True(descendant.HasExited, "A descendant survived its shell command.");
            Assert.False(File.Exists(Path.Combine(root, "late.txt")));
        }
        finally
        {
            await StopAsync(descendant);
            await StopAsync(middle);
            await File.WriteAllTextAsync(Path.Combine(root, "release-parent"), "go", CancellationToken.None);
            if (execution is not null)
                await execution.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
            await WindowsProcessTestDirectory.DeleteAsync(root);
        }
    }

    // [Diagnostics.Process]::Start bypasses PowerShell's provider path resolution,
    // which is unavailable inside an AppContainer (Start-Process throws DriveNotFound).
    private static string Launch(string script) =>
        "$null = [Diagnostics.Process]::Start('powershell.exe', '-NoProfile -File \"" + script + "\"')";

    private static async Task<Process> BindAsync(string root, string name, Task<ToolResult> execution)
    {
        var token = TestContext.Current.CancellationToken;
        var path = Path.Combine(root, name);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        string content;
        do
        {
            await Task.Delay(20, timeout.Token);
            content = File.Exists(path) ? await File.ReadAllTextAsync(path, timeout.Token) : string.Empty;
            if (execution.IsCompleted)
            {
                var result = await execution;
                throw new InvalidOperationException(result.Summary + "\n" + result.Content);
            }
        } while (!int.TryParse(content, out _));
        var process = Process.GetProcessById(int.Parse(content));
        _ = process.Handle;
        return process;
    }

    private static async Task StopAsync(Process? process)
    {
        if (process is null) return;
        if (!process.HasExited) process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync(CancellationToken.None);
        process.Dispose();
    }

}
