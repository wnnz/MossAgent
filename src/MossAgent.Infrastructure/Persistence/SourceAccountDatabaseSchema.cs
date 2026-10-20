namespace MossAgent.Infrastructure.Persistence;

internal static class SourceAccountDatabaseSchema
{
    public const string VersionEighteen = """
        CREATE TABLE IF NOT EXISTS source_accounts(
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL CHECK(length(name) BETWEEN 1 AND 80),
            api_endpoint TEXT NOT NULL,
            access_token TEXT NOT NULL,
            proxy_id TEXT REFERENCES proxies(id) ON DELETE RESTRICT,
            is_enabled INTEGER NOT NULL CHECK(is_enabled IN (0,1)),
            revision INTEGER NOT NULL CHECK(revision > 0)
        );
        CREATE INDEX IF NOT EXISTS source_accounts_proxy ON source_accounts(proxy_id);
        INSERT INTO schema_versions(version,applied_at) VALUES(18,datetime('now'));
        """;
}
