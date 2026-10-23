using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AuthorizationLeaseDisposalTests
{
    [Fact]
    public async Task CrossInstanceDrainCannotFinishBeforePinnedRootsExit()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (project, roots) = await EnrollAsync(fixture, () =>
        {
            entered.TrySetResult();
            release.Task.WaitAsync(TimeSpan.FromSeconds(10), fixture.Token).GetAwaiter().GetResult();
        });
        var resources = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        var peerResources = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        await using var monitor = new ProjectAuthorizationMonitor(fixture.Repository,
            new ProjectAuthorizationChanges(), roots: roots, resources: resources);
        await using var peer = new ProjectAuthorizationMonitor(fixture.Repository,
            new ProjectAuthorizationChanges(), resources: peerResources);
        var lease = await monitor.AcquireAsync(project, fixture.Token);
        var disposing = Task.Run(async () => await lease.DisposeAsync(), fixture.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            var waiting = await peer.WaitForQuiescenceAsync(project.Id, project.AuthorizationRevision,
                TimeSpan.FromMilliseconds(75), fixture.Token);
            Assert.False(waiting.IsDrained);
            Assert.Equal(1, waiting.OutstandingOperations);
            Assert.Single(await peerResources.ListOutstandingAsync(project.Id, fixture.Token));
            release.TrySetResult();
            await disposing.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
            var drained = await peer.WaitForQuiescenceAsync(project.Id, project.AuthorizationRevision,
                TimeSpan.FromSeconds(1), fixture.Token);
            Assert.True(drained.IsDrained);
            Assert.Empty(await peerResources.ListOutstandingAsync(project.Id, fixture.Token));
        }
        finally
        {
            release.TrySetResult();
            await disposing.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRootCleanupStaysOutstandingAcrossRepeatedDrainChecks(bool persistent)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (project, roots) = await EnrollAsync(fixture,
            () => throw new InvalidOperationException("Synthetic root cleanup failure."));
        var resources = persistent
            ? new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System) : null;
        var monitor = new ProjectAuthorizationMonitor(fixture.Repository,
            new ProjectAuthorizationChanges(), roots: roots, resources: resources);
        try
        {
            var lease = await monitor.AcquireAsync(project, fixture.Token);
            await Assert.ThrowsAsync<InvalidOperationException>(() => lease.DisposeAsync().AsTask());
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var result = await monitor.WaitForQuiescenceAsync(project.Id, project.AuthorizationRevision,
                    TimeSpan.FromMilliseconds(75), fixture.Token);
                Assert.False(result.IsDrained);
                Assert.Equal(1, result.OutstandingOperations);
            }
            if (resources is not null)
                Assert.Single(await resources.ListOutstandingAsync(project.Id, fixture.Token));
            await Assert.ThrowsAsync<InvalidOperationException>(() => lease.DisposeAsync().AsTask());
        }
        finally
        {
            try { await monitor.DisposeAsync(); }
            catch (InvalidOperationException) { }
        }
    }

    [Fact]
    public async Task BrokenRevocationObserverCannotPreventRootOrRegistrationCleanup()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var released = 0;
        var (project, roots) = await EnrollAsync(fixture, () => Interlocked.Increment(ref released));
        var resources = new SqliteAuthorizationResourceRegistry(fixture.Connections, TimeProvider.System);
        var changes = new ProjectAuthorizationChanges();
        await using var monitor = new ProjectAuthorizationMonitor(fixture.Repository, changes,
            roots: roots, resources: resources);
        var lease = await monitor.AcquireAsync(project, fixture.Token);
        using var observer = lease.Revocation.Register(() => throw new InvalidOperationException("Broken observer."));
        changes.Publish(project.Id, project.AuthorizationRevision + 1);
        await Task.WhenAll(lease.DisposeAsync().AsTask(), lease.DisposeAsync().AsTask());
        Assert.Equal(1, released);
        Assert.Empty(await resources.ListOutstandingAsync(project.Id, fixture.Token));
        Assert.True((await monitor.WaitForQuiescenceAsync(project.Id, project.AuthorizationRevision,
            TimeSpan.FromSeconds(1), fixture.Token)).IsDrained);
    }

    private static async Task<(ProjectProfile Project, RootIdentityProbe Roots)> EnrollAsync(
        SqliteFailurePersistenceFixture fixture, Action releaseHeldRoots)
    {
        var roots = new RootIdentityProbe();
        var repository = new SqliteWorkspaceRepository(fixture.Connections, rootIdentities: roots);
        var project = await repository.ConfirmRootObjectsAsync(fixture.Project, fixture.Token);
        using var template = await roots.OpenAsync(project.AuthorizedDirectories, fixture.Token);
        var identities = template.Roots.ToArray();
        var opened = 0;
        roots.Override = (_, token) =>
        {
            token.ThrowIfCancellationRequested();
            var held = Interlocked.Increment(ref opened) == 2;
            return Task.FromResult<IRootIdentitySession>(new RootIdentityProbeSession(identities,
                () => { if (held) releaseHeldRoots(); }));
        };
        return (project, roots);
    }
}
