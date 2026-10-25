namespace MossAgent.Infrastructure.Persistence;

internal static class ModelCapabilityFailureDatabaseSchema
{
    public const string VersionThirtyFive = """
        CREATE TABLE agent_task_failures_v35 (
            task_id TEXT PRIMARY KEY REFERENCES agent_tasks(id) ON DELETE CASCADE,
            reason INTEGER NOT NULL CHECK(reason IN (0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19))
        );
        INSERT INTO agent_task_failures_v35(task_id,reason) SELECT task_id,reason FROM agent_task_failures;
        DROP TABLE agent_task_failures;
        ALTER TABLE agent_task_failures_v35 RENAME TO agent_task_failures;
        INSERT INTO schema_versions(version,applied_at) VALUES(35,datetime('now'));
        """;
}
