namespace MossAgent.Infrastructure.Persistence;

internal static class WorktreeCleanupIntentDatabaseSchema
{
    public const string VersionThirtyThree = """
        CREATE TABLE IF NOT EXISTS worktree_cleanup_intents (
            task_id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL,
            target_path TEXT NOT NULL UNIQUE,
            quarantine_path TEXT NOT NULL UNIQUE,
            stage INTEGER NOT NULL CHECK(stage IN (0,1,2)),
            state INTEGER NOT NULL CHECK(state IN (0,1,2,3)),
            owner_instance_id TEXT NOT NULL,
            error_code TEXT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_worktree_cleanup_intents_project_state
            ON worktree_cleanup_intents(project_id,state,updated_at);
        CREATE TABLE IF NOT EXISTS worktree_root_authorization_history (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            task_id TEXT NOT NULL,
            project_id TEXT NOT NULL,
            previous_grant_id TEXT NOT NULL,
            grant_id TEXT NOT NULL,
            path TEXT NOT NULL,
            previous_volume_id TEXT NOT NULL,
            previous_file_id TEXT NOT NULL,
            volume_id TEXT NOT NULL,
            file_id TEXT NOT NULL,
            project_revision INTEGER NOT NULL,
            created_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_worktree_root_authorization_history_task_created
            ON worktree_root_authorization_history(task_id,created_at DESC);
        INSERT INTO schema_versions(version,applied_at) VALUES(33,datetime('now'));
        """;
}
