using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AutomationSchedulerShutdownTests
{
    [Theory]
    [InlineData("tick")]
    [InlineData("manual")]
    [InlineData("startup")]
    public async Task ShutdownCancelsTheOwnedPortBeforeWaitingForTheGate(string operation)
    {
        await using var fixture = new AutomationShutdownFixture(operation == "startup" ? "recover" : "read");
        var token = TestContext.Current.CancellationToken;
        if (operation != "startup") await fixture.Scheduler.StartAsync(token);
        var running = fixture.Track(operation switch
        {
            "startup" => fixture.Scheduler.StartAsync(token),
            "manual" => fixture.Scheduler.RunNowAsync(fixture.Repository.Plan.Id, token),
            _ => fixture.Scheduler.TickAsync(token)
        });
        await fixture.Repository.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
        var stop = fixture.Scheduler.StopAsync();
        await fixture.Repository.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running.WaitAsync(TimeSpan.FromSeconds(2), token));
        await stop.WaitAsync(TimeSpan.FromSeconds(2), token);
        Assert.False(fixture.Scheduler.IsAvailable);
        Assert.False(fixture.Lease.IsHeld);
        Assert.Empty(fixture.Runner.Requests);
        Assert.Equal(0, fixture.Repository.Claims);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("recover")]
    public async Task NoncooperativePortsRemainOwnedAndQueuedOperationsAreCancelled(string phase)
    {
        await using var fixture = new AutomationShutdownFixture(phase, false);
        var token = TestContext.Current.CancellationToken;
        if (phase != "recover") await fixture.Scheduler.StartAsync(token);
        var running = fixture.Track(phase == "recover" ? fixture.Scheduler.StartAsync(token)
            : fixture.Scheduler.RunNowAsync(fixture.Repository.Plan.Id, token));
        await fixture.Repository.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
        var queued = new[] { fixture.Track(fixture.Scheduler.TickAsync(token)),
            fixture.Track(fixture.Scheduler.RunNowAsync(fixture.Repository.Plan.Id, token)),
            fixture.Track(fixture.Scheduler.StartAsync(token)) };
        var stop = fixture.Scheduler.StopAsync();
        await fixture.Repository.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
        Assert.Same(stop, fixture.Scheduler.StopAsync());
        Assert.Same(stop, fixture.Scheduler.DisposeAsync().AsTask());
        Assert.False(stop.IsCompleted);
        Assert.True(fixture.Lease.IsHeld);
        foreach (var waiting in queued)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(2), token));
        Assert.False(await fixture.Scheduler.RunNowAsync(fixture.Repository.Plan.Id, token));
        await fixture.Scheduler.StartAsync(token);
        await fixture.Scheduler.TickAsync(token);
        fixture.Repository.Released.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        await stop.WaitAsync(TimeSpan.FromSeconds(2), token);
        Assert.Equal(phase == "recover" ? 0 : 1, fixture.Repository.Reads);
        Assert.Equal(1, fixture.Repository.Recoveries);
        Assert.Empty(fixture.Runner.Requests);
        Assert.False(fixture.Lease.IsHeld);
        Assert.Equal(1, fixture.Lease.Releases);
    }

    [Fact]
    public async Task CallerCancellationOfAQueuedRequestDoesNotCancelTheOwnedRead()
    {
        await using var fixture = new AutomationShutdownFixture("read", false);
        var token = TestContext.Current.CancellationToken;
        await fixture.Scheduler.StartAsync(token);
        var reading = fixture.Track(fixture.Scheduler.TickAsync(token));
        await fixture.Repository.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        var queued = fixture.Track(fixture.Scheduler.RunNowAsync(fixture.Repository.Plan.Id, caller.Token));
        caller.Cancel();
        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.Equal(caller.Token, failure.CancellationToken);
        Assert.False(fixture.Repository.PortToken.IsCancellationRequested);
        fixture.Repository.Released.TrySetResult();
        await reading;
        Assert.Empty(fixture.Runner.Requests);
    }

    [Fact]
    public async Task StartedNoncooperativeExecutionKeepsTheLeaseUntilItsReturnAndFinalization()
    {
        await using var fixture = new AutomationShutdownFixture();
        var token = TestContext.Current.CancellationToken;
        fixture.Runner.Block = true;
        await fixture.Scheduler.StartAsync(token);
        Assert.True(await fixture.Scheduler.RunNowAsync(fixture.Repository.Plan.Id, token));
        await fixture.Runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
        var stop = fixture.Scheduler.StopAsync();
        await fixture.Runner.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
        Assert.Same(stop, fixture.Scheduler.StopAsync());
        Assert.Same(stop, fixture.Scheduler.DisposeAsync().AsTask());
        Assert.False(stop.IsCompleted);
        Assert.True(fixture.Lease.IsHeld);
        Assert.Empty(fixture.Repository.Finished);
        fixture.Runner.Released.TrySetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(2), token);
        Assert.False(fixture.Lease.IsHeld);
        Assert.Equal(1, fixture.Lease.Releases);
        Assert.Equal(1, fixture.Repository.FinishAttempts);
        Assert.Equal(Assert.Single(fixture.Runner.Requests), Assert.Single(fixture.Repository.Finished).TaskId);
    }

    [Fact]
    public async Task StartedTerminalPersistenceKeepsTheLeaseUntilStorageReturns()
    {
        await using var fixture = new AutomationShutdownFixture("finish", false);
        var token = TestContext.Current.CancellationToken;
        await fixture.Scheduler.StartAsync(token);
        Assert.True(await fixture.Scheduler.RunNowAsync(fixture.Repository.Plan.Id, token));
        await fixture.Repository.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
        var stop = fixture.Scheduler.StopAsync();
        Assert.False(stop.IsCompleted);
        Assert.True(fixture.Lease.IsHeld);
        Assert.False(fixture.Repository.PortToken.CanBeCanceled);
        Assert.Empty(fixture.Repository.Finished);
        fixture.Repository.Released.TrySetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(2), token);
        Assert.Equal(AutomationRunStatus.Completed, Assert.Single(fixture.Repository.Finished).Status);
        Assert.Equal(1, fixture.Repository.FinishAttempts);
        Assert.Equal(1, fixture.Lease.Releases);
    }
}
