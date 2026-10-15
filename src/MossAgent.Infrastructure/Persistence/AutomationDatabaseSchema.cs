namespace MossAgent.Infrastructure.Persistence;

internal static class AutomationDatabaseSchema
{
    internal const string VersionNine = """
        CREATE TABLE IF NOT EXISTS automation_schedules (
            id TEXT PRIMARY KEY, payload TEXT NOT NULL, revision INTEGER NOT NULL,
            enabled INTEGER NOT NULL, next_run_at TEXT NOT NULL, archived INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS automation_runs (
            id TEXT PRIMARY KEY, schedule_id TEXT NOT NULL REFERENCES automation_schedules(id),
            task_id TEXT NOT NULL, started_at TEXT NOT NULL, finished_at TEXT,
            status INTEGER NOT NULL, manual INTEGER NOT NULL, error_code TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_automation_runs_schedule ON automation_runs(schedule_id,started_at);
        CREATE UNIQUE INDEX IF NOT EXISTS ix_automation_running ON automation_runs(schedule_id) WHERE status=0;
        INSERT OR IGNORE INTO schema_versions(version,applied_at) VALUES(9,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
        """;
}
