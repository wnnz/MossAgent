using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class WorktreeRootLifetimeFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateOpenFailureWithoutAHandleReleasesTheReservation(bool monitorRoots)
    {
        await using var fixture = await WorktreeRootLifetimeFixture.CreateAsync(monitorRoots);
        var task = await fixture.PrepareAsync("acquire");
        await fixture.BlockWorktreeAsync(task.WorktreePath!);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var pending = fixture.RunAsync("acquire", task, cancellation.Token);
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2), fixture.Token));
        Assert.False((await fixture.DrainAsync()).IsDrained);
        Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
        fixture.FailNative();
        await fixture.WaitForDrainAsync();
    }

    [Fact]
    public async Task SavedRevisionRevokesPendingOpenButKeepsReservationAndOriginalGrant()
    {
        await using var fixture = await WorktreeRootLifetimeFixture.CreateAsync();
        var task = await fixture.PrepareAsync("acquire");
        var grant = await fixture.Service.ReadAuthorizationAsync(task.Id, fixture.Token);
        await fixture.BlockWorktreeAsync(task.WorktreePath!);
        var pending = fixture.RunAsync("acquire", task, fixture.Token);
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), fixture.Token);
        await fixture.Repository.SaveAuthorizationAsync(fixture.Project with
        { AuthorizedDirectories = [fixture.Project.PrimaryDirectory, fixture.Git.Outside] }, fixture.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2), fixture.Token));
        Assert.False((await fixture.DrainAsync()).IsDrained);
        Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
        fixture.CompleteNative();
        await fixture.WaitForDrainAsync();
        Assert.Equal(grant, await fixture.Service.ReadAuthorizationAsync(task.Id, fixture.Token));
        Assert.Equal(task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
    }

    [Fact]
    public async Task DisposingOneWorktreeCannotReleaseOrRevokeAnotherTasksLease()
    {
        await using var fixture = await WorktreeRootLifetimeFixture.CreateAsync();
        var first = await fixture.PrepareAsync("acquire");
        var second = await fixture.PrepareAsync("acquire");
        await using var firstLease = await fixture.Service.AcquireAsync(fixture.Project, first.Id, first.WorktreePath!, fixture.Token);
        await using var secondLease = await fixture.Service.AcquireAsync(fixture.Project, second.Id, second.WorktreePath!, fixture.Token);
        Assert.Equal(2, (await fixture.PeerResources.ListOutstandingAsync(fixture.Project.Id, fixture.Token)).Count);
        await firstLease.DisposeAsync();
        Assert.Single(await fixture.PeerResources.ListOutstandingAsync(fixture.Project.Id, fixture.Token));
        Assert.False(secondLease.IsRevoked);
        Assert.False(secondLease.Revocation.IsCancellationRequested);
        await secondLease.DisposeAsync();
        await fixture.WaitForDrainAsync();
    }
    [Fact]
    public async Task ReturnedLeaseDisposalCachesFailureAndNeverReleasesFailedNativeCleanup()
    {
        await using var fixture = await WorktreeRootLifetimeFixture.CreateAsync();
        var task = await fixture.PrepareAsync("acquire");
        await fixture.BlockWorktreeAsync(task.WorktreePath!);
        var pending = fixture.Service.AcquireAsync(fixture.Project, task.Id, task.WorktreePath!, fixture.Token);
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), fixture.Token);
        fixture.CompleteNative(cleanupFails: true);
        var lease = await pending.WaitAsync(TimeSpan.FromSeconds(10), fixture.Token);
        var first = lease.DisposeAsync().AsTask();
        var second = lease.DisposeAsync().AsTask();
        Assert.Same(first, second);
        await Assert.ThrowsAsync<InvalidOperationException>(() => first.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.False((await fixture.DrainAsync()).IsDrained);
            Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
        }
    }

    [Fact]
    public async Task TimeoutCannotHideANativeOpenThatIgnoresCancellation()
    {
        var time = new ManualToolTimeProvider();
        await using var fixture = await WorktreeRootLifetimeFixture.CreateAsync(operationTime: time);
        var task = await fixture.PrepareAsync("acquire");
        await fixture.BlockWorktreeAsync(task.WorktreePath!);
        var pending = fixture.RunAsync("acquire", task, fixture.Token);
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), fixture.Token);
        time.Advance(TimeSpan.FromSeconds(31));
        await Assert.ThrowsAsync<TimeoutException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2), fixture.Token));
        Assert.False((await fixture.DrainAsync()).IsDrained);
        Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
        fixture.CompleteNative();
        await fixture.WaitForDrainAsync();
    }

    [Fact]
    public async Task MonitorShutdownWaitsForActualLateWorktreeCleanup()
    {
        await using var fixture = await WorktreeRootLifetimeFixture.CreateAsync();
        var task = await fixture.PrepareAsync("acquire");
        await fixture.BlockWorktreeAsync(task.WorktreePath!);
        var pending = fixture.RunAsync("acquire", task, fixture.Token);
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), fixture.Token);
        var shutdown = fixture.Monitor.DisposeAsync().AsTask();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2), fixture.Token));
            Assert.False(shutdown.IsCompleted);
            Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
            fixture.CompleteNative();
            await shutdown.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            Assert.True((await fixture.DrainAsync(peer: true)).IsDrained);
        }
        finally
        {
            fixture.CompleteNative();
            await shutdown.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        }
    }

}
