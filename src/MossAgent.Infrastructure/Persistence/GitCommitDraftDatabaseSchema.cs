namespace MossAgent.Infrastructure.Persistence;

internal static class GitCommitDraftDatabaseSchema
{
    public const string VersionForty = """
        CREATE TABLE IF NOT EXISTS git_commit_drafts (
            task_id TEXT PRIMARY KEY REFERENCES agent_tasks(id) ON DELETE CASCADE,
            text TEXT NOT NULL CHECK(length(text) BETWEEN 1 AND 8192),
            revision INTEGER NOT NULL CHECK(revision > 0),
            updated_at TEXT NOT NULL
        );
        INSERT INTO schema_versions(version,applied_at) VALUES(40,datetime('now'));
        """;
}
