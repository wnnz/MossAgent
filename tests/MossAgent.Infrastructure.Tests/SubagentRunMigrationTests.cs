using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SubagentRunMigrationTests
{
    [Fact]
    public async Task FreshDatabaseCreatesSubagentRunsAtVersionFortyOne()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        Assert.Equal(46, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='subagent_runs';"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='ix_subagent_runs_task';"));
        var missingTask = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync($$"""
            INSERT INTO subagent_runs(run_id,task_id,parent_call_id,profile_name,snapshot_json,status,error_code,created_at,updated_at)
            VALUES('{{Guid.NewGuid():D}}','{{Guid.NewGuid():D}}','call-1','review','{}','running',NULL,'now','now');
            """));
        Assert.Equal(19, missingTask.SqliteErrorCode);
        var shortRunId = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync($$"""
            INSERT INTO subagent_runs(run_id,task_id,parent_call_id,profile_name,snapshot_json,status,error_code,created_at,updated_at)
            VALUES('{{Guid.NewGuid():N}}','{{fixture.Task.Id}}','call-1','review','{}','running',NULL,'now','now');
            """));
        Assert.Equal(19, shortRunId.SqliteErrorCode);
        var badStatus = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync($$"""
            INSERT INTO subagent_runs(run_id,task_id,parent_call_id,profile_name,snapshot_json,status,error_code,created_at,updated_at)
            VALUES('{{Guid.NewGuid():D}}','{{fixture.Task.Id}}','call-1','review','{}','orphaned',NULL,'now','now');
            """));
        Assert.Equal(19, badStatus.SqliteErrorCode);
        var emptyCall = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync($$"""
            INSERT INTO subagent_runs(run_id,task_id,parent_call_id,profile_name,snapshot_json,status,error_code,created_at,updated_at)
            VALUES('{{Guid.NewGuid():D}}','{{fixture.Task.Id}}','','review','{}','running',NULL,'now','now');
            """));
        Assert.Equal(19, emptyCall.SqliteErrorCode);
        var oversizedSnapshot = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            "INSERT INTO subagent_runs(run_id,task_id,parent_call_id,profile_name,snapshot_json,status,error_code,created_at,updated_at) "
            + "VALUES('" + Guid.NewGuid().ToString("D") + "','" + fixture.Task.Id + "','call-1','review','{"
            + new string('x', 16385) + "}','running',NULL,'now','now');"));
        Assert.Equal(19, oversizedSnapshot.SqliteErrorCode);
        var oversizedError = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync($$"""
            INSERT INTO subagent_runs(run_id,task_id,parent_call_id,profile_name,snapshot_json,status,error_code,created_at,updated_at)
            VALUES('{{Guid.NewGuid():D}}','{{fixture.Task.Id}}','call-1','review','{}','failed','{{new string('e', 129)}}','now','now');
            """));
        Assert.Equal(19, oversizedError.SqliteErrorCode);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedFortyOneMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE subagent_runs;
            DELETE FROM schema_versions WHERE version=41;
            CREATE TRIGGER reject_version_forty_one BEFORE INSERT ON schema_versions
            WHEN NEW.version=41 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(45, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='subagent_runs';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_forty_one;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(46, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=41;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='subagent_runs';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task ReinitializePreservesJournaledRuns()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteSubagentRunJournal(fixture.Connections);
        var record = new SubagentRunRecord(Guid.NewGuid(), fixture.Task.Id, "call-1", "review",
            """{"profile":"review"}""", SubagentRunStatus.Running, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        Assert.True(await journal.BeginAsync(record, fixture.Token));

        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(46, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        var stored = Assert.Single(await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(record.RunId, stored.RunId);
        Assert.Equal(SubagentRunStatus.Running, stored.Status);
    }
}
