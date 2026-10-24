namespace MossAgent.Infrastructure.Persistence;

internal static class WorktreeCleanupIntentDatabaseSchema
{
    public const string VersionThirtyOne = """
        CREATE TABLE worktree_cleanup_intents (
            task_id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
            repository TEXT NOT NULL,
            target TEXT NOT NULL,
            stage INTEGER NOT NULL DEFAULT 0 CHECK(stage IN (0,1,2,3,4,5)),
            error_code TEXT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_worktree_cleanup_intents_project_stage
            ON worktree_cleanup_intents(project_id,stage);
        INSERT INTO schema_versions(version,applied_at) VALUES(31,datetime('now'));
        """;
}
