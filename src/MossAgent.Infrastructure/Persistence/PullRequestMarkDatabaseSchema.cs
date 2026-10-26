namespace MossAgent.Infrastructure.Persistence;

internal static class PullRequestMarkDatabaseSchema
{
    public const string VersionFortyFour = """
        CREATE TABLE IF NOT EXISTS pull_request_marks (
            account_id TEXT NOT NULL,
            owner TEXT NOT NULL CHECK(length(owner) BETWEEN 1 AND 100),
            repository TEXT NOT NULL CHECK(length(repository) BETWEEN 1 AND 100),
            number INTEGER NOT NULL CHECK(number > 0),
            head_revision TEXT NOT NULL CHECK(length(head_revision) BETWEEN 40 AND 64),
            viewed_at INTEGER NOT NULL,
            PRIMARY KEY (account_id, owner, repository, number)
        );
        INSERT INTO schema_versions(version,applied_at) VALUES(44,datetime('now'));
        """;
}
