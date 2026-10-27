using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ProjectAuthorizationDrainBoardTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static ProjectProfile Project() => new(Guid.NewGuid(), "drain-board", Path.GetTempPath(), [Path.GetTempPath()], DateTimeOffset.UtcNow);
    private static ProjectProfile Enrolled()
    {
        var path = Path.GetTempPath();
        var identity = new DirectoryObjectIdentity(path, "synthetic-volume", Guid.NewGuid().ToString("N"));
        return new ProjectProfile(Guid.NewGuid(), "drain-board", path, [path], DateTimeOffset.UtcNow)
            with { RootAuthorizations = [new ProjectRootAuthorization(Guid.NewGuid(), identity)] };
    }

    [Fact]
    public async Task RevocationPublishesDrainingThenReleasePublishesIdle()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        var board = new RecordingProjectDrainStateBoard();
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes, board: board);
        var lease = await monitor.AcquireAsync(project, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);

        await board.WaitForPublishCountAsync(1, Token).WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.Equal([(project.Id, monitor.InstanceId, AuthorizationDrainState.Draining)], board.Publishes);
        await lease.DisposeAsync();
        await board.WaitForPublishCountAsync(2, Token).WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.Equal(AuthorizationDrainState.Idle, board.Publishes[^1].State);
        Assert.Equal(AuthorizationDrainState.Idle,
            await board.AggregateAsync(project.Id, DateTimeOffset.MinValue, Token));
    }

    [Fact]
    public async Task RepeatedRevocationsPublishOnlyOnActualTransitions()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        var board = new RecordingProjectDrainStateBoard();
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes, board: board);
        var lease = await monitor.AcquireAsync(project, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);
        await board.WaitForPublishCountAsync(1, Token).WaitAsync(TimeSpan.FromSeconds(5), Token);

        reader.Current = project with { AuthorizationRevision = 3 };
        changes.Publish(project.Id, 3);
        await Task.Delay(100, Token);
        Assert.Single(board.Publishes);
        await lease.DisposeAsync();
        await board.WaitForPublishCountAsync(2, Token).WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.Equal([AuthorizationDrainState.Draining, AuthorizationDrainState.Idle],
            board.Publishes.Select(publish => publish.State).ToArray());
    }

    [Fact]
    public async Task ReleaseFailurePublishesCleanupUnknown()
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
        var board = new RecordingProjectDrainStateBoard();
        var monitor = new ProjectAuthorizationMonitor(reader, changes, roots: roots, board: board);
        var lease = await monitor.AcquireAsync(project, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);

        await Assert.ThrowsAnyAsync<Exception>(() => lease.DisposeAsync().AsTask());
        await board.WaitForPublishCountAsync(2, Token).WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.Equal([AuthorizationDrainState.Draining, AuthorizationDrainState.CleanupUnknown],
            board.Publishes.Select(publish => publish.State).ToArray());
        await Assert.ThrowsAnyAsync<Exception>(() => monitor.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task ReaderFailureSelfRevocationPublishesDraining()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        var board = new RecordingProjectDrainStateBoard();
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes, board: board);
        var lease = await monitor.AcquireAsync(project, Token);
        reader.Read = (_, _) => throw new InvalidOperationException("reader outage");
        await Task.Delay(Timeout.Infinite, lease.Revocation).ContinueWith(_ => { }, Token)
            .WaitAsync(TimeSpan.FromSeconds(5), Token);

        await board.WaitForPublishCountAsync(1, Token).WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.Equal(AuthorizationDrainState.Draining, board.Publishes[^1].State);
        await lease.DisposeAsync();
    }

    [Fact]
    public async Task PublishFailureDoesNotBreakRevocationOrAdmission()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        var board = new RecordingProjectDrainStateBoard
        {
            PublishFailure = new InvalidOperationException("board outage")
        };
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes, board: board);
        var lease = await monitor.AcquireAsync(project, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);

        await board.WaitForPublishCountAsync(1, Token).WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.Equal(AuthorizationDrainState.Draining, monitor.DrainState(project.Id));
        await Assert.ThrowsAsync<WorkspaceAuthorizationException>(
            () => monitor.AcquireAsync(reader.Current, Token));
        await lease.DisposeAsync();
        Assert.Equal(AuthorizationDrainState.Idle, monitor.DrainState(project.Id));
    }

    [Fact]
    public async Task DisposeClearsOnlyOwnBoardRows()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        var board = new RecordingProjectDrainStateBoard();
        var monitor = new ProjectAuthorizationMonitor(reader, changes, board: board);
        var foreign = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await board.PublishAsync(project.Id, monitor.InstanceId, AuthorizationDrainState.Draining, now, Token);
        await board.PublishAsync(project.Id, foreign, AuthorizationDrainState.CleanupUnknown, now, Token);

        await monitor.DisposeAsync();
        Assert.Equal(1, board.Clears);
        Assert.Equal(AuthorizationDrainState.CleanupUnknown,
            await board.AggregateAsync(project.Id, DateTimeOffset.MinValue, Token));
    }

    [Fact]
    public async Task ObserveDrainStateTakesWorstOfLocalAndBoard()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        var board = new RecordingProjectDrainStateBoard();
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes, board: board);
        var foreign = Guid.NewGuid();
        await board.PublishAsync(project.Id, foreign, AuthorizationDrainState.Draining,
            DateTimeOffset.UtcNow, Token);
        Assert.Equal(AuthorizationDrainState.Draining,
            await monitor.ObserveDrainStateAsync(project.Id, Token));

        var lease = await monitor.AcquireAsync(project, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);
        Assert.Equal(AuthorizationDrainState.Draining,
            await monitor.ObserveDrainStateAsync(project.Id, Token));
        await board.PublishAsync(project.Id, foreign, AuthorizationDrainState.CleanupUnknown,
            DateTimeOffset.UtcNow, Token);
        Assert.Equal(AuthorizationDrainState.CleanupUnknown,
            await monitor.ObserveDrainStateAsync(project.Id, Token));
        await lease.DisposeAsync();
    }

    [Fact]
    public async Task StaleForeignRowIsObservedAsCleanupUnknown()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        var board = new RecordingProjectDrainStateBoard();
        var time = new ManualToolTimeProvider();
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes, time, board: board);
        var staleAt = time.GetUtcNow() - ProjectAuthorizationMonitor.BoardStaleWindow - TimeSpan.FromSeconds(1);
        await board.PublishAsync(project.Id, Guid.NewGuid(), AuthorizationDrainState.Draining, staleAt, Token);

        Assert.Equal(AuthorizationDrainState.Idle, monitor.DrainState(project.Id));
        Assert.Equal(AuthorizationDrainState.CleanupUnknown,
            await monitor.ObserveDrainStateAsync(project.Id, Token));
        Assert.Equal(time.GetUtcNow() - ProjectAuthorizationMonitor.BoardStaleWindow,
            board.LastStaleBefore);
    }

    [Fact]
    public async Task StaleForeignRowIsReapedByObservation()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        var board = new RecordingProjectDrainStateBoard();
        var time = new ManualToolTimeProvider();
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes, time, board: board);
        var staleAt = time.GetUtcNow() - ProjectAuthorizationMonitor.BoardStaleWindow - TimeSpan.FromSeconds(1);
        await board.PublishAsync(project.Id, Guid.NewGuid(), AuthorizationDrainState.Draining,
            staleAt, Token);

        Assert.Equal(AuthorizationDrainState.CleanupUnknown,
            await monitor.ObserveDrainStateAsync(project.Id, Token));
        Assert.Equal(AuthorizationDrainState.Idle,
            await monitor.ObserveDrainStateAsync(project.Id, Token));
        Assert.Equal(AuthorizationDrainState.Idle,
            await board.AggregateAsync(project.Id, DateTimeOffset.MinValue, Token));
    }

    [Fact]
    public async Task ReapStaleDeletesEveryStaleRowAcrossProjects()
    {
        var board = new RecordingProjectDrainStateBoard();
        var now = DateTimeOffset.UtcNow;
        var staleAt = now - ProjectAuthorizationMonitor.BoardStaleWindow - TimeSpan.FromSeconds(1);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var fresh = Guid.NewGuid();
        await board.PublishAsync(first, Guid.NewGuid(), AuthorizationDrainState.Draining, staleAt, Token);
        await board.PublishAsync(second, Guid.NewGuid(), AuthorizationDrainState.CleanupUnknown, staleAt, Token);
        await board.PublishAsync(fresh, Guid.NewGuid(), AuthorizationDrainState.Draining, now, Token);
        var staleBefore = now - ProjectAuthorizationMonitor.BoardStaleWindow;

        Assert.Equal(2, await board.ReapStaleAsync(staleBefore, Token));
        Assert.Equal(1, board.ReapCalls);
        Assert.Equal(AuthorizationDrainState.Idle,
            await board.AggregateAsync(first, staleBefore, Token));
        Assert.Equal(AuthorizationDrainState.Draining,
            await board.AggregateAsync(fresh, staleBefore, Token));
    }

    [Fact]
    public async Task BoardReadFailureFallsBackToLocalState()
    {
        var project = Project();
        var changes = new ProjectAuthorizationChanges();
        var reader = new ProjectAuthorizationReaderProbe(project);
        var board = new RecordingProjectDrainStateBoard
        {
            AggregateFailure = new InvalidOperationException("board outage")
        };
        await using var monitor = new ProjectAuthorizationMonitor(reader, changes, board: board);
        Assert.Equal(AuthorizationDrainState.Idle,
            await monitor.ObserveDrainStateAsync(project.Id, Token));

        var lease = await monitor.AcquireAsync(project, Token);
        reader.Current = project with { AuthorizationRevision = 2 };
        changes.Publish(project.Id, 2);
        Assert.Equal(AuthorizationDrainState.Draining,
            await monitor.ObserveDrainStateAsync(project.Id, Token));
        await lease.DisposeAsync();
    }
}
