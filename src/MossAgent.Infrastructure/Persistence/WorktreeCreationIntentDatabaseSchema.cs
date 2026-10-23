namespace MossAgent.Infrastructure.Persistence;

internal static class WorktreeCreationIntentDatabaseSchema
{
    public const string VersionTwentyNine = """
        CREATE TABLE IF NOT EXISTS worktree_creation_intents (
            task_id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
            repository TEXT NOT NULL,
            base_reference TEXT NULL,
            base_commit TEXT NULL,
            target TEXT NOT NULL,
            volume_id TEXT NULL,
            file_id TEXT NULL,
            state INTEGER NOT NULL CHECK(state IN (0,1)),
            created_at TEXT NOT NULL,
            claimed_at TEXT NULL
        );
        INSERT OR IGNORE INTO worktree_creation_intents(
            task_id,project_id,repository,base_reference,base_commit,
            target,volume_id,file_id,state,created_at,claimed_at)
        SELECT task.id,task.project_id,project.primary_directory,NULL,NULL,
               task.worktree_path,NULL,NULL,1,task.created_at,task.created_at
        FROM agent_tasks task
        INNER JOIN projects project ON project.id=task.project_id
        WHERE task.worktree_path IS NOT NULL;
        INSERT INTO schema_versions(version,applied_at) VALUES(29,datetime('now'));
        """;
}
