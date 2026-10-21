namespace MossAgent.Infrastructure.Persistence;

internal static class RootObjectAuthorizationDatabaseSchema
{
    public const string VersionTwentySeven = """
        CREATE TABLE IF NOT EXISTS project_root_authorizations (
            project_id TEXT PRIMARY KEY REFERENCES projects(id) ON DELETE CASCADE,
            authorization_revision INTEGER NOT NULL CHECK(typeof(authorization_revision)='integer' AND authorization_revision > 0),
            roots_json TEXT NOT NULL CHECK(length(roots_json)<=262144 AND json_valid(roots_json) AND json_type(roots_json)='array')
        );
        INSERT INTO schema_versions(version,applied_at) VALUES(27,datetime('now'));
        """;
}
