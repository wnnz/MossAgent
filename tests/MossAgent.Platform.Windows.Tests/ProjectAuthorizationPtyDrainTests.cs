using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Terminal;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class ProjectAuthorizationPtyDrainTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(20);
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static PtyTerminalSession Session(PtyTestConnection connection) =>
        new(connection, new CancellationTokenSource(), _ => { });

    private static ToolExecutionContext NextContext(ProjectAuthorizationPtyFixture fixture, Guid taskId) =>
        new(taskId, fixture.Project.PrimaryDirectory, [], ApprovalPolicy.FullAccess)
        {
            WorkspaceGuard = new ProjectWorkspaceGuard(fixture.Monitor,
                fixture.Project with { AuthorizationRevision = 2, AuthorizedDirectories = [] },
                taskId, fixture.Project.PrimaryDirectory, [])
        };

    [Fact]
    public async Task BlockedSessionCleanupHoldsDrainBarrierUntilDisposeReturns()
    {
        await using var fixture = new ProjectAuthorizationPtyFixture(Path.GetTempPath());
        using var gate = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked = new PtyTestConnection { Disposing = () =>
        {
            entered.TrySetResult();
            if (!gate.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("synthetic pty disposal gate");
        } };
        var healthy = new PtyTestConnection();
        var connections = new Queue<PtyTestConnection>();
        connections.Enqueue(blocked);
        connections.Enqueue(healthy);
        await using var manager = new PtyTerminalSessionManager((_, _, _) => Task.FromResult(Session(connections.Dequeue())));
        var task = Guid.NewGuid();
        await manager.StartAuthorizedAsync(fixture.Context(task), Token);
        fixture.Revoke();

        var stopping = manager.StopAsync(task);
        try
        {
            await entered.Task.WaitAsync(Deadline, Token);
            Assert.False(stopping.IsCompleted);
            await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() =>
                manager.StartAuthorizedAsync(NextContext(fixture, Guid.NewGuid()), Token));
        }
        finally { gate.Set(); }
        await stopping.WaitAsync(Deadline, Token);
        Assert.Equal(TerminalSessionState.Stopped, manager.GetSnapshot(task)!.State);

        var next = Guid.NewGuid();
        await manager.StartAuthorizedAsync(NextContext(fixture, next), Token);
        Assert.Equal(TerminalSessionState.Running, manager.GetSnapshot(next)!.State);
    }

    [Fact]
    public async Task FailedSessionCleanupKeepsCleanupFailedAndRetainsAuthorization()
    {
        await using var fixture = new ProjectAuthorizationPtyFixture(Path.GetTempPath());
        var connection = new PtyTestConnection { Disposing = () => throw new InvalidOperationException("synthetic pty cleanup secret") };
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
            var exception = await Record.ExceptionAsync(() => manager.StopAsync(task).WaitAsync(Deadline, Token));
            Assert.NotNull(exception);
            var denied = await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() =>
                manager.StartAuthorizedAsync(NextContext(fixture, Guid.NewGuid()), Token));
            Assert.DoesNotContain("synthetic pty cleanup secret", denied.Message);
        }
        finally { await Assert.ThrowsAnyAsync<Exception>(() => manager.DisposeAsync().AsTask()); }
    }
}
