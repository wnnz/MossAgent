using MossAgent.Platform.Windows.Terminal;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyTerminalCleanupBoundaryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static TimeSpan Deadline => TimeSpan.FromSeconds(10);

    [Fact]
    public async Task NonCooperativeWriteRetainsShutdownOwnershipUntilItReturns()
    {
        using var writer = new PtyPendingWriteStream();
        var connection = new PtyTestConnection { WriterStream = writer };
        await using var manager = new PtyTerminalSessionManager((_, output, _, _) =>
            Task.FromResult(new PtyTerminalSession(connection, new CancellationTokenSource(), output)));
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        var writing = manager.WriteAsync(task, "pending", Token);
        Task? stopping = null;
        try
        {
            await writer.Entered.Task.WaitAsync(Deadline, Token);
            stopping = manager.StopAsync(task);
            await writer.Cancelled.Task.WaitAsync(Deadline, Token);
            Assert.False(stopping.IsCompleted);
            Assert.False(writing.IsCompleted);
            writer.Release.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writing.WaitAsync(Deadline, Token));
            await stopping.WaitAsync(Deadline, Token);
            Assert.Equal(1, connection.DisposeCount);
        }
        finally
        {
            writer.Release.TrySetResult();
            await writing.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
            if (stopping is not null) await stopping.WaitAsync(Deadline, Token);
        }
    }

    [Fact]
    public async Task FailedCloseIsNotSuccessAndDoesNotSkipOtherTasksCleanup()
    {
        var failedTask = Guid.NewGuid();
        var failed = new PtyTestConnection { Disposing = () => throw new IOException("synthetic-close-failure") };
        var healthy = new PtyTestConnection();
        var calls = 0;
        var manager = new PtyTerminalSessionManager((_, output, _, _) =>
            Task.FromResult(new PtyTerminalSession(++calls == 1 ? failed : healthy, new CancellationTokenSource(), output)));
        try
        {
            await manager.StartAsync(failedTask, Path.GetTempPath(), Token);
            await manager.StartAsync(Guid.NewGuid(), Path.GetTempPath(), Token);
            var disposal = manager.DisposeAsync().AsTask();
            await Assert.ThrowsAsync<IOException>(() => disposal.WaitAsync(Deadline, Token));
            Assert.Same(disposal, manager.DisposeAsync().AsTask());
            Assert.Equal(1, failed.DisposeCount);
            Assert.Equal(1, healthy.DisposeCount);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => manager.StartAsync(Guid.NewGuid(), Path.GetTempPath(), Token));
            Assert.Equal(2, calls);
        }
        finally
        {
            await manager.DisposeAsync().AsTask().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
            failed.ReaderStream.Dispose();
            failed.WriterStream.Dispose();
        }
    }

    [Fact]
    public async Task OldOutputCannotEnterExplicitlyRestartedTasksNewSession()
    {
        var publishers = new List<Action<string>>();
        var received = new List<string>();
        await using var manager = new PtyTerminalSessionManager((_, output, _, _) =>
        {
            publishers.Add(output);
            return Task.FromResult(new PtyTerminalSession(new PtyTestConnection(), new CancellationTokenSource(), output));
        });
        manager.OutputReceived += (_, output) => received.Add(output.Output);
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        publishers[0]("first");
        await manager.StopAsync(task);
        publishers[0]("stopped");
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        publishers[0]("stale");
        publishers[1]("second");
        Assert.Equal(["first", "second"], received);
    }
}
