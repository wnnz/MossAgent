namespace MossAgent.Infrastructure.Persistence;

internal static class ConversationFollowupDatabaseSchema
{
    public const string VersionTwentyThree = """
        CREATE TABLE IF NOT EXISTS conversation_followup_queues(
            task_id TEXT PRIMARY KEY REFERENCES agent_tasks(id) ON DELETE CASCADE,
            revision INTEGER NOT NULL CHECK(revision > 0)
        );
        CREATE TABLE IF NOT EXISTS conversation_followups(
            id TEXT PRIMARY KEY,
            task_id TEXT NOT NULL REFERENCES conversation_followup_queues(task_id) ON DELETE CASCADE,
            ordinal INTEGER NOT NULL CHECK(ordinal BETWEEN 0 AND 15),
            payload TEXT NOT NULL CHECK(length(payload) BETWEEN 1 AND 1048576),
            UNIQUE(task_id,ordinal)
        );
        INSERT INTO schema_versions(version,applied_at) VALUES(23,datetime('now'));
        """;
}
