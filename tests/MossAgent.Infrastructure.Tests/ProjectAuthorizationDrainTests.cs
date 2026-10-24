using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ProjectAuthorizationDrainTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static ProjectProfile Project() => new(Guid.NewGuid(), "drain", Path.GetTempPath(), [Path.GetTempPath()], DateTimeOffset.UtcNow);
    private static ProjectProfile Enrolled()
    {
        var path = Path.GetTempPath();
        var identity = new DirectoryObjectIdentity(path, "synthetic-volume", Guid.NewGuid().ToString("N"));
        return new ProjectProfile(Guid.NewGuid(), "drain", path, [path], DateTimeOffset.UtcNow)
            with { RootAuthorizations = [new ProjectRootAuthorization(Guid.NewGuid(), identity)] };
    }

    [Fact]
    public async Task NewerRevisionIsDeniedWhileOldLeaseDrainIsBlockedByRootDisposal()
    {
        var project = Enrolled();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        var identity = project.RootAuthorizations[0]!.Identity;
        using var gate = new ManualResetEventSlim();
        var session = new BlockingRootIdentitySession(identity, gate);
        var opens = 0;
        // Call 1 is the validation probe; call 2 is the retained lease session.
        var roots = new RootIdentityProbe
        {
            Override = (_, _) => Task.FromResult<IRootIdentitySession>(Interlocked.Increment(ref opens) == 2
                ? session : new RootIdentityProbeSession([identity], () => { }))
        };
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes, roots: roots);
        var old = await monitor.AcquireAsync(project, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);
        Assert.True(old.IsRevoked);

        try
        {
            var disposing = Task.Run(async () => await old.DisposeAsync(), Token);
            await session.DisposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
            Assert.False(disposing.IsCompleted);
            await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(reader.Current, Token));
            gate.Set();
            await disposing.WaitAsync(TimeSpan.FromSeconds(5), Token);
            await using var next = await monitor.AcquireAsync(reader.Current, Token);
            Assert.False(next.IsRevoked);
        }
        finally
        {
            gate.Set();
            await old.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15), Token);
        }
    }

    [Fact]
    public async Task ReaderObservedRevisionBeforeChangeEventStillDrains()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes);
        await using var old = await monitor.AcquireAsync(project, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        // No Changed event: the reader result itself teaches the monitor the new high-water.
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.ValidateAsync(project, Token));
        Assert.True(old.IsRevoked);
        await old.DisposeAsync();
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(project, Token));
        await using var next = await monitor.AcquireAsync(reader.Current, Token);
        Assert.False(next.IsRevoked);
    }

    [Fact]
    public async Task LateRegistrationAgainstStaleReadIsRejected()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        var held = new TaskCompletionSource<ProjectProfile?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.Read = (count, _) =>
        {
            if (count == 1) { entered.TrySetResult(); return held.Task; }
            return Task.FromResult(reader.Current);
        };
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes);
        var acquiring = monitor.AcquireAsync(project, Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        changes.Publish(project.Id, 2);
        held.TrySetResult(project);
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => acquiring);
        Assert.Equal(1, reader.Reads);
    }

    [Fact]
    public async Task SameGenerationUnrevokedNestedAcquisitionsRemainAllowed()
    {
        var project = Project();
        var reader = new ProjectAuthorizationReaderProbe(project);
        await using var monitor = new ProjectAuthorizationMonitor(reader, new ProjectAuthorizationChanges());
        await using var first = await monitor.AcquireAsync(project, Token);
        await using var second = await monitor.AcquireAsync(project, Token);
        Assert.False(first.IsRevoked);
        Assert.False(second.IsRevoked);
    }

    [Fact]
    public async Task DrainBarrierForOneProjectLeavesOtherProjectsUntouched()
    {
        var project = Project();
        var other = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes);
        var old = await monitor.AcquireAsync(project, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);
        Assert.True(old.IsRevoked);
        reader.Current = other;
        await using var otherLease = await monitor.AcquireAsync(other, Token);
        Assert.False(otherLease.IsRevoked);
        reader.Current = project with { AuthorizationRevision = 2 };
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(reader.Current, Token));
        await old.DisposeAsync();
    }

    [Fact]
    public async Task RootDisposalFailureRetainsDrainBarrierAndNeverLeaksSecretText()
    {
        var project = Enrolled();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        var identity = project.RootAuthorizations[0]!.Identity;
        using var gate = new ManualResetEventSlim(true);
        var session = new BlockingRootIdentitySession(identity, gate,
            new InvalidOperationException("synthetic-drain-secret"));
        var opens = 0;
        var roots = new RootIdentityProbe
        {
            Override = (_, _) => Task.FromResult<IRootIdentitySession>(Interlocked.Increment(ref opens) == 2
                ? session : new RootIdentityProbeSession([identity], () => { }))
        };
        var monitor = new ProjectAuthorizationMonitor(reader, changes, roots: roots);
        var old = await monitor.AcquireAsync(project, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);

        var disposing = old.DisposeAsync().AsTask();
        var exception = await Record.ExceptionAsync(() => disposing.WaitAsync(TimeSpan.FromSeconds(5), Token));
        Assert.NotNull(exception);
        var denied = await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(reader.Current, Token));
        Assert.DoesNotContain("synthetic-drain-secret", denied.Message);
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(reader.Current, Token));
        await Assert.ThrowsAnyAsync<Exception>(() => monitor.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task StaleReaderSnapshotCannotReacquireKnownLowerRevisionWithoutReading()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes);
        var old = await monitor.AcquireAsync(project, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);
        await old.DisposeAsync();
        var fresh = await monitor.AcquireAsync(reader.Current, Token);
        await fresh.DisposeAsync();

        reader.Current = project;
        changes.Publish(project.Id, 1);
        var reads = reader.Reads;
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(project, Token));
        Assert.Equal(reads, reader.Reads);
    }

    [Fact]
    public async Task EnvironmentCleanupFaultRetainsProjectBarrierUntilSuccessfulDisposal()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes);
        var environment = new FaultingEnvironmentGuard { ProjectId = project.Id, Revision = 1,
            DisposeFailure = new InvalidOperationException("synthetic-env-secret") };
        var context = new ToolExecutionContext(Guid.NewGuid(), project.PrimaryDirectory, project.AuthorizedDirectories, ApprovalPolicy.FullAccess);
        var guard = new ProjectWorkspaceGuard(monitor, project, context.TaskId, context.WorkingDirectory, context.AuthorizedRoots, environment);
        var combined = await guard.AcquireAsync(context, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);

        var disposing = combined.DisposeAsync().AsTask();
        var exception = await Record.ExceptionAsync(() => disposing.WaitAsync(TimeSpan.FromSeconds(5), Token));
        Assert.NotNull(exception);
        Assert.True(environment.Disposed);
        var denied = await Assert.ThrowsAsync<WorkspaceAuthorizationException>(() => monitor.AcquireAsync(reader.Current, Token));
        Assert.DoesNotContain("synthetic-env-secret", denied.Message);
    }

    [Fact]
    public async Task SuccessfulCombinedCleanupPermitsNewerRevision()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes);
        var environment = new FaultingEnvironmentGuard { ProjectId = project.Id, Revision = 1 };
        var context = new ToolExecutionContext(Guid.NewGuid(), project.PrimaryDirectory, project.AuthorizedDirectories, ApprovalPolicy.FullAccess);
        var guard = new ProjectWorkspaceGuard(monitor, project, context.TaskId, context.WorkingDirectory, context.AuthorizedRoots, environment);
        var combined = await guard.AcquireAsync(context, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);
        await combined.DisposeAsync();
        Assert.True(environment.Disposed);
        await using var next = await monitor.AcquireAsync(reader.Current, Token);
        Assert.False(next.IsRevoked);
    }
}
