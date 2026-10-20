namespace MossAgent.Infrastructure.Persistence;

internal static class SourceRepositoryDatabaseSchema
{
    public const string VersionNineteen = """
        CREATE TABLE IF NOT EXISTS source_repository_grants(
            id TEXT PRIMARY KEY,
            account_id TEXT NOT NULL REFERENCES source_accounts(id) ON DELETE CASCADE,
            account_revision INTEGER NOT NULL CHECK(account_revision > 0),
            owner TEXT NOT NULL CHECK(length(owner) BETWEEN 1 AND 100),
            repository TEXT NOT NULL CHECK(length(repository) BETWEEN 1 AND 100),
            UNIQUE(account_id,owner,repository)
        );
        CREATE TABLE IF NOT EXISTS source_repository_revocations(id TEXT PRIMARY KEY);
        CREATE TABLE IF NOT EXISTS source_review_sessions(
            task_id TEXT PRIMARY KEY REFERENCES agent_tasks(id) ON DELETE CASCADE,
            grant_id TEXT NOT NULL UNIQUE
        );
        INSERT INTO schema_versions(version,applied_at) VALUES(19,datetime('now'));
        """;
}
