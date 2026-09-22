using MossAgent.Platform.Windows.Terminal;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyTerminalSessionManagerTests
{
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
