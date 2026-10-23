using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class WorktreeObjectCancellationTests
{
    [Fact]
    public async Task CancelledLegacyConfirmationDoesNotPersistGrantOrResumeTaskAndReleasesLateSession()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Git.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var task = await fixture.SaveTaskAsync(id, path, seedIntent: true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<IRootIdentitySession>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Roots.Override = (_, _) => { entered.TrySetResult(); return pending.Task; };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var confirmation = fixture.Service.ConfirmLegacyAsync(fixture.Project, task, cancellation.Token);
        await entered.Task.WaitAsync(fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => confirmation.WaitAsync(fixture.Token));
        var late = new RootIdentityProbeSession([new DirectoryObjectIdentity(path, "late-volume", "late-file")], () => { });
        pending.SetResult(late);
        await late.Disposed.Task.WaitAsync(fixture.Token);
        Assert.Null(await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
        Assert.Equal(task, Assert.Single(await fixture.Storage.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
    }

    [Fact]
    public async Task CancellationDoesNotWaitForIdentityReaderAndDisposesItsLateSession()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var id = Guid.NewGuid();
        var path = await fixture.Service.CreateAsync(fixture.Project, id, fixture.Token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<IRootIdentitySession>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Roots.Override = (_, _) => { entered.TrySetResult(); return pending.Task; };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var execution = fixture.Service.AcquireAsync(fixture.Project, id, path, cancellation.Token);
        await entered.Task.WaitAsync(fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution.WaitAsync(fixture.Token));
        var late = new RootIdentityProbeSession([new DirectoryObjectIdentity(path, "late-volume", "late-file")], () => { });
        pending.SetResult(late);
        await late.Disposed.Task.WaitAsync(fixture.Token);
        Assert.NotNull(await fixture.Service.ReadAuthorizationAsync(id, fixture.Token));
    }

    [Fact]
    public async Task TwoTaskLeasesOwnIndependentSessionsAndDisposeIdempotently()
    {
        await using var fixture = await WorktreeObjectAuthorizationFixture.CreateAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var firstPath = await fixture.Service.CreateAsync(fixture.Project, first, fixture.Token);
        var secondPath = await fixture.Service.CreateAsync(fixture.Project, second, fixture.Token);
        await using var firstLease = await fixture.Service.AcquireAsync(fixture.Project, first, firstPath, fixture.Token);
        await using var secondLease = await fixture.Service.AcquireAsync(fixture.Project, second, secondPath, fixture.Token);
        Assert.Equal(2, fixture.Roots.ActiveSessions);
        await firstLease.DisposeAsync();
        await firstLease.DisposeAsync();
        Assert.Equal(1, fixture.Roots.ActiveSessions);
        await fixture.Service.ValidateAsync(fixture.Project, second, secondPath, fixture.Token);
        Assert.Equal(1, fixture.Roots.ActiveSessions);
    }
}
