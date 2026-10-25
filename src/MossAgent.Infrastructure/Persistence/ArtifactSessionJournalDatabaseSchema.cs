namespace MossAgent.Infrastructure.Persistence;

internal static class ArtifactSessionJournalDatabaseSchema
{
    public const string VersionThirtySeven = """
        CREATE TABLE IF NOT EXISTS artifact_session_records (
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
        CREATE INDEX IF NOT EXISTS ix_artifact_session_records_stage_updated
            ON artifact_session_records(stage,updated_at);
        INSERT INTO schema_versions(version,applied_at) VALUES(37,datetime('now'));
        """;

    public const string VersionThirtyEight = """
        CREATE TABLE artifact_session_records_v38 (
            id INTEGER PRIMARY KEY,
            task_id TEXT NOT NULL CHECK(length(task_id)=32),
            file_name TEXT NOT NULL CHECK(length(file_name) BETWEEN 1 AND 256),
            full_path TEXT NOT NULL CHECK(length(full_path) BETWEEN 1 AND 32768),
            kind INTEGER NOT NULL CHECK(kind IN (0,1,2)),
            stage INTEGER NOT NULL CHECK(stage IN (0,1,2,3,4,5,6)),
            byte_count INTEGER NULL CHECK(byte_count IS NULL OR (typeof(byte_count)='integer' AND byte_count>=0)),
            error_code TEXT NULL CHECK(error_code IS NULL OR length(error_code) BETWEEN 1 AND 64),
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            UNIQUE(task_id,file_name)
        );
        INSERT INTO artifact_session_records_v38(id,task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at)
            SELECT id,task_id,file_name,full_path,kind,stage,byte_count,error_code,created_at,updated_at
            FROM artifact_session_records;
        DROP TABLE artifact_session_records;
        ALTER TABLE artifact_session_records_v38 RENAME TO artifact_session_records;
        CREATE INDEX ix_artifact_session_records_stage_updated
            ON artifact_session_records(stage,updated_at);
        INSERT INTO schema_versions(version,applied_at) VALUES(38,datetime('now'));
        """;
}
