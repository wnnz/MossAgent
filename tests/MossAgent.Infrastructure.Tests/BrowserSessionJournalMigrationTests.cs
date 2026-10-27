using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class BrowserSessionJournalMigrationTests
{
    [Fact]
    public async Task FreshDatabaseCreatesBrowserJournalAtVersionFortyThree()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        Assert.Equal(46, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='browser_session_records';"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='browser_action_intents';"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='ix_browser_session_records_task';"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='ix_browser_action_intents_session';"));
        // The schema stores no browser secrets: no url, endpoint, path or text columns exist.
        Assert.Equal(0, await fixture.CountAsync("""
            SELECT COUNT(*) FROM pragma_table_info('browser_session_records')
            WHERE name LIKE '%url%' OR name LIKE '%endpoint%' OR name LIKE '%path%';
            """));
        Assert.Equal(0, await fixture.CountAsync("""
            SELECT COUNT(*) FROM pragma_table_info('browser_action_intents')
            WHERE name LIKE '%url%' OR name LIKE '%selector%' OR name LIKE '%text%';
            """));
        var missingTask = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync($$"""
            INSERT INTO browser_session_records(id,task_id,engine,profile_mode,state,created_at,updated_at)
            VALUES('{{Guid.NewGuid():D}}','{{Guid.NewGuid():D}}',0,0,0,'now','now');
            """));
        Assert.Equal(19, missingTask.SqliteErrorCode);
        var badEngine = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync($$"""
            INSERT INTO browser_session_records(id,task_id,engine,profile_mode,state,created_at,updated_at)
            VALUES('{{Guid.NewGuid():D}}','{{fixture.Task.Id}}',3,0,0,'now','now');
            """));
        Assert.Equal(19, badEngine.SqliteErrorCode);
        var badState = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync($$"""
            INSERT INTO browser_session_records(id,task_id,engine,profile_mode,state,created_at,updated_at)
            VALUES('{{Guid.NewGuid():D}}','{{fixture.Task.Id}}',0,0,4,'now','now');
            """));
        Assert.Equal(19, badState.SqliteErrorCode);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedFortyThreeMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE browser_action_intents;
            DROP TABLE browser_session_records;
            DELETE FROM schema_versions WHERE version=43;
            CREATE TRIGGER reject_version_forty_three BEFORE INSERT ON schema_versions
            WHEN NEW.version=43 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(45, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='browser_session_records';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_forty_three;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(46, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=43;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='browser_session_records';"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='browser_action_intents';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task ReinitializePreservesJournaledSessions()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteBrowserSessionJournal(fixture.Connections);
        Assert.True(await journal.RecordSessionAsync(
            fixture.Task.Id, BrowserEngine.Chrome, BrowserProfileMode.DailyProfile, fixture.Token));

        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(46, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        var stored = Assert.Single(await journal.ReadSessionsAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(BrowserEngine.Chrome, stored.Engine);
        Assert.Equal(BrowserSessionState.Attached, stored.State);
    }
}
