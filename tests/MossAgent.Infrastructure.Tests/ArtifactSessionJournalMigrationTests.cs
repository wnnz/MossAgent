using Microsoft.Data.Sqlite;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ArtifactSessionJournalMigrationTests
{
    [Fact]
    public async Task FreshDatabaseCreatesSessionJournalAtVersionThirtySeven()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();

        Assert.Equal(37, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='artifact_session_records';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM artifact_session_records;"));
        var invalid = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync("""
            INSERT INTO artifact_session_records(task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at)
            VALUES('x','f','p',0,9,NULL,NULL,'now','now');
            """));
        Assert.Equal(19, invalid.SqliteErrorCode);
        var oversized = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            "INSERT INTO artifact_session_records(task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at) "
            + "VALUES('" + Guid.NewGuid().ToString("N") + "','f','p',0,0,NULL,'" + new string('e', 65) + "','now','now');"));
        Assert.Equal(19, oversized.SqliteErrorCode);
        var negative = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            "INSERT INTO artifact_session_records(task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at) "
            + "VALUES('" + Guid.NewGuid().ToString("N") + "','f','p',1,0,-1,NULL,'now','now');"));
        Assert.Equal(19, negative.SqliteErrorCode);
    }

    [Fact]
    public async Task FailedThirtySevenMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE artifact_session_records;
            DELETE FROM schema_versions WHERE version>=37;
            CREATE TRIGGER reject_version_thirty_seven BEFORE INSERT ON schema_versions
            WHEN NEW.version=37 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));

        Assert.Equal(36, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='artifact_session_records';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_thirty_seven;");
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(37, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=37;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task ReinitializePreservesJournalRows()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var paths = new AppDataPaths(fixture.Project.PrimaryDirectory);
        var journal = new SqliteArtifactSessionJournal(fixture.Connections, new TaskArtifactPaths(paths));
        var path = Path.Combine(paths.ArtifactsPath, fixture.Task.Id.ToString("N"), "seed-1.txt");
        var id = await journal.RecordAsync(fixture.Task.Id, "seed-1.txt", path,
            ArtifactSessionKind.Artifact, null, fixture.Token);

        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(37, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        var record = Assert.Single(await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(id, record.Id);
        Assert.Equal(ArtifactSessionStage.Recorded, record.Stage);
    }
}
