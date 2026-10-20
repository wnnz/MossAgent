using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceSendPreparationTests
{
    [Fact]
    public async Task CancellationKeepsOwnershipUntilTheActualOperationFinishesAndNextAttemptGetsANewToken()
    {
        using var preparation = new WorkspaceSendPreparation();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = CancellationToken.None;
        var execution = preparation.Start(async token => { observed = token; await release.Task; });
        Assert.True(preparation.IsActive);
        Assert.True(preparation.CanCancel);
        await Assert.ThrowsAsync<InvalidOperationException>(() => preparation.Start(_ => Task.CompletedTask));
        var stopping = preparation.StopAsync();
        Assert.Same(execution, stopping);
        Assert.True(observed.IsCancellationRequested);
        Assert.True(preparation.IsActive);
        Assert.False(preparation.CanCancel);
        Assert.False(stopping.IsCompleted);
        preparation.Cancel();
        release.SetResult();
        await stopping.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(preparation.IsActive);
        Assert.False(preparation.IsCancellationRequested);
        await preparation.Start(token =>
        {
            Assert.NotEqual(observed, token);
            Assert.False(token.IsCancellationRequested);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task CooperativeCancellationCompletesAsCancelledAndNeverLeavesAnActiveOwner()
    {
        using var preparation = new WorkspaceSendPreparation();
        var execution = preparation.Start(token => Task.Delay(Timeout.InfiniteTimeSpan, token));
        preparation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.True(execution.IsCanceled);
        Assert.False(preparation.IsActive);
    }

    [Fact]
    public async Task FailureAndStateNotificationFailureAreObservedByTheTrackedCompletion()
    {
        using var preparation = new WorkspaceSendPreparation();
        var failure = new IOException("synthetic operation failure");
        var execution = preparation.Start(_ => Task.FromException(failure));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => execution));
        Assert.False(preparation.IsActive);
        preparation.StateChanged += () => throw new InvalidOperationException("synthetic observer failure");
        var failedNotification = preparation.Start(_ => Task.CompletedTask);
        await Assert.ThrowsAsync<AggregateException>(() => failedNotification);
        Assert.False(preparation.IsActive);
    }

    [Fact]
    public async Task DisposalCancelsPendingWorkAndDetachesNotificationsButStillAllowsJoiningIt()
    {
        var preparation = new WorkspaceSendPreparation();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var afterDisposal = 0;
        var disposed = false;
        preparation.StateChanged += () => { if (disposed) afterDisposal++; };
        var execution = preparation.Start(_ => release.Task);
        disposed = true;
        preparation.Dispose();
        preparation.Dispose();
        Assert.False(preparation.CanCancel);
        Assert.True(preparation.IsCancellationRequested);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => preparation.Start(_ => Task.CompletedTask));
        release.SetResult();
        await preparation.StopAsync().WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(execution.IsCompletedSuccessfully);
        Assert.Equal(0, afterDisposal);
    }

    [Fact]
    public async Task CancellingCommittedTaskUsesAnIndependentBoundedPersistenceToken()
    {
        using var preparation = new WorkspaceSendPreparation();
        var repository = new TestWorkspaceRepository
        {
            SaveTaskHandler = (task, token) =>
            {
                Assert.Equal(AgentTaskStatus.Cancelled, task.Status);
                Assert.True(token.CanBeCanceled);
                Assert.False(token.IsCancellationRequested);
                return Task.CompletedTask;
            }
        };
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), null, "fixture", ApprovalPolicy.ReadOnly, AgentTaskStatus.Running, now, now, null);
        var result = await preparation.CancelPreparedTaskAsync(new WorkspaceTaskFactory(repository, new WorkspaceWorktreeProbe()), task);
        Assert.Equal(task.Id, result.Id);
        Assert.Equal(AgentTaskStatus.Cancelled, result.Status);
        Assert.Equal(result, repository.SavedTask);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnconfirmedFinalization_IsNeverMistakenForASuccessfulUserCancellation(bool cancellationFailure)
    {
        using var preparation = new WorkspaceSendPreparation();
        Exception inner = cancellationFailure ? new OperationCanceledException("synthetic-secret deadline")
            : new IOException("synthetic-secret storage failure");
        var repository = new TestWorkspaceRepository { SaveTaskHandler = (_, _) => Task.FromException(inner) };
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), null, "fixture", ApprovalPolicy.ReadOnly, AgentTaskStatus.Running, now, now, null);
        var failure = await Assert.ThrowsAsync<WorkspacePreparationFinalizationException>(() => preparation.CancelPreparedTaskAsync(
            new WorkspaceTaskFactory(repository, new WorkspaceWorktreeProbe()), task));
        Assert.Same(inner, failure.InnerException);
        var environment = new WorkspaceTaskEnvironmentViewModel();
        environment.ReportPreparationFailure(failure);
        Assert.Contains(task.Id.ToString(), environment.PreparationNotice);
        Assert.DoesNotContain("synthetic-secret", environment.PreparationNotice);
    }
}
