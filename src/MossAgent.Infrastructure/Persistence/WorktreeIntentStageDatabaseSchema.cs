namespace MossAgent.Infrastructure.Persistence;

internal static class WorktreeIntentStageDatabaseSchema
{
    public const string VersionThirty = """
        CREATE TABLE worktree_creation_intents_v30 (
            task_id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
            repository TEXT NOT NULL,
            base_reference TEXT NULL,
            base_commit TEXT NULL,
            target TEXT NOT NULL,
            volume_id TEXT NULL,
            file_id TEXT NULL,
            state INTEGER NOT NULL CHECK(state IN (0,1)),
            stage INTEGER NOT NULL DEFAULT 0 CHECK(stage IN (0,1,2,3,4,5,6)),
            error_code TEXT NULL,
            created_at TEXT NOT NULL,
            claimed_at TEXT NULL,
            updated_at TEXT NULL
        );
        INSERT INTO worktree_creation_intents_v30(
            task_id,project_id,repository,base_reference,base_commit,target,
            volume_id,file_id,state,stage,error_code,created_at,claimed_at,updated_at)
        SELECT task_id,project_id,repository,base_reference,base_commit,target,
               volume_id,file_id,state,
               CASE WHEN state=1 OR EXISTS(
                        SELECT 1 FROM agent_tasks WHERE agent_tasks.id=worktree_creation_intents.task_id)
                    THEN 4 ELSE 0 END,
               NULL,created_at,claimed_at,COALESCE(claimed_at,created_at)
        FROM worktree_creation_intents;
        DROP TABLE worktree_creation_intents;
        ALTER TABLE worktree_creation_intents_v30 RENAME TO worktree_creation_intents;
        CREATE INDEX IF NOT EXISTS ix_worktree_creation_intents_project_stage
            ON worktree_creation_intents(project_id,stage);
        INSERT INTO schema_versions(version,applied_at) VALUES(30,datetime('now'));
        """;
}
