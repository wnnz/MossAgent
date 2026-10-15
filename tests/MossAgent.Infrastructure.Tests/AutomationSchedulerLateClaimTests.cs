using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AutomationSchedulerLateClaimTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShutdownRejectsLateDurableClaimsWithoutInvokingTheRunner(bool rejectFinish)
    {
        await using var fixture = new AutomationShutdownFixture("claim", false);
        var token = TestContext.Current.CancellationToken;
        fixture.Repository.RejectFinish = rejectFinish;
        await fixture.Scheduler.StartAsync(token);
        var claiming = fixture.Track(fixture.Scheduler.RunNowAsync(fixture.Repository.Plan.Id, token));
        await fixture.Repository.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
        var stop = fixture.Scheduler.StopAsync();
        await fixture.Repository.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
        Assert.False(stop.IsCompleted);
        Assert.True(fixture.Lease.IsHeld);
        fixture.Repository.Released.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => claiming);
        await stop.WaitAsync(TimeSpan.FromSeconds(2), token);
        Assert.Empty(fixture.Runner.Requests);
        Assert.False(fixture.Scheduler.IsRunning(fixture.Repository.Plan.Id));
        Assert.Equal(1, fixture.Repository.FinishAttempts);
        if (rejectFinish)
        {
            Assert.Empty(fixture.Repository.Finished);
            Assert.Equal("persistence-error", fixture.Scheduler.ErrorCode);
        }
        else
        {
            var finished = Assert.Single(fixture.Repository.Finished);
            Assert.Equal(fixture.Repository.Claimed!.TaskId, finished.TaskId);
            Assert.Equal(AutomationRunStatus.Cancelled, finished.Status);
            Assert.NotNull(finished.FinishedAt);
            Assert.Null(finished.ErrorCode);
        }
    }

    [Theory]
    [InlineData("read")]
    [InlineData("claim")]
    public async Task CallerCancellationRejectsLatePortResultsAndPreservesItsToken(string phase)
    {
        await using var fixture = new AutomationShutdownFixture(phase, false);
        var token = TestContext.Current.CancellationToken;
        await fixture.Scheduler.StartAsync(token);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        var pending = fixture.Track(fixture.Scheduler.RunNowAsync(fixture.Repository.Plan.Id, caller.Token));
        await fixture.Repository.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
        caller.Cancel();
        await fixture.Repository.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
        fixture.Repository.Released.TrySetResult();
        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(caller.Token, failure.CancellationToken);
        Assert.Empty(fixture.Runner.Requests);
        Assert.True(fixture.Scheduler.IsAvailable);
        Assert.True(fixture.Lease.IsHeld);
        Assert.Equal(phase == "claim" ? 1 : 0, fixture.Repository.FinishAttempts);
    }
}
