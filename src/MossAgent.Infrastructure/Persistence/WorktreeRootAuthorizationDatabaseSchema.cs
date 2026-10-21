namespace MossAgent.Infrastructure.Persistence;

internal static class WorktreeRootAuthorizationDatabaseSchema
{
    public const string VersionTwentyEight = """
        CREATE TABLE IF NOT EXISTS worktree_root_authorizations (
            task_id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
            grant_id TEXT NOT NULL UNIQUE,
            path TEXT NOT NULL CHECK(length(path)>0 AND length(path)<=32768),
            volume_id TEXT NOT NULL CHECK(length(volume_id)>0 AND length(volume_id)<=1024),
            file_id TEXT NOT NULL CHECK(length(file_id)>0 AND length(file_id)<=1024)
        );
        INSERT INTO schema_versions(version,applied_at) VALUES(28,datetime('now'));
        """;
}
