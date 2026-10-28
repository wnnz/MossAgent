using Microsoft.Data.Sqlite;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ArtifactSessionJournalMigrationTests
{
    [Fact]
    public async Task FreshDatabaseCreatesSessionJournalAtVersionThirtyEight()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();

        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='artifact_session_records';"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='ix_artifact_session_records_stage_updated';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM artifact_session_records;"));
        var invalid = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync("""
            INSERT INTO artifact_session_records(task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at)
            VALUES('x','f','p',0,9,NULL,NULL,'now','now');
            """));
        Assert.Equal(19, invalid.SqliteErrorCode);
        var unknownKind = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            "INSERT INTO artifact_session_records(task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at) "
            + "VALUES('" + Guid.NewGuid().ToString("N") + "','f','p',3,0,NULL,NULL,'now','now');"));
        Assert.Equal(19, unknownKind.SqliteErrorCode);
        var oversized = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            "INSERT INTO artifact_session_records(task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at) "
            + "VALUES('" + Guid.NewGuid().ToString("N") + "','f','p',0,0,NULL,'" + new string('e', 65) + "','now','now');"));
        Assert.Equal(19, oversized.SqliteErrorCode);
        var negative = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            "INSERT INTO artifact_session_records(task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at) "
            + "VALUES('" + Guid.NewGuid().ToString("N") + "','f','p',1,0,-1,NULL,'now','now');"));
        Assert.Equal(19, negative.SqliteErrorCode);
        await fixture.ExecuteAsync(
            "INSERT INTO artifact_session_records(task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at) "
            + "VALUES('" + Guid.NewGuid().ToString("N") + "','sdk.tmp','/tmp/sdk.tmp',2,0,NULL,NULL,'now','now');");
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM artifact_session_records WHERE kind=2;"));
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

        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=37;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=38;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task ThirtyEightRebuildPreservesPreMigrationRows()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await RestoreVersionThirtySevenAsync(fixture);
        var task = fixture.Task.Id.ToString("N");
        var other = Guid.NewGuid().ToString("N");
        await fixture.ExecuteAsync($"""
            INSERT INTO artifact_session_records(task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at)
            VALUES('{task}','keep-artifact.txt','/a/keep-artifact.txt',0,2,12,NULL,'2020-01-01','2020-01-02'),
                  ('{other}','keep-diff.tmp','/b/keep-diff.tmp',1,5,7,'cleanup_unknown','2020-01-03','2020-01-04');
            """);

        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(2, await fixture.CountAsync("SELECT COUNT(*) FROM artifact_session_records;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM artifact_session_records WHERE file_name='keep-artifact.txt' AND kind=0 AND stage=2 AND byte_count=12;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM artifact_session_records WHERE file_name='keep-diff.tmp' AND kind=1 AND stage=5 AND error_code='cleanup_unknown';"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='ix_artifact_session_records_stage_updated';"));
        await fixture.ExecuteAsync(
            "INSERT INTO artifact_session_records(task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at) "
            + "VALUES('" + other + "','sdk.download','/tmp/sdk.download',2,0,NULL,NULL,'now','now');");
        Assert.Equal(3, await fixture.CountAsync("SELECT COUNT(*) FROM artifact_session_records;"));
    }

    [Fact]
    public async Task FailedThirtyEightMigrationRollsBackAndRetriesCleanly()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await RestoreVersionThirtySevenAsync(fixture);
        var task = fixture.Task.Id.ToString("N");
        await fixture.ExecuteAsync($"""
            INSERT INTO artifact_session_records(task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at)
            VALUES('{task}','keep.txt','/a/keep.txt',1,0,NULL,NULL,'now','now');
            CREATE TRIGGER reject_version_thirty_eight BEFORE INSERT ON schema_versions
            WHEN NEW.version=38 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));

        Assert.Equal(37, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM artifact_session_records;"));
        var rejected = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            "INSERT INTO artifact_session_records(task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at) "
            + "VALUES('" + task + "','sdk.tmp','/tmp/sdk.tmp',2,0,NULL,NULL,'now','now');"));
        Assert.Equal(19, rejected.SqliteErrorCode);
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_thirty_eight;");
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM schema_versions WHERE version=38;"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM artifact_session_records;"));
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

        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        var record = Assert.Single(await journal.ReadTaskAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(id, record.Id);
        Assert.Equal(ArtifactSessionStage.Recorded, record.Stage);
    }

    private static Task RestoreVersionThirtySevenAsync(SqliteFailurePersistenceFixture fixture) =>
        fixture.ExecuteAsync("""
            DROP TABLE artifact_session_records;
            DELETE FROM schema_versions WHERE version>=38;
            CREATE TABLE artifact_session_records (
                id INTEGER PRIMARY KEY,
                task_id TEXT NOT NULL CHECK(length(task_id)=32),
                file_name TEXT NOT NULL CHECK(length(file_name) BETWEEN 1 AND 256),
                full_path TEXT NOT NULL CHECK(length(full_path) BETWEEN 1 AND 32768),
                kind INTEGER NOT NULL CHECK(kind IN (0,1)),
                stage INTEGER NOT NULL CHECK(stage IN (0,1,2,3,4,5,6)),
                byte_count INTEGER NULL CHECK(byte_count IS NULL OR (typeof(byte_count)='integer' AND byte_count>=0)),
                error_code TEXT NULL CHECK(error_code IS NULL OR length(error_code) BETWEEN 1 AND 64),
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                UNIQUE(task_id,file_name)
            );
            CREATE INDEX ix_artifact_session_records_stage_updated
                ON artifact_session_records(stage,updated_at);
            """);
}
