using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PendingAuthorizationRootTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CancellationCannotHideAnUnfinishedNativeRootOpen(bool acquire, bool persistent)
    {
        await using var fixture = await PendingAuthorizationRootFixture.CreateAsync(persistent);
        fixture.BlockRoots();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var operation = fixture.ValidateOrAcquireAsync(acquire, cancellation.Token);
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2), fixture.Token));
        Assert.False((await fixture.DrainAsync()).IsDrained);
        if (persistent)
        {
            Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
            Assert.NotEmpty(await fixture.PeerResources.ListOutstandingAsync(fixture.Project.Id, fixture.Token));
        }
        fixture.CompleteNative();
        await fixture.ReleaseAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        await WaitForDrainAsync(fixture, persistent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateNativeFailureWithoutAHandleEventuallyReleasesTheReservation(bool acquire)
    {
        await using var fixture = await PendingAuthorizationRootFixture.CreateAsync();
        fixture.BlockRoots();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var operation = fixture.ValidateOrAcquireAsync(acquire, cancellation.Token);
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2), fixture.Token));
        Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
        fixture.FailNative();
        await WaitForDrainAsync(fixture, peer: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateCleanupFailureRemainsUnknownLocallyAndToOtherInstances(bool acquire)
    {
        await using var fixture = await PendingAuthorizationRootFixture.CreateAsync();
        fixture.BlockRoots();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var operation = fixture.ValidateOrAcquireAsync(acquire, cancellation.Token);
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2), fixture.Token));
        Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
        fixture.CompleteNative(cleanupFails: true);
        await fixture.ReleaseAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        Assert.False((await fixture.DrainAsync()).IsDrained);
        Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
    }

    [Fact]
    public async Task LeaseDisposalCannotForgetThePollingReadersLateNativeSession()
    {
        await using var fixture = await PendingAuthorizationRootFixture.CreateAsync();
        var lease = await fixture.Monitor.AcquireAsync(fixture.Project, fixture.Token);
        fixture.BlockRoots();
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        var disposal = lease.DisposeAsync().AsTask();
        try
        {
            Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
            var observed = await Task.WhenAny(disposal, Task.Delay(TimeSpan.FromSeconds(1), fixture.Token));
            Assert.NotSame(disposal, observed);
            fixture.CompleteNative();
            await disposal.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            await fixture.ReleaseAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            await WaitForDrainAsync(fixture, peer: true);
        }
        finally
        {
            fixture.CompleteNative();
            await disposal.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        }
    }

    private static async Task WaitForDrainAsync(PendingAuthorizationRootFixture fixture, bool peer)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (!(await fixture.DrainAsync(peer)).IsDrained) await Task.Delay(15, deadline.Token);
        Assert.Empty(await fixture.PeerResources.ListOutstandingAsync(fixture.Project.Id, fixture.Token));
    }
}
