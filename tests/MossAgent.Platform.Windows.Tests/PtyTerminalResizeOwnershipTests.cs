using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Platform.Windows.Terminal;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyTerminalResizeOwnershipTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static TimeSpan Deadline => TimeSpan.FromSeconds(10);

    [Fact]
    public async Task CancelledRequestWaitingForNativeResizeNeverMutatesChild()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var connection = new PtyTestConnection { Resizing = (_, _) => { Interlocked.Increment(ref calls); entered.TrySetResult(); release.Wait(Token); } };
        await using var manager = new PtyTerminalSessionManager((_, output, _) => Task.FromResult(new PtyTerminalSession(connection, new(), output)));
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        var session = manager.GetSnapshot(task)!.SessionId;
        var first = manager.ResizeAsync(task, session, Path.GetTempPath(), new(40, 10), Token);
        try
        {
            await entered.Task.WaitAsync(Deadline, Token);
            using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token);
            var second = manager.ResizeAsync(task, session, Path.GetTempPath(), new(50, 15), cancelled.Token);
            cancelled.Cancel();
            release.Set();
            await first.WaitAsync(Deadline, Token);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second.WaitAsync(Deadline, Token));
            Assert.Equal(1, calls);
            Assert.Equal(new TerminalGridSize(40, 10), manager.GetSnapshot(task)!.Grid);
        }
        finally { release.Set(); await first.WaitAsync(Deadline, Token); }
    }

    [Fact]
    public async Task StopRetainsNativeResizeUntilReturnAndBlocksReplacement()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new PtyTestConnection { Resizing = (_, _) => { entered.TrySetResult(); release.Wait(Token); } };
        await using var manager = new PtyTerminalSessionManager((_, output, _) => Task.FromResult(new PtyTerminalSession(connection, new(), output)));
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        var resize = manager.ResizeAsync(task, manager.GetSnapshot(task)!.SessionId, Path.GetTempPath(), new(40, 10), Token);
        try
        {
            await entered.Task.WaitAsync(Deadline, Token);
            var stop = manager.StopAsync(task);
            Assert.False(stop.IsCompleted);
            Assert.Equal(0, connection.DisposeCount);
            Assert.Equal(TerminalSessionState.Stopping, manager.GetSnapshot(task)!.State);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.StartAsync(task, Path.GetTempPath(), Token));
            release.Set();
            await resize.WaitAsync(Deadline, Token);
            await stop.WaitAsync(Deadline, Token);
            Assert.Equal(1, connection.DisposeCount);
            Assert.Equal(TerminalSessionState.Stopped, manager.GetSnapshot(task)!.State);
        }
        finally { release.Set(); await resize.WaitAsync(Deadline, Token); }
    }

    [Fact]
    public async Task DeadlineRetainsStartedResizeAndReportsTimeoutWithoutHidingAppliedSize()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new PtyTestConnection { Resizing = (_, _) => { entered.TrySetResult(); release.Wait(Token); } };
        var clock = new ManualToolTimeProvider();
        await using var manager = new PtyTerminalSessionManager((_, output, _) => Task.FromResult(new PtyTerminalSession(connection, new(), output)));
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.FullAccess, Timeout: TimeSpan.FromMilliseconds(100));
        await manager.StartAsync(context.TaskId, context.WorkingDirectory, Token);
        var executor = new ToolExecutor(new ToolRegistry([new TerminalResizeTool(manager, new AuthorizedPathResolver())]),
            new PolicyApprovalService((_, _, _) => ValueTask.FromResult(false)), timeProvider: clock);
        var operation = executor.ExecuteAsync(new("deadline", "terminal.resize", JsonSerializer.SerializeToElement(new
        { columns = 40, rows = 10, sessionId = manager.GetSnapshot(context.TaskId)!.SessionId.ToString("D") })), context, Token);
        try
        {
            await entered.Task.WaitAsync(Deadline, Token);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            Assert.False(operation.IsCompleted);
            release.Set();
            Assert.Equal("tool_timeout", (await operation.WaitAsync(Deadline, Token)).ErrorCode);
            Assert.Equal(new TerminalGridSize(40, 10), manager.GetSnapshot(context.TaskId)!.Grid);
        }
        finally { release.Set(); await operation.WaitAsync(Deadline, Token); }
    }
}
