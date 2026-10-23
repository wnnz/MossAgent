using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class WorktreeNativeLifetimeTests
{
    [Fact]
    public async Task ReturnedLeasePinsNativeWorktreeAndKeepsPeerReservationUntilDisposed()
    {
        await using var fixture = await WorktreeNativeLifetimeFixture.CreateAsync();
        var task = await fixture.CreateWorktreeAsync();
        await using var lease = await fixture.Service.AcquireAsync(fixture.Project, task.Id, task.WorktreePath!, fixture.Token);
        Assert.NotEmpty(await fixture.PeerResources.ListOutstandingAsync(fixture.Project.Id, fixture.Token));
        Assert.Throws<IOException>(() => Directory.Move(task.WorktreePath!, task.WorktreePath + "-moved"));
        await lease.DisposeAsync();
        await fixture.WaitForDrainAsync();
        Directory.Move(task.WorktreePath!, task.WorktreePath + "-moved");
        Directory.Move(task.WorktreePath + "-moved", task.WorktreePath!);
    }

    [Fact]
    public async Task CancellationRetainsTheActualLateNativeHandleUntilItIsDeliveredAndClosed()
    {
        await using var fixture = await WorktreeNativeLifetimeFixture.CreateAsync();
        var task = await fixture.CreateWorktreeAsync();
        var grant = await fixture.Service.ReadAuthorizationAsync(task.Id, fixture.Token);
        fixture.Roots.Block(task.WorktreePath!);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var operation = fixture.Service.ValidateAsync(fixture.Project, task.Id, task.WorktreePath!, cancellation.Token);
        await fixture.Roots.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2), fixture.Token));
        Assert.False((await fixture.DrainAsync()).IsDrained);
        Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
        Assert.Throws<IOException>(() => Directory.Move(task.WorktreePath!, task.WorktreePath + "-moved"));
        fixture.Roots.Release();
        await fixture.WaitForDrainAsync();
        Directory.Move(task.WorktreePath!, task.WorktreePath + "-moved");
        Directory.Move(task.WorktreePath + "-moved", task.WorktreePath!);
        Assert.Equal(grant, await fixture.Service.ReadAuthorizationAsync(task.Id, fixture.Token));
        Assert.Equal(task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
    }

    [Fact]
    public async Task NativeRemovalTransfersThePinnedRootWithoutAnyReopenFallback()
    {
        await using var fixture = await WorktreeNativeLifetimeFixture.CreateAsync();
        var task = await fixture.CreateWorktreeAsync();
        var previous = fixture.Roots.Transfers;
        await fixture.Service.RemoveAsync(fixture.Project, task.WorktreePath!, fixture.Token);
        Assert.Equal(previous + 1, fixture.Roots.Transfers);
        Assert.False(Directory.Exists(task.WorktreePath));
        Assert.Equal(task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
        await fixture.WaitForDrainAsync();
        await fixture.AssertCleanupCompletedAsync(task.Id);
        Assert.False(Directory.Exists(Path.Combine(fixture.Project.PrimaryDirectory,
            ".git", "worktrees", Path.GetFileName(task.WorktreePath)!)));
    }
    [Fact]
    public async Task TransferredNativeQuarantineKeepsItsOriginalReservationUntilClosed()
    {
        await using var fixture = await WorktreeNativeLifetimeFixture.CreateAsync();
        var task = await fixture.CreateWorktreeAsync();
        var owner = await fixture.Monitor.AcquireRootOperationsAsync(fixture.Project, fixture.Token);
        var factory = Assert.IsAssignableFrom<ITransferableRootIdentitySessionFactory>(owner.TrackRoots(fixture.Roots));
        using var identity = await factory.OpenTransferableAsync([task.WorktreePath!], fixture.Token);
        var transfer = Assert.IsAssignableFrom<IPathQuarantineSessionFactory>(identity);
        var destination = task.WorktreePath + "-quarantine";
        using var quarantine = transfer.Create(task.WorktreePath!, destination,
            [Path.GetDirectoryName(task.WorktreePath)!], fixture.Token);
        try
        {
            identity.Dispose();
            var disposal = owner.DisposeAsync().AsTask();
            Assert.NotSame(disposal, await Task.WhenAny(disposal, Task.Delay(250, fixture.Token)));
            Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
            Assert.Throws<IOException>(() => Directory.Move(task.WorktreePath!, destination));
            quarantine.Dispose();
            await disposal.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            await fixture.WaitForDrainAsync();
        }
        finally
        {
            identity.Dispose();
            quarantine.Dispose();
            await owner.DisposeAsync();
        }
    }

}
