using MossAgent.Application.Git;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class WorktreeRootLifetimeTests
{
    [Fact]
    public async Task ReturnedWorktreeLeaseKeepsItsProjectReservationUntilNativeCleanup()
    {
        await using var fixture = await WorktreeRootLifetimeFixture.CreateAsync();
        var task = await fixture.PrepareAsync("acquire");
        await using var lease = await fixture.Service.AcquireAsync(fixture.Project, task.Id, task.WorktreePath!, fixture.Token);
        Assert.NotEmpty(await fixture.PeerResources.ListOutstandingAsync(fixture.Project.Id, fixture.Token));
        Assert.False((await fixture.DrainAsync()).IsDrained);
        Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
        await lease.DisposeAsync();
        await lease.DisposeAsync();
        await fixture.WaitForDrainAsync();
    }

    [Fact]
    public async Task ReturnedWorktreeLeaseIsRevokedBySavedAuthorizationChanges()
    {
        await using var fixture = await WorktreeRootLifetimeFixture.CreateAsync();
        var task = await fixture.PrepareAsync("acquire");
        await using var lease = await fixture.Service.AcquireAsync(fixture.Project, task.Id, task.WorktreePath!, fixture.Token);
        var updated = await fixture.Repository.SaveAuthorizationAsync(fixture.Project with
        { AuthorizedDirectories = [fixture.Project.PrimaryDirectory, fixture.Git.Outside] }, fixture.Token);
        Assert.True(updated.AuthorizationRevision > lease.Revision);
        Assert.True(lease.IsRevoked);
        Assert.True(lease.Revocation.IsCancellationRequested);
        await lease.DisposeAsync();
        await fixture.WaitForDrainAsync();
    }

    [Theory]
    [InlineData("acquire")]
    [InlineData("validate")]
    [InlineData("confirm")]
    [InlineData("create")]
    [InlineData("reauthorize")]
    [InlineData("remove")]
    [InlineData("remove-transfer")]
    public async Task CancellationLeavesLateWorktreeRootsVisibleWithoutReplayingTheOperation(string operation)
    {
        await using var fixture = await WorktreeRootLifetimeFixture.CreateAsync();
        var task = await fixture.PrepareAsync(operation);
        var grant = await fixture.Service.ReadAuthorizationAsync(task.Id, fixture.Token);
        await fixture.BlockWorktreeAsync(task.WorktreePath!, operation == "remove-transfer" ? 2 : 1);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var pending = fixture.RunAsync(operation, task, cancellation.Token);
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), fixture.Token);
        await cancellation.CancelAsync();
        var failure = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(2), fixture.Token));
        if (operation == "create") Assert.IsAssignableFrom<OperationCanceledException>(Assert.IsType<GitWorktreeCreationException>(failure).InnerException);
        else Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.False((await fixture.DrainAsync()).IsDrained);
        Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
        fixture.CompleteNative();
        await fixture.CleanupAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        await fixture.WaitForDrainAsync();
        Assert.Equal(grant, await fixture.Service.ReadAuthorizationAsync(task.Id, fixture.Token));
        var tasks = await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token);
        if (operation == "create") Assert.Empty(tasks);
        else Assert.Equal(task, Assert.Single(tasks));
        Assert.True(Directory.Exists(task.WorktreePath));
        await fixture.Git.AssertSentinelAsync();
    }

    [Fact]
    public async Task LateCleanupFailureRemainsUnknownOnRepeatedLocalAndPeerChecks()
    {
        await using var fixture = await WorktreeRootLifetimeFixture.CreateAsync();
        var task = await fixture.PrepareAsync("acquire");
        await fixture.BlockWorktreeAsync(task.WorktreePath!);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var pending = fixture.RunAsync("acquire", task, cancellation.Token);
        await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2), fixture.Token));
        fixture.CompleteNative(cleanupFails: true);
        await fixture.CleanupAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.False((await fixture.DrainAsync()).IsDrained);
            Assert.False((await fixture.DrainAsync(peer: true)).IsDrained);
            Assert.NotEmpty(await fixture.PeerResources.ListOutstandingAsync(fixture.Project.Id, fixture.Token));
        }
    }
}
