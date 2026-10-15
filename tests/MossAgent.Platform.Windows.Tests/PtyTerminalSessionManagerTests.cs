using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Terminal;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyTerminalSessionManagerTests
{
    [Fact]
    public async Task TerminalStart_RejectsUnauthorizedWorkingDirectoryBeforeApproval()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var outside = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var manager = new PtyTerminalSessionManager();
            var tool = new TerminalStartTool(manager, new AuthorizedPathResolver());
            var approvalRequests = 0;
            var executor = new ToolExecutor(
                new ToolRegistry([tool]),
                new PolicyApprovalService((_, _, _) =>
                {
                    approvalRequests++;
                    return ValueTask.FromResult(true);
                }));
            var request = new ToolRequest("terminal-outside", tool.Descriptor.Name,
                JsonSerializer.SerializeToElement(new { }));
            var context = new ToolExecutionContext(Guid.NewGuid(), outside, [root], ApprovalPolicy.AskEveryTime);

            var result = await executor.ExecuteAsync(request, context, TestContext.Current.CancellationToken);

            Assert.Equal("path_denied", result.ErrorCode);
            Assert.Equal(0, approvalRequests);
            Assert.False(Directory.Exists(outside));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Terminal_StartsAndReturnsInteractiveOutput()
    {
        await using var manager = new PtyTerminalSessionManager();
        var taskId = Guid.NewGuid();
        var completion = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        manager.OutputReceived += (_, eventArgs) => Capture(eventArgs, taskId, completion);

        var cancellationToken = TestContext.Current.CancellationToken;
        await manager.StartAsync(taskId, Environment.CurrentDirectory, cancellationToken);
        await manager.WriteAsync(
            taskId, "Write-Output '__MOSS_PTY_OK__'\r", cancellationToken);
        var output = await completion.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);

        Assert.Contains("__MOSS_PTY_OK__", output, StringComparison.Ordinal);
        await manager.StopAsync(taskId);
    }

    private static void Capture(
        TerminalOutputEventArgs eventArgs,
        Guid taskId,
        TaskCompletionSource<string> completion)
    {
        if (eventArgs.TaskId == taskId
            && eventArgs.Output.Contains("__MOSS_PTY_OK__", StringComparison.Ordinal))
        {
            completion.TrySetResult(eventArgs.Output);
        }
    }
}
