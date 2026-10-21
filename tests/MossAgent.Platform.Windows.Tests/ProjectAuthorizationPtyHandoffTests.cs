using MossAgent.Domain;
using MossAgent.Platform.Windows.Terminal;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class ProjectAuthorizationPtyHandoffTests
{
    [Fact]
    public async Task RevokedLeaseBeforeCancellationDeliveryRetiresLateSessionAsStopped()
    {
        var token = TestContext.Current.CancellationToken;
        var deadline = TimeSpan.FromSeconds(10);
        await using var fixture = new ProjectAuthorizationPtyFixture(Path.GetTempPath());
        var context = fixture.Context(Guid.NewGuid());
        var guard = new CapturingTerminalWorkspaceGuard(context.WorkspaceGuard!);
        context = context with { WorkspaceGuard = guard };
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<PtyTerminalSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var deliverCancellation = new ManualResetEventSlim();
        var connection = new PtyTestConnection();
        CancellationToken spawnToken = default;
        await using var manager = new PtyTerminalSessionManager((_, _, startup) =>
        {
            spawnToken = startup;
            entered.TrySetResult();
            return pending.Task;
        });
        manager.StateChanged += (_, state) =>
        {
            if (state.Snapshot.State == TerminalSessionState.Stopping) stopping.TrySetResult();
        };
        var starting = manager.StartAuthorizedAsync(context, token);
        CancellationTokenRegistration notification = default;
        try
        {
            await entered.Task.WaitAsync(deadline, token);
            var lease = Assert.IsAssignableFrom<MossAgent.Tools.Abstractions.ITaskWorkspaceLease>(guard.Lease);
            notification = lease.Revocation.Register(() =>
            {
                cancelling.TrySetResult();
                deliverCancellation.Wait(token);
            });
            fixture.Revoke();
            await cancelling.Task.WaitAsync(deadline, token);
            Assert.True(lease.IsRevoked);
            Assert.True(lease.Revocation.IsCancellationRequested);
            Assert.False(spawnToken.IsCancellationRequested);
            pending.SetResult(new(connection, new CancellationTokenSource(), _ => { }));
            await stopping.Task.WaitAsync(deadline, token);
        }
        finally
        {
            deliverCancellation.Set();
            pending.TrySetCanceled(token);
            notification.Dispose();
        }
        var exception = await Record.ExceptionAsync(() => starting.WaitAsync(deadline, token));
        Assert.IsType<WorkspaceAuthorizationException>(exception);
        await manager.StopAsync(context.TaskId).WaitAsync(deadline, token);
        Assert.Equal(1, connection.DisposeCount);
        Assert.Equal(TerminalSessionState.Stopped, manager.GetSnapshot(context.TaskId)!.State);
    }
}
