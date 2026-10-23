using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class TransferredRootLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransferredQuarantineRemainsOwnedAfterItsParentRootIsDisposed(bool cleanupFails)
    {
        await using var fixture = await WorktreeRootLifetimeFixture.CreateAsync();
        var nativeChild = new QuarantineDisposalProbe(cleanupFails);
        var factory = new RootIdentityProbe { Override = (_, _) => Task.FromResult<IRootIdentitySession>(
            new QuarantineRootProbe(fixture.Project.RootAuthorizations[0].Identity, nativeChild)) };
        var owner = await fixture.Monitor.AcquireRootOperationsAsync(fixture.Project, fixture.Token);
        using var identity = await owner.TrackRoots(factory).OpenAsync([fixture.Project.PrimaryDirectory], fixture.Token);
        var transfer = Assert.IsAssignableFrom<IPathQuarantineSessionFactory>(identity);
        var child = transfer.Create(fixture.Project.PrimaryDirectory, "unused", [fixture.Project.PrimaryDirectory], fixture.Token);
        try
        {
            await child.MoveAsync(static _ => Task.CompletedTask, fixture.Token);
            Assert.True(child.Moved);
            await child.DeleteMovedAsync(static _ => Task.CompletedTask, fixture.Token);
            identity.Dispose();
            var disposal = owner.DisposeAsync().AsTask();
            Assert.NotSame(disposal, await Task.WhenAny(disposal, Task.Delay(250, fixture.Token)));
            Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
            if (cleanupFails)
            {
                Assert.Throws<InvalidOperationException>(child.Dispose);
                await Assert.ThrowsAsync<InvalidOperationException>(() => disposal.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token));
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    Assert.False((await fixture.DrainAsync()).IsDrained);
                    Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
                }
            }
            else
            {
                child.Dispose();
                child.Dispose();
                await disposal.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
                await fixture.WaitForDrainAsync();
            }
            Assert.Equal(1, nativeChild.Disposals);
        }
        finally
        {
            identity.Dispose();
            try { child.Dispose(); }
            catch (InvalidOperationException) when (cleanupFails) { }
            try { await owner.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), fixture.Token); }
            catch (InvalidOperationException) when (cleanupFails) { }
        }
    }
    [Fact]
    public async Task RejectedTransferDoesNotLeakAPhantomReservationOrInventFactoryCapabilities()
    {
        await using var fixture = await WorktreeRootLifetimeFixture.CreateAsync();
        var child = new QuarantineDisposalProbe(false);
        var opens = 0;
        var factory = new RootIdentityProbe { Override = (_, _) =>
        {
            opens++;
            return Task.FromResult<IRootIdentitySession>(new QuarantineRootProbe(
                fixture.Project.RootAuthorizations[0].Identity, child, rejectCreation: true));
        } };
        await using var owner = await fixture.Monitor.AcquireRootOperationsAsync(fixture.Project, fixture.Token);
        var tracked = owner.TrackRoots(factory);
        Assert.False(tracked is ITransferableRootIdentitySessionFactory);
        using var identity = await tracked.OpenAsync([fixture.Project.PrimaryDirectory], fixture.Token);
        var transfer = Assert.IsAssignableFrom<IPathQuarantineSessionFactory>(identity);
        Assert.Throws<UnauthorizedAccessException>(() => transfer.Create(
            fixture.Project.PrimaryDirectory, "unused", [fixture.Project.PrimaryDirectory], fixture.Token));
        identity.Dispose();
        await owner.DisposeAsync();
        await fixture.WaitForDrainAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => tracked.OpenAsync(
            [fixture.Project.PrimaryDirectory], fixture.Token));
        Assert.Equal(1, opens);
        Assert.Equal(0, child.Disposals);
    }

}
