using MossAgent.Application.Authorization;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteProjectDrainStateBoardTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static DateTimeOffset Now => DateTimeOffset.UtcNow;
    private static DateTimeOffset StaleBoundary => Now - ProjectAuthorizationMonitor.BoardStaleWindow;

    [Fact]
    public async Task PublishThenAggregateRoundTripsAndScopesPerProject()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var board = new SqliteProjectDrainStateBoard(fixture.Connections);
        var project = Guid.NewGuid();
        var other = Guid.NewGuid();
        var instance = Guid.NewGuid();
        Assert.Equal(AuthorizationDrainState.Idle,
            await board.AggregateAsync(project, StaleBoundary, Token));

        await board.PublishAsync(project, instance, AuthorizationDrainState.Draining, Now, Token);
        await board.PublishAsync(other, instance, AuthorizationDrainState.CleanupUnknown, Now, Token);
        Assert.Equal(AuthorizationDrainState.Draining,
            await board.AggregateAsync(project, StaleBoundary, Token));
        Assert.Equal(AuthorizationDrainState.CleanupUnknown,
            await board.AggregateAsync(other, StaleBoundary, Token));
    }

    [Fact]
    public async Task RepublishUpsertsTheInstanceRow()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var board = new SqliteProjectDrainStateBoard(fixture.Connections);
        var project = Guid.NewGuid();
        var instance = Guid.NewGuid();
        await board.PublishAsync(project, instance, AuthorizationDrainState.Draining, Now, Token);
        await board.PublishAsync(project, instance, AuthorizationDrainState.CleanupUnknown, Now, Token);
        Assert.Equal(AuthorizationDrainState.CleanupUnknown,
            await board.AggregateAsync(project, StaleBoundary, Token));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM project_drain_states;"));
    }

    [Fact]
    public async Task PublishingIdleDeletesTheRow()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var board = new SqliteProjectDrainStateBoard(fixture.Connections);
        var project = Guid.NewGuid();
        var instance = Guid.NewGuid();
        await board.PublishAsync(project, instance, AuthorizationDrainState.Draining, Now, Token);
        await board.PublishAsync(project, instance, AuthorizationDrainState.Idle, Now, Token);
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM project_drain_states;"));
        Assert.Equal(AuthorizationDrainState.Idle,
            await board.AggregateAsync(project, StaleBoundary, Token));
    }

    [Fact]
    public async Task ClearInstanceRemovesOnlyOwnRows()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var board = new SqliteProjectDrainStateBoard(fixture.Connections);
        var project = Guid.NewGuid();
        var own = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        await board.PublishAsync(project, own, AuthorizationDrainState.Draining, Now, Token);
        await board.PublishAsync(project, foreign, AuthorizationDrainState.CleanupUnknown, Now, Token);
        await board.ClearInstanceAsync(own, Token);
        Assert.Equal(AuthorizationDrainState.CleanupUnknown,
            await board.AggregateAsync(project, StaleBoundary, Token));
        await board.ClearInstanceAsync(foreign, Token);
        Assert.Equal(AuthorizationDrainState.Idle,
            await board.AggregateAsync(project, StaleBoundary, Token));
    }

    [Fact]
    public async Task StaleNonIdleRowAggregatesAsCleanupUnknown()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var board = new SqliteProjectDrainStateBoard(fixture.Connections);
        var project = Guid.NewGuid();
        var instance = Guid.NewGuid();
        var staleAt = Now - ProjectAuthorizationMonitor.BoardStaleWindow - TimeSpan.FromSeconds(1);
        await board.PublishAsync(project, instance, AuthorizationDrainState.Draining, staleAt, Token);
        Assert.Equal(AuthorizationDrainState.CleanupUnknown,
            await board.AggregateAsync(project, StaleBoundary, Token));
    }

    [Fact]
    public async Task AggregateReapsStaleRowsAfterFolding()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var board = new SqliteProjectDrainStateBoard(fixture.Connections);
        var project = Guid.NewGuid();
        var staleAt = Now - ProjectAuthorizationMonitor.BoardStaleWindow - TimeSpan.FromSeconds(1);
        await board.PublishAsync(project, Guid.NewGuid(), AuthorizationDrainState.Draining, staleAt, Token);

        Assert.Equal(AuthorizationDrainState.CleanupUnknown,
            await board.AggregateAsync(project, StaleBoundary, Token));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM project_drain_states;"));
        Assert.Equal(AuthorizationDrainState.Idle,
            await board.AggregateAsync(project, StaleBoundary, Token));
    }

    [Fact]
    public async Task AggregateReapLeavesOtherProjectsUntouched()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var board = new SqliteProjectDrainStateBoard(fixture.Connections);
        var own = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        var staleAt = Now - ProjectAuthorizationMonitor.BoardStaleWindow - TimeSpan.FromSeconds(1);
        await board.PublishAsync(own, Guid.NewGuid(), AuthorizationDrainState.Draining, staleAt, Token);
        await board.PublishAsync(foreign, Guid.NewGuid(), AuthorizationDrainState.Draining, staleAt, Token);

        Assert.Equal(AuthorizationDrainState.CleanupUnknown,
            await board.AggregateAsync(own, StaleBoundary, Token));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM project_drain_states;"));
        Assert.Equal(AuthorizationDrainState.CleanupUnknown,
            await board.AggregateAsync(foreign, StaleBoundary, Token));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM project_drain_states;"));
    }

    [Fact]
    public async Task AggregateOfOnlyFreshRowsDeletesNothing()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var board = new SqliteProjectDrainStateBoard(fixture.Connections);
        var project = Guid.NewGuid();
        var staleAt = Now - ProjectAuthorizationMonitor.BoardStaleWindow - TimeSpan.FromSeconds(1);
        await board.PublishAsync(project, Guid.NewGuid(), AuthorizationDrainState.Draining, Now, Token);
        await board.PublishAsync(Guid.NewGuid(), Guid.NewGuid(), AuthorizationDrainState.Draining, staleAt, Token);

        Assert.Equal(AuthorizationDrainState.Draining,
            await board.AggregateAsync(project, StaleBoundary, Token));
        Assert.Equal(2, await fixture.CountAsync(
            "SELECT COUNT(*) FROM project_drain_states;"));
    }

    [Fact]
    public async Task ReapStaleDeletesAcrossProjectsAndReportsCount()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var board = new SqliteProjectDrainStateBoard(fixture.Connections);
        var project = Guid.NewGuid();
        var staleAt = Now - ProjectAuthorizationMonitor.BoardStaleWindow - TimeSpan.FromSeconds(1);
        await board.PublishAsync(project, Guid.NewGuid(), AuthorizationDrainState.Draining, staleAt, Token);
        await board.PublishAsync(Guid.NewGuid(), Guid.NewGuid(), AuthorizationDrainState.CleanupUnknown, staleAt, Token);
        await board.PublishAsync(project, Guid.NewGuid(), AuthorizationDrainState.Draining, Now, Token);

        Assert.Equal(2, await board.ReapStaleAsync(StaleBoundary, Token));
        Assert.Equal(0, await board.ReapStaleAsync(StaleBoundary, Token));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM project_drain_states;"));
        Assert.Equal(AuthorizationDrainState.Draining,
            await board.AggregateAsync(project, StaleBoundary, Token));
    }

    [Fact]
    public async Task WorstStateWinsAcrossInstances()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var board = new SqliteProjectDrainStateBoard(fixture.Connections);
        var project = Guid.NewGuid();
        var staleAt = Now - ProjectAuthorizationMonitor.BoardStaleWindow - TimeSpan.FromSeconds(1);
        await board.PublishAsync(project, Guid.NewGuid(), AuthorizationDrainState.Draining, Now, Token);
        await board.PublishAsync(project, Guid.NewGuid(), AuthorizationDrainState.Draining, staleAt, Token);
        Assert.Equal(AuthorizationDrainState.CleanupUnknown,
            await board.AggregateAsync(project, StaleBoundary, Token));
        await board.PublishAsync(project, Guid.NewGuid(), AuthorizationDrainState.CleanupUnknown, Now, Token);
        Assert.Equal(AuthorizationDrainState.CleanupUnknown,
            await board.AggregateAsync(project, StaleBoundary, Token));
    }
}
