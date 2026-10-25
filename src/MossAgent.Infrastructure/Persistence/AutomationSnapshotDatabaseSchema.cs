namespace MossAgent.Infrastructure.Persistence;

internal static class AutomationSnapshotDatabaseSchema
{
    public const string VersionThirtyFour = """
        CREATE TABLE IF NOT EXISTS automation_snapshots (
            schedule_id TEXT PRIMARY KEY REFERENCES automation_schedules(id),
            payload TEXT NOT NULL CHECK(length(payload) BETWEEN 2 AND 262144)
        );
        INSERT INTO schema_versions(version,applied_at) VALUES(34,datetime('now'));
        """;
}
