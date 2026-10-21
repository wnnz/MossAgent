namespace MossAgent.Infrastructure.Persistence;

internal static class ProjectAuthorizationDatabaseSchema
{
    public const string VersionTwentySix = """
        ALTER TABLE projects ADD COLUMN authorization_revision INTEGER NOT NULL DEFAULT 1
            CHECK(typeof(authorization_revision)='integer' AND authorization_revision > 0);
        CREATE TRIGGER project_authorization_changed AFTER UPDATE OF primary_directory,authorized_directories_json ON projects
        WHEN OLD.primary_directory IS NOT NEW.primary_directory OR OLD.authorized_directories_json IS NOT NEW.authorized_directories_json
        BEGIN
            UPDATE projects SET authorization_revision=OLD.authorization_revision+1 WHERE id=NEW.id;
        END;
        CREATE TABLE agent_task_failures_v26 (
            task_id TEXT PRIMARY KEY REFERENCES agent_tasks(id) ON DELETE CASCADE,
            reason INTEGER NOT NULL CHECK(reason IN (0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17))
        );
        INSERT INTO agent_task_failures_v26(task_id,reason) SELECT task_id,reason FROM agent_task_failures;
        DROP TABLE agent_task_failures;
        ALTER TABLE agent_task_failures_v26 RENAME TO agent_task_failures;
        INSERT INTO schema_versions(version,applied_at) VALUES(26,datetime('now'));
        """;
}
