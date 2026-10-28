using Microsoft.Data.Sqlite;
using MossAgent.Application.Authorization;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ProjectDrainStateMigrationTests
{
    [Fact]
    public async Task FreshDatabaseCreatesProjectDrainStatesAtVersionFortyEight()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='project_drain_states';"));
        var idle = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO project_drain_states VALUES('{Guid.NewGuid()}','{Guid.NewGuid()}',0,'t');"));
        Assert.Equal(19, idle.SqliteErrorCode);
        var unknown = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            $"INSERT INTO project_drain_states VALUES('{Guid.NewGuid()}','{Guid.NewGuid()}',3,'t');"));
        Assert.Equal(19, unknown.SqliteErrorCode);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedFortyEightMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE project_drain_states;
            DELETE FROM schema_versions WHERE version=48;
            CREATE TRIGGER reject_version_forty_eight BEFORE INSERT ON schema_versions
            WHEN NEW.version=48 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='project_drain_states';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_forty_eight;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=48;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='project_drain_states';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FortyEightReplayAfterVersionRowLossKeepsBoardRows()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var board = new SqliteProjectDrainStateBoard(fixture.Connections);
        var project = Guid.NewGuid();
        var instance = Guid.NewGuid();
        await board.PublishAsync(project, instance, AuthorizationDrainState.CleanupUnknown,
            DateTimeOffset.UtcNow, fixture.Token);
        await fixture.ExecuteAsync("DELETE FROM schema_versions WHERE version=48;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(52, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM project_drain_states;"));
        Assert.Equal(AuthorizationDrainState.CleanupUnknown,
            await board.AggregateAsync(project, DateTimeOffset.MinValue, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
