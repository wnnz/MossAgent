namespace MossAgent.Infrastructure.Persistence;

internal static class WorktreeCreationIntentDatabaseSchema
{
    public const string VersionThirtyOne = """
        CREATE TABLE IF NOT EXISTS worktree_creation_intents (
            task_id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL,
            repository_root TEXT NOT NULL,
            base_reference TEXT NULL,
            target_path TEXT NOT NULL UNIQUE,
            status INTEGER NOT NULL CHECK(status IN (0,1,2,3,4,5,6)),
            volume_id TEXT NULL,
            object_id TEXT NULL,
            error_code TEXT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_worktree_creation_intents_project_status
            ON worktree_creation_intents(project_id,status,updated_at);
        INSERT INTO schema_versions(version,applied_at) VALUES(31,datetime('now'));
        """;
}
