namespace MossAgent.Infrastructure.Persistence;

/// <summary>Schema v48: cross-instance per-project drain-state board. Every process publishes
/// its own (project, instance) row while a revocation drain is in progress or its cleanup
/// outcome is unknown, so peer instances can aggregate the worst state and surface drained but
/// unconfirmed cleanup after a mid-drain crash. Rows only exist for non-Idle states; an
/// instance deletes its rows on clean exit, and readers treat rows untouched for longer than
/// the staleness window as CleanupUnknown. CREATE TABLE IF NOT EXISTS plus the version-row
/// gate makes the migration replayable without touching stored rows.</summary>
internal static class ProjectDrainStateDatabaseSchema
{
    public const string VersionFortyEight = """
        CREATE TABLE IF NOT EXISTS project_drain_states(
            project_id TEXT NOT NULL,
            instance_id TEXT NOT NULL,
            state INTEGER NOT NULL CHECK(state IN (1,2)),
            updated_at TEXT NOT NULL,
            PRIMARY KEY(project_id,instance_id)
        );
        CREATE INDEX IF NOT EXISTS project_drain_states_by_project ON project_drain_states(project_id);
        INSERT INTO schema_versions(version,applied_at) VALUES(48,datetime('now'));
        """;
}
