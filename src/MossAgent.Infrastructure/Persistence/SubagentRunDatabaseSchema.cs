namespace MossAgent.Infrastructure.Persistence;

internal static class SubagentRunDatabaseSchema
{
    public const string VersionFortyOne = """
        CREATE TABLE IF NOT EXISTS subagent_runs (
            run_id TEXT PRIMARY KEY CHECK(length(run_id)=36),
            task_id TEXT NOT NULL REFERENCES agent_tasks(id) ON DELETE CASCADE,
            parent_call_id TEXT NOT NULL CHECK(length(parent_call_id) BETWEEN 1 AND 256),
            profile_name TEXT NOT NULL CHECK(length(profile_name) BETWEEN 1 AND 256),
            snapshot_json TEXT NOT NULL CHECK(length(snapshot_json) BETWEEN 2 AND 16384),
            status TEXT NOT NULL CHECK(status IN ('running','completed','failed','cancelled','interrupted')),
            error_code TEXT CHECK(error_code IS NULL OR length(error_code) BETWEEN 1 AND 128),
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_subagent_runs_task ON subagent_runs(task_id,created_at);
        INSERT INTO schema_versions(version,applied_at) VALUES(41,datetime('now'));
        """;
}
