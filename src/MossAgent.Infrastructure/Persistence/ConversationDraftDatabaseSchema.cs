namespace MossAgent.Infrastructure.Persistence;

internal static class ConversationDraftDatabaseSchema
{
    public const string VersionThirteen = """
        CREATE TABLE IF NOT EXISTS conversation_drafts (
            scope_key TEXT PRIMARY KEY,
            project_id TEXT NULL REFERENCES projects(id) ON DELETE CASCADE,
            task_id TEXT NULL REFERENCES agent_tasks(id) ON DELETE CASCADE,
            text TEXT NOT NULL CHECK(length(text) <= 262144),
            revision INTEGER NOT NULL CHECK(revision > 0),
            CHECK(scope_key = CASE
                WHEN task_id IS NOT NULL THEN 'task:' || task_id
                WHEN project_id IS NOT NULL THEN 'project:' || project_id
                ELSE 'projectless' END)
        );
        INSERT OR IGNORE INTO schema_versions(version,applied_at) VALUES(13,datetime('now'));
        """;
}
