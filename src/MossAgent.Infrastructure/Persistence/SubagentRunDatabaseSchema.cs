namespace MossAgent.Infrastructure.Persistence;

internal static class SubagentRunDatabaseSchema
{
    public const string VersionThirtyTwo = """
        CREATE TABLE IF NOT EXISTS subagent_runs (
            id TEXT PRIMARY KEY,
            task_id TEXT NOT NULL,
            parent_run_id TEXT NOT NULL,
            parent_call_id TEXT NOT NULL,
            profile_id TEXT NOT NULL,
            agent_name TEXT NOT NULL,
            trigger_condition TEXT NOT NULL,
            system_prompt TEXT NOT NULL,
            provider_id TEXT NOT NULL,
            provider_name TEXT NOT NULL,
            provider_protocol INTEGER NOT NULL,
            model_profile_id TEXT NOT NULL,
            model_id TEXT NOT NULL,
            context_length INTEGER NOT NULL,
            maximum_output_tokens INTEGER NOT NULL,
            reasoning_effort TEXT NOT NULL,
            share_context INTEGER NOT NULL CHECK(share_context IN (0,1)),
            owner_instance_id TEXT NOT NULL,
            status INTEGER NOT NULL CHECK(status IN (0,1,2,3,4)),
            summary TEXT NULL,
            error_code TEXT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            completed_at TEXT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_subagent_runs_task_created
            ON subagent_runs(task_id,created_at DESC);
        CREATE INDEX IF NOT EXISTS ix_subagent_runs_parent_status
            ON subagent_runs(parent_run_id,status,updated_at);
        INSERT INTO schema_versions(version,applied_at) VALUES(32,datetime('now'));
        """;
}
