using MossAgent.Platform.Windows.Terminal;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyTerminalOwnershipTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Root => Path.GetTempPath();
    private static TaskCompletionSource<PtyTerminalSession> Pending() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static PtyTerminalSession Session(PtyTestConnection connection, Action<string>? output = null) =>
        new(connection, new CancellationTokenSource(), output ?? (_ => { }));
    private static async Task DrainAsync(Task operation)
    {
        try { await operation.WaitAsync(Deadline, Token); }
        catch (OperationCanceledException) { }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopAndDisposalRetainPendingSpawnAndRejectLateHandoff(bool disposal)
    {
        var pending = Pending();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new PtyTestConnection();
        await using var manager = new PtyTerminalSessionManager((_, _, _) => { entered.SetResult(); return pending.Task; });
        var task = Guid.NewGuid();
        var starting = manager.StartAsync(task, Root, Token);
        await entered.Task.WaitAsync(Deadline, Token);
        var stopping = disposal ? manager.DisposeAsync().AsTask() : manager.StopAsync(task);
        try
        {
            Assert.False(stopping.IsCompleted);
            pending.SetResult(Session(connection));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting.WaitAsync(Deadline, Token));
            await stopping.WaitAsync(Deadline, Token);
            Assert.Equal(1, connection.DisposeCount);
        }
        finally
        {
            if (!pending.Task.IsCompleted) pending.TrySetResult(Session(connection));
            await DrainAsync(starting);
            await DrainAsync(stopping);
            await manager.StopAsync(task).WaitAsync(Deadline, Token);
        }
    }

    [Fact]
    public async Task DisposedManagerRejectsCreationBeforeCallingFactory()
    {
        var calls = 0;
        var connection = new PtyTestConnection();
        await using var manager = new PtyTerminalSessionManager((_, _, _) => { calls++; return Task.FromResult(Session(connection)); });
        await manager.DisposeAsync();
        try
        {
            await Assert.ThrowsAsync<ObjectDisposedException>(() => manager.StartAsync(Guid.NewGuid(), Root, Token));
            Assert.Equal(0, calls);
        }
        finally { await manager.DisposeAsync(); }
    }

    [Fact]
    public async Task ConcurrentStartsForOneTaskShareCreation()
    {
        var pending = Pending();
        var calls = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new PtyTestConnection();
        await using var manager = new PtyTerminalSessionManager((_, _, _) => { Interlocked.Increment(ref calls); entered.TrySetResult(); return pending.Task; });
        var task = Guid.NewGuid();
        var first = manager.StartAsync(task, Root, Token);
        await entered.Task.WaitAsync(Deadline, Token);
        var second = manager.StartAsync(task, Root, Token);
        try
        {
            Assert.Equal(1, calls);
            pending.SetResult(Session(connection));
            await Task.WhenAll(first, second).WaitAsync(Deadline, Token);
            await manager.StopAsync(task);
            Assert.Equal(1, connection.DisposeCount);
        }
        finally { if (!pending.Task.IsCompleted) pending.TrySetResult(Session(connection)); await DrainAsync(Task.WhenAll(first, second)); }
    }

    [Fact]
    public async Task CancellationRetainsSpawnUntilLateConnectionIsDisposed()
    {
        var pending = Pending();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new PtyTestConnection();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        await using var manager = new PtyTerminalSessionManager((_, _, _) => { entered.SetResult(); return pending.Task; });
        var task = Guid.NewGuid();
        var starting = manager.StartAsync(task, Root, cancellation.Token);
        await entered.Task.WaitAsync(Deadline, Token);
        try
        {
            cancellation.Cancel();
            Assert.False(starting.IsCompleted);
            pending.SetResult(Session(connection));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting.WaitAsync(Deadline, Token));
            Assert.Equal(1, connection.DisposeCount);
        }
        finally { if (!pending.Task.IsCompleted) pending.TrySetResult(Session(connection)); await DrainAsync(starting); }
    }

    [Fact]
    public async Task IndependentTaskDoesNotWaitForAnotherTasksSpawn()
    {
        var pending = Pending();
        var slowRoot = Path.Combine(Root, "synthetic-slow");
        var fast = new PtyTestConnection();
        var slow = new PtyTestConnection();
        await using var manager = new PtyTerminalSessionManager((root, _, _) => root == slowRoot
            ? pending.Task : Task.FromResult(Session(fast)));
        var slowTask = Guid.NewGuid();
        var fastTask = Guid.NewGuid();
        var starting = manager.StartAsync(slowTask, slowRoot, Token);
        try
        {
            await manager.StartAsync(fastTask, Root, Token).WaitAsync(Deadline, Token);
            await manager.WriteAsync(fastTask, "synthetic", Token);
            await manager.StopAsync(fastTask).WaitAsync(Deadline, Token);
            Assert.Equal(1, fast.DisposeCount);
            Assert.False(starting.IsCompleted);
        }
        finally { pending.TrySetResult(Session(slow)); await DrainAsync(starting); }
    }

    [Fact]
    public async Task ExitedProcessKillFailureCannotSkipConnectionCleanup()
    {
        var connection = new PtyTestConnection { KillThrows = true };
        await using var manager = new PtyTerminalSessionManager((_, _, _) => Task.FromResult(Session(connection)));
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Root, Token);
        try { await manager.StopAsync(task); }
        finally { Assert.Equal(1, connection.DisposeCount); }
    }
}
