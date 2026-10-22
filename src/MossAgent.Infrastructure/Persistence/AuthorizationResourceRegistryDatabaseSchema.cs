namespace MossAgent.Infrastructure.Persistence;

internal static class AuthorizationResourceRegistryDatabaseSchema
{
    public const string VersionThirty = """
        CREATE TABLE IF NOT EXISTS authorization_resource_leases (
            lease_id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
            authorization_revision INTEGER NOT NULL CHECK(authorization_revision > 0),
            instance_id TEXT NOT NULL,
            registered_at TEXT NOT NULL,
            heartbeat_at TEXT NOT NULL,
            released_at TEXT NULL,
            resolution TEXT NULL CHECK(resolution IS NULL OR resolution IN ('released','abandoned')),
            resolved_by_instance_id TEXT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_authorization_resource_leases_active
            ON authorization_resource_leases(project_id,authorization_revision,released_at);
        INSERT INTO schema_versions(version,applied_at) VALUES(30,datetime('now'));
        """;
}
