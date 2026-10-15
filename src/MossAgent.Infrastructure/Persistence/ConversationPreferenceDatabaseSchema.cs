namespace MossAgent.Infrastructure.Persistence;

internal static class ConversationPreferenceDatabaseSchema
{
    public const string VersionEleven = """
        CREATE TABLE conversation_model_preferences (
            task_id TEXT PRIMARY KEY REFERENCES agent_tasks(id) ON DELETE CASCADE,
            provider_id TEXT NOT NULL,
            model_profile_id TEXT NOT NULL,
            reasoning_effort TEXT CHECK(reasoning_effort IS NULL OR reasoning_effort IN
                ('none','minimal','low','medium','high','xhigh','max','ultra')),
            agent_profile_id TEXT,
            follow_agent_reasoning INTEGER NOT NULL DEFAULT 0 CHECK(follow_agent_reasoning IN (0,1)
                AND (follow_agent_reasoning=0 OR (agent_profile_id IS NOT NULL AND reasoning_effort IS NULL)))
        );
        INSERT INTO schema_versions(version, applied_at) VALUES(11, CURRENT_TIMESTAMP);
        """;
}
