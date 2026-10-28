namespace MossAgent.Infrastructure.Persistence;

/// <summary>Schema v51: per-task skill enable choices. A row records the last explicit choice
/// one conversation made for one skill component of one plugin; no row means the task defers
/// to the plugin-wide choice and then the default-enabled state. Rows cascade with their
/// agent_tasks parent, so deleting the conversation clears its choices. The plugin id carries
/// no foreign key by design — uninstall/reinstall swaps the plugin row id and stale rows stay
/// inert history, matching the task_skill_activations philosophy. CREATE TABLE IF NOT EXISTS
/// plus the version-row gate makes the migration replayable without touching stored rows.</summary>
internal static class TaskSkillOverrideDatabaseSchema
{
    public const string VersionFiftyOne = """
        CREATE TABLE IF NOT EXISTS task_skill_overrides(
            task_id TEXT NOT NULL REFERENCES agent_tasks(id) ON DELETE CASCADE,
            plugin_id TEXT NOT NULL,
            component_path TEXT NOT NULL,
            enabled INTEGER NOT NULL CHECK(enabled IN (0,1)),
            PRIMARY KEY(task_id,plugin_id,component_path)
        );
        INSERT INTO schema_versions(version,applied_at) VALUES(51,datetime('now'));
        """;
}
