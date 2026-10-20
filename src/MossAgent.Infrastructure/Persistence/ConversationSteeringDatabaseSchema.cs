namespace MossAgent.Infrastructure.Persistence;

internal static class ConversationSteeringDatabaseSchema
{
    public const string VersionTwentyFive = """
        CREATE TABLE IF NOT EXISTS conversation_steering_runs(
            run_id TEXT PRIMARY KEY,
            task_id TEXT NOT NULL REFERENCES agent_tasks(id) ON DELETE CASCADE,
            revision INTEGER NOT NULL CHECK(typeof(revision)='integer' AND revision>=0),
            project_id TEXT,
            worktree_path TEXT,
            approval_policy INTEGER NOT NULL,
            task_created_at TEXT NOT NULL,
            started_at TEXT NOT NULL,
            closed_at TEXT,
            UNIQUE(run_id,task_id)
        );
        CREATE UNIQUE INDEX IF NOT EXISTS one_open_steering_run ON conversation_steering_runs(task_id) WHERE closed_at IS NULL;
        CREATE TABLE IF NOT EXISTS conversation_steering_inputs(
            sequence INTEGER PRIMARY KEY AUTOINCREMENT,
            id TEXT NOT NULL UNIQUE,
            task_id TEXT NOT NULL,
            run_id TEXT NOT NULL,
            text TEXT NOT NULL CHECK(length(text)>0 AND length(text)<=32768),
            byte_count INTEGER NOT NULL CHECK(byte_count>0 AND byte_count<=262144),
            state INTEGER NOT NULL CHECK(state IN (0,1,2)),
            accepted_at TEXT NOT NULL,
            applied_at TEXT,
            interrupted_at TEXT,
            FOREIGN KEY(run_id,task_id) REFERENCES conversation_steering_runs(run_id,task_id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS steering_task_history ON conversation_steering_inputs(task_id,sequence DESC);
        CREATE INDEX IF NOT EXISTS steering_pending_inputs ON conversation_steering_inputs(run_id,sequence) WHERE state=0;
        INSERT INTO schema_versions(version,applied_at) VALUES(25,datetime('now'));
        """;
}
