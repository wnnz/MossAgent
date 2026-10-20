namespace MossAgent.Infrastructure.Persistence;

internal static class ToolInterruptionDatabaseSchema
{
    public const string VersionTwentyFour = """
        CREATE TABLE IF NOT EXISTS tool_execution_interruptions(
            execution_id TEXT PRIMARY KEY REFERENCES tool_executions(id) ON DELETE CASCADE,
            interrupted_at TEXT NOT NULL
        );
        INSERT INTO schema_versions(version,applied_at) VALUES(24,datetime('now'));
        """;
}
