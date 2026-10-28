using MossAgent.Platform.Windows.Terminal;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyTerminalShutdownTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static PtyTerminalSession Session(PtyTestConnection connection, Action<string> output) =>
        new(connection, new CancellationTokenSource(), output);

    [Fact]
    public async Task RepeatedStopAndManagerDisposalShareConnectionCleanup()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new PtyTestConnection { Disposing = () => { entered.SetResult(); release.Task.GetAwaiter().GetResult(); } };
        await using var manager = new PtyTerminalSessionManager((_, output, _, _) => Task.FromResult(Session(connection, output)));
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        var first = manager.StopAsync(task);
        await entered.Task.WaitAsync(Deadline, Token);
        var second = manager.StopAsync(task);
        var disposal = manager.DisposeAsync().AsTask();
        try
        {
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
            Assert.False(disposal.IsCompleted);
            Assert.Same(disposal, manager.DisposeAsync().AsTask());
            release.SetResult();
            await Task.WhenAll(first, second, disposal).WaitAsync(Deadline, Token);
            Assert.Equal(1, connection.DisposeCount);
        }
        finally { release.TrySetResult(); await Task.WhenAll(first, second, disposal).WaitAsync(Deadline, Token); }
    }

    [Fact]
    public async Task DuplicateCallerCancellationDoesNotStopTheCreatingCaller()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<PtyTerminalSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new PtyTestConnection();
        await using var manager = new PtyTerminalSessionManager((_, _, _, _) => { entered.SetResult(); return pending.Task; });
        var task = Guid.NewGuid();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var first = manager.StartAsync(task, Path.GetTempPath(), Token);
        await entered.Task.WaitAsync(Deadline, Token);
        var duplicate = manager.StartAsync(task, Path.GetTempPath(), cancellation.Token);
        try
        {
            cancellation.Cancel();
            pending.SetResult(Session(connection, _ => { }));
            await first.WaitAsync(Deadline, Token);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => duplicate.WaitAsync(Deadline, Token));
            Assert.Equal(0, connection.DisposeCount);
            await manager.WriteAsync(task, "retained", Token);
        }
        finally
        {
            if (!pending.Task.IsCompleted) pending.TrySetResult(Session(connection, _ => { }));
            await first.WaitAsync(Deadline, Token);
        }
    }

    [Fact]
    public async Task ActiveTaskCannotSilentlyReuseDifferentWorkingDirectory()
    {
        var calls = 0;
        var connection = new PtyTestConnection();
        await using var manager = new PtyTerminalSessionManager((_, output, _, _) =>
        { calls++; return Task.FromResult(Session(connection, output)); });
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        await Assert.ThrowsAsync<ArgumentException>(() => manager.StartAsync(task, Path.Combine(Path.GetTempPath(), "different"), Token));
        Assert.Equal(1, calls);
        Assert.Equal(0, connection.DisposeCount);
        await manager.WriteAsync(task, "retained", Token);
    }

    [Fact]
    public async Task FailedSpawnRetiresOwnerAndAllowsExplicitRetry()
    {
        var calls = 0;
        var connection = new PtyTestConnection();
        await using var manager = new PtyTerminalSessionManager((_, output, _, _) => ++calls == 1
            ? Task.FromException<PtyTerminalSession>(new IOException("synthetic-start-failure"))
            : Task.FromResult(Session(connection, output)));
        var task = Guid.NewGuid();
        await Assert.ThrowsAsync<IOException>(() => manager.StartAsync(task, Path.GetTempPath(), Token));
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        Assert.Equal(2, calls);
        await manager.StopAsync(task);
        Assert.Equal(1, connection.DisposeCount);
    }
}
