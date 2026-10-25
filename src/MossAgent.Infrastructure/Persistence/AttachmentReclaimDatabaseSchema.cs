namespace MossAgent.Infrastructure.Persistence;

internal static class AttachmentReclaimDatabaseSchema
{
    public const string VersionThirtyThree = """
        CREATE TABLE IF NOT EXISTS attachment_reclaim_intents (
            path TEXT PRIMARY KEY CHECK(length(path) BETWEEN 1 AND 32768),
            byte_count INTEGER NOT NULL CHECK(typeof(byte_count)='integer' AND byte_count>=0),
            stage INTEGER NOT NULL CHECK(stage IN (0,1,2,3)),
            error_code TEXT NULL CHECK(error_code IS NULL OR length(error_code) BETWEEN 1 AND 64),
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );
        INSERT INTO schema_versions(version,applied_at) VALUES(33,datetime('now'));
        """;
}
