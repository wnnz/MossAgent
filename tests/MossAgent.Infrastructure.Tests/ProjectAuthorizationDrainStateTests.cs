using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ProjectAuthorizationDrainStateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static ProjectProfile Project() => new(Guid.NewGuid(), "drain-state", Path.GetTempPath(), [Path.GetTempPath()], DateTimeOffset.UtcNow);
    private static ProjectProfile Enrolled()
    {
        var path = Path.GetTempPath();
        var identity = new DirectoryObjectIdentity(path, "synthetic-volume", Guid.NewGuid().ToString("N"));
        return new ProjectProfile(Guid.NewGuid(), "drain-state", path, [path], DateTimeOffset.UtcNow)
            with { RootAuthorizations = [new ProjectRootAuthorization(Guid.NewGuid(), identity)] };
    }

    [Fact]
    public async Task RevokedThenReleasedLeaseTransitionsDrainingThenIdle()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes);
        var observed = new List<AuthorizationDrainState>();
        monitor.DrainStateChanged += projectId => observed.Add(monitor.DrainState(projectId));
        var lease = await monitor.AcquireAsync(project, Token);
        Assert.Equal(AuthorizationDrainState.Idle, monitor.DrainState(project.Id));

        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);
        Assert.Equal(AuthorizationDrainState.Draining, monitor.DrainState(project.Id));
        await lease.DisposeAsync();
        Assert.Equal(AuthorizationDrainState.Idle, monitor.DrainState(project.Id));
        Assert.Equal([AuthorizationDrainState.Draining, AuthorizationDrainState.Idle], observed);
    }

    [Fact]
    public async Task RootReleaseFailureMarksCleanupUnknownAndRetainsAdmissionBarrier()
    {
        var project = Enrolled();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        var identity = project.RootAuthorizations[0]!.Identity;
        using var gate = new ManualResetEventSlim(true);
        var session = new BlockingRootIdentitySession(identity, gate,
            new InvalidOperationException("synthetic-release-secret"));
        var opens = 0;
        var roots = new RootIdentityProbe
        {
            Override = (_, _) => Task.FromResult<IRootIdentitySession>(Interlocked.Increment(ref opens) == 2
                ? session : new RootIdentityProbeSession([identity], () => { }))
        };
        var monitor = new ProjectAuthorizationMonitor(reader, changes, roots: roots);
        var observed = new List<AuthorizationDrainState>();
        monitor.DrainStateChanged += projectId => observed.Add(monitor.DrainState(projectId));
        var lease = await monitor.AcquireAsync(project, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);
        Assert.Equal(AuthorizationDrainState.Draining, monitor.DrainState(project.Id));

        await Assert.ThrowsAnyAsync<Exception>(() => lease.DisposeAsync().AsTask());
        Assert.Equal(AuthorizationDrainState.CleanupUnknown, monitor.DrainState(project.Id));
        var denied = await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(reader.Current, Token));
        Assert.DoesNotContain("synthetic-release-secret", denied.Message);
        Assert.Equal([AuthorizationDrainState.Draining, AuthorizationDrainState.CleanupUnknown], observed);
        await Assert.ThrowsAnyAsync<Exception>(() => monitor.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task ProjectStaysDrainingUntilEveryRevokedLeaseIsReleased()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes);
        var first = await monitor.AcquireAsync(project, Token);
        var second = await monitor.AcquireAsync(project, Token);
        Assert.Equal(AuthorizationDrainState.Idle, monitor.DrainState(project.Id));

        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);
        Assert.Equal(AuthorizationDrainState.Draining, monitor.DrainState(project.Id));
        await first.DisposeAsync();
        Assert.Equal(AuthorizationDrainState.Draining, monitor.DrainState(project.Id));
        await second.DisposeAsync();
        Assert.Equal(AuthorizationDrainState.Idle, monitor.DrainState(project.Id));
    }

    [Fact]
    public async Task RevocationOnOneProjectLeavesOtherProjectsIdle()
    {
        var project = Project();
        var other = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new MultiProjectAuthorizationReaderProbe();
        reader.Set(project);
        reader.Set(other);
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes);
        var notified = new List<Guid>();
        monitor.DrainStateChanged += notified.Add;
        var lease = await monitor.AcquireAsync(project, Token);
        var otherLease = await monitor.AcquireAsync(other, Token);

        reader.Set(project with { AuthorizationRevision = 2 });
        changes.Publish(project.Id, 2);
        Assert.Equal(AuthorizationDrainState.Draining, monitor.DrainState(project.Id));
        Assert.Equal(AuthorizationDrainState.Idle, monitor.DrainState(other.Id));
        Assert.DoesNotContain(other.Id, notified);
        await using var extra = await monitor.AcquireAsync(other, Token);
        await lease.DisposeAsync();
        await otherLease.DisposeAsync();
        Assert.Equal(AuthorizationDrainState.Idle, monitor.DrainState(project.Id));
    }

    [Fact]
    public async Task ReaderFailureSelfRevocationNotifiesTheDrainState()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes);
        var observed = new List<AuthorizationDrainState>();
        monitor.DrainStateChanged += projectId => observed.Add(monitor.DrainState(projectId));
        var lease = await monitor.AcquireAsync(project, Token);
        reader.Read = (_, _) => throw new InvalidOperationException("reader outage");
        await Task.Delay(Timeout.Infinite, lease.Revocation).ContinueWith(_ => { }, Token)
            .WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.True(lease.IsRevoked);
        Assert.Equal(AuthorizationDrainState.Draining, monitor.DrainState(project.Id));
        Assert.Equal([AuthorizationDrainState.Draining], observed);
        await lease.DisposeAsync();
        Assert.Equal(AuthorizationDrainState.Idle, monitor.DrainState(project.Id));
    }

    [Fact]
    public async Task ThrowingObserverCannotBreakRevocationOrLeaseCleanup()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes);
        monitor.DrainStateChanged += _ => throw new InvalidOperationException("broken drain observer");
        var lease = await monitor.AcquireAsync(project, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);
        Assert.Equal(AuthorizationDrainState.Draining, monitor.DrainState(project.Id));
        await lease.DisposeAsync();
        Assert.Equal(AuthorizationDrainState.Idle, monitor.DrainState(project.Id));
    }
}
