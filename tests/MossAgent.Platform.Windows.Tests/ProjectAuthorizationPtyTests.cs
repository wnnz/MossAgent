using MossAgent.Domain;
using MossAgent.Platform.Windows.Terminal;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class ProjectAuthorizationPtyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static TimeSpan Deadline => TimeSpan.FromSeconds(10);
    private static PtyTerminalSession Session(PtyTestConnection connection) => new(connection, new CancellationTokenSource(), _ => { });

    [Fact]
    public async Task StartReturnRetainsLeaseAndRevocationStopsOnlyItsOwner()
    {
        await using var first = new ProjectAuthorizationPtyFixture(Path.GetTempPath());
        await using var other = new ProjectAuthorizationPtyFixture(Path.GetTempPath());
        var connections = new Queue<PtyTestConnection>([new(), new(), new()]);
        var retired = connections.Peek();
        await using var manager = new PtyTerminalSessionManager((_, _, _) => Task.FromResult(Session(connections.Dequeue())));
        var task = Guid.NewGuid();
        var otherTask = Guid.NewGuid();
        await manager.StartAuthorizedAsync(first.Context(task), Token);
        await manager.StartAuthorizedAsync(other.Context(otherTask), Token);
        var oldId = manager.GetSnapshot(task)!.SessionId;
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.StateChanged += (_, state) => { if (state.Snapshot.TaskId == task && state.Snapshot.State == TerminalSessionState.Stopped) stopped.TrySetResult(); };
        first.Revoke();
        await stopped.Task.WaitAsync(Deadline, Token);
        Assert.Equal(1, retired.DisposeCount);
        Assert.Equal(TerminalSessionState.Running, manager.GetSnapshot(otherTask)!.State);
        await manager.WriteAsync(otherTask, "still usable", Token);
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => manager.StartAuthorizedAsync(first.Context(task), Token));
        await using var replacement = new ProjectAuthorizationPtyFixture(Path.GetTempPath());
        await manager.StartAuthorizedAsync(replacement.Context(task), Token);
        Assert.NotEqual(oldId, manager.GetSnapshot(task)!.SessionId);
        first.Changes.Publish(first.Project.Id, 3);
        await manager.WriteAsync(task, "new owner", Token);
        Assert.Equal(TerminalSessionState.Running, manager.GetSnapshot(task)!.State);
    }

    [Fact]
    public async Task RevocationDuringSpawnRejectsLateHandoffAndDrainsConnection()
    {
        await using var fixture = new ProjectAuthorizationPtyFixture(Path.GetTempPath());
        var pending = new TaskCompletionSource<PtyTerminalSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new PtyTestConnection();
        await using var manager = new PtyTerminalSessionManager((_, _, _) => { entered.TrySetResult(); return pending.Task; });
        var task = Guid.NewGuid();
        var starting = manager.StartAuthorizedAsync(fixture.Context(task), Token);
        await entered.Task.WaitAsync(Deadline, Token);
        fixture.Revoke();
        pending.TrySetResult(Session(connection));
        var exception = await Record.ExceptionAsync(() => starting.WaitAsync(Deadline, Token));
        Assert.True(exception is OperationCanceledException or WorkspaceAuthorizationException);
        await manager.StopAsync(task).WaitAsync(Deadline, Token);
        Assert.Equal(1, connection.DisposeCount);
        Assert.Equal(TerminalSessionState.Stopped, manager.GetSnapshot(task)!.State);
    }

    [Fact]
    public async Task RevocationCleanupFailureRemainsVisibleAndCannotRestartSameOwner()
    {
        await using var fixture = new ProjectAuthorizationPtyFixture(Path.GetTempPath());
        var connection = new PtyTestConnection { Disposing = () => throw new InvalidOperationException("synthetic cleanup failure") };
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manager = new PtyTerminalSessionManager((_, _, _) => Task.FromResult(Session(connection)));
        var task = Guid.NewGuid();
        manager.StateChanged += (_, state) => { if (state.Snapshot.State == TerminalSessionState.CleanupFailed) failed.TrySetResult(); };
        try
        {
            await manager.StartAuthorizedAsync(fixture.Context(task), Token);
            fixture.Revoke();
            await failed.Task.WaitAsync(Deadline, Token);
            Assert.Equal(TerminalSessionState.CleanupFailed, manager.GetSnapshot(task)!.State);
            await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => manager.WriteAsync(task, "denied", Token));
        }
        finally { await Assert.ThrowsAnyAsync<Exception>(() => manager.DisposeAsync().AsTask()); }
    }

    [Fact]
    public async Task NativeConPtyRevocationStopsPublishedParentAndChildProcesses()
    {
        await using var native = new PtyNativeFixture();
        await using var fixture = new ProjectAuthorizationPtyFixture(native.Root);
        var task = Guid.NewGuid();
        await native.Manager.StartAuthorizedAsync(fixture.Context(task), Token);
        await native.CreateChildScriptAsync();
        await native.Manager.WriteAsync(task, PtyNativeFixture.PublishPid("parent.pid") + "; " + PtyNativeFixture.LaunchChild + "\r", Token);
        var parent = await native.BindAsync("parent.pid");
        var child = await native.BindAsync("child.pid");
        fixture.Revoke();
        await PtyNativeFixture.AssertExitedAsync(parent);
        await PtyNativeFixture.AssertExitedAsync(child);
        await native.Manager.StopAsync(task).WaitAsync(Deadline, Token);
        Assert.Equal(TerminalSessionState.Stopped, native.Manager.GetSnapshot(task)!.State);
        Assert.False(File.Exists(Path.Combine(native.Root, "late.txt")));
    }
}
