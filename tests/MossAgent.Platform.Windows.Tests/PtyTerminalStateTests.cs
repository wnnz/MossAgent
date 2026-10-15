using System.Collections.Concurrent;
using MossAgent.Platform.Windows.Terminal;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyTerminalStateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private static PtyTerminalSession Session(PtyTestConnection connection, Action<string> output) => new(connection, new(), output);

    private static async Task<TerminalSessionSnapshot> WaitStateAsync(PtyTerminalSessionManager manager, Guid task, TerminalSessionState state)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancellation.CancelAfter(Deadline);
        while (true)
        {
            var snapshot = manager.GetSnapshot(task);
            if (snapshot?.State == state) return snapshot;
            await Task.Delay(10, cancellation.Token);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    public async Task NaturalExitRetiresOwnerAndAllowsExplicitRestart(int exitCode)
    {
        var connections = new ConcurrentQueue<PtyTestConnection>();
        await using var manager = new PtyTerminalSessionManager((_, output, _) =>
        {
            var connection = new PtyTestConnection();
            connections.Enqueue(connection);
            return Task.FromResult(Session(connection, output));
        });
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        var running = manager.GetSnapshot(task)!;
        Assert.Equal(TerminalSessionState.Running, running.State);
        connections.TryPeek(out var first);
        first!.Exit(exitCode);
        var exited = await WaitStateAsync(manager, task, TerminalSessionState.Exited);
        Assert.Equal(exitCode, exited.ExitCode);
        Assert.Equal(running.SessionId, exited.SessionId);
        Assert.True(exited.Revision > running.Revision);
        Assert.Equal(1, first.DisposeCount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.WriteAsync(task, "rejected", Token));
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        var restarted = manager.GetSnapshot(task)!;
        Assert.NotEqual(exited.SessionId, restarted.SessionId);
        first.Exit(55);
        Assert.Equal(restarted, manager.GetSnapshot(task));
        await manager.StopAsync(task);
        Assert.Equal(TerminalSessionState.Stopped, manager.GetSnapshot(task)!.State);
    }

    [Fact]
    public async Task ExitBeforeSubscriptionIsDetectedByZeroWaitProbe()
    {
        var connection = new PtyTestConnection { HasExited = true, ExitCode = 7 };
        await using var manager = new PtyTerminalSessionManager((_, output, _) => Task.FromResult(Session(connection, output)));
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        var snapshot = await WaitStateAsync(manager, task, TerminalSessionState.Exited);
        Assert.Equal(7, snapshot.ExitCode);
        Assert.Equal(1, connection.DisposeCount);
    }

    [Fact]
    public async Task NaturalCleanupMustReturnBeforeRestartAndDoesNotStopOtherTasks()
    {
        var cancellationToken = Token;
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new PtyTestConnection { Disposing = () => { entered.TrySetResult(); release.Wait(cancellationToken); } };
        var second = new PtyTestConnection();
        var calls = 0;
        await using var manager = new PtyTerminalSessionManager((_, output, _) =>
            Task.FromResult(Session(Interlocked.Increment(ref calls) == 1 ? first : second, output)));
        var firstTask = Guid.NewGuid();
        var secondTask = Guid.NewGuid();
        try
        {
            await manager.StartAsync(firstTask, Path.GetTempPath(), cancellationToken);
            await manager.StartAsync(secondTask, Path.GetTempPath(), cancellationToken);
            first.Exit(0);
            await entered.Task.WaitAsync(Deadline, Token);
            Assert.Equal(TerminalSessionState.Stopping, manager.GetSnapshot(firstTask)!.State);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.StartAsync(firstTask, Path.GetTempPath(), Token));
            Assert.Equal(2, calls);
            await manager.WriteAsync(secondTask, "retained", Token);
            Assert.Equal(0, second.DisposeCount);
            var stopping = manager.StopAsync(firstTask);
            Assert.False(stopping.IsCompleted);
            release.Set();
            await stopping.WaitAsync(Deadline, Token);
            Assert.Equal(TerminalSessionState.Exited, manager.GetSnapshot(firstTask)!.State);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task StartupFailureIsSafeAndCanBeRetried()
    {
        var calls = 0;
        await using var manager = new PtyTerminalSessionManager((_, output, _) =>
            ++calls == 1 ? Task.FromException<PtyTerminalSession>(new IOException("secret-api-key")) : Task.FromResult(Session(new(), output)));
        var task = Guid.NewGuid();
        await Assert.ThrowsAsync<IOException>(() => manager.StartAsync(task, Path.GetTempPath(), Token));
        var failed = manager.GetSnapshot(task)!;
        Assert.Equal(TerminalSessionState.Failed, failed.State);
        Assert.DoesNotContain("secret", failed.ToString());
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        Assert.Equal(TerminalSessionState.Running, manager.GetSnapshot(task)!.State);
    }

    [Fact]
    public async Task ReadFailureCleansConnectionWithoutLeakingExceptionText()
    {
        var reader = new MemoryStream();
        reader.Dispose();
        var connection = new PtyTestConnection { ReaderStream = reader };
        await using var manager = new PtyTerminalSessionManager((_, output, _) => Task.FromResult(Session(connection, output)));
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        await WaitStateAsync(manager, task, TerminalSessionState.Failed);
        Assert.Equal(1, connection.DisposeCount);
    }

    [Fact]
    public async Task CleanupFailureRetainsOwnerAndRejectsReplacement()
    {
        var connection = new PtyTestConnection { Disposing = () => throw new IOException("secret-cleanup-key") };
        var calls = 0;
        var manager = new PtyTerminalSessionManager((_, output, _) => { calls++; return Task.FromResult(Session(connection, output)); });
        var task = Guid.NewGuid();
        try
        {
            await manager.StartAsync(task, Path.GetTempPath(), Token);
            connection.Exit(0);
            var snapshot = await WaitStateAsync(manager, task, TerminalSessionState.CleanupFailed);
            Assert.DoesNotContain("secret", snapshot.ToString());
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.StartAsync(task, Path.GetTempPath(), Token));
            Assert.Equal(1, calls);
            await Assert.ThrowsAsync<IOException>(() => manager.StopAsync(task).WaitAsync(Deadline, Token));
        }
        finally
        {
            await manager.DisposeAsync().AsTask().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
            connection.ReaderStream.Dispose();
            connection.WriterStream.Dispose();
        }
    }

    [Fact]
    public async Task CancellingPendingCreationRetainsStartingThenStoppingState()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<PtyTerminalSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var manager = new PtyTerminalSessionManager((_, _, _) => { entered.SetResult(); return release.Task; });
        var task = Guid.NewGuid();
        var starting = manager.StartAsync(task, Path.GetTempPath(), cancellation.Token);
        await entered.Task.WaitAsync(Deadline, Token);
        Assert.Equal(TerminalSessionState.Starting, manager.GetSnapshot(task)!.State);
        var stopping = manager.StopAsync(task);
        try
        {
            Assert.Equal(TerminalSessionState.Stopping, manager.GetSnapshot(task)!.State);
            Assert.False(stopping.IsCompleted);
            var connection = new PtyTestConnection();
            release.SetResult(Session(connection, _ => { }));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting.WaitAsync(Deadline, Token));
            await stopping.WaitAsync(Deadline, Token);
            Assert.Equal(TerminalSessionState.Stopped, manager.GetSnapshot(task)!.State);
            Assert.Equal(1, connection.DisposeCount);
        }
        finally
        {
            if (!release.Task.IsCompleted) release.TrySetResult(Session(new(), _ => { }));
            await stopping.WaitAsync(Deadline, Token);
        }
    }

    [Fact]
    public async Task ThrowingStateObserverCannotPreventCleanupOrOtherObservers()
    {
        var states = new ConcurrentQueue<TerminalSessionSnapshot>();
        var connection = new PtyTestConnection();
        await using var manager = new PtyTerminalSessionManager((_, output, _) => Task.FromResult(Session(connection, output)));
        manager.StateChanged += (_, _) => throw new IOException("observer-secret");
        manager.StateChanged += (_, args) => states.Enqueue(args.Snapshot);
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        await manager.StopAsync(task);
        Assert.Contains(states, snapshot => snapshot.State == TerminalSessionState.Running);
        Assert.Contains(states, snapshot => snapshot.State == TerminalSessionState.Stopped);
        Assert.Equal(1, connection.DisposeCount);
    }
}
