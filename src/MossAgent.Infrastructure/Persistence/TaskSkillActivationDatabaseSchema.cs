namespace MossAgent.Infrastructure.Persistence;

/// <summary>Schema v47: per-task plugin skill activation snapshots. Each row records the exact
/// skill content one task dispatch injected into its system prompt, so the run is insulated from
/// later plugin disable/uninstall by construction. Rows are replaced wholesale on each activation
/// and cascade with their task. CREATE TABLE IF NOT EXISTS plus the version-row gate makes the
/// migration replayable without touching stored rows.</summary>
internal static class TaskSkillActivationDatabaseSchema
{
    public const string VersionFortySeven = """
        CREATE TABLE IF NOT EXISTS task_skill_activations(
            id TEXT PRIMARY KEY,
            task_id TEXT NOT NULL REFERENCES agent_tasks(id) ON DELETE CASCADE,
            plugin_id TEXT NOT NULL,
            plugin_name TEXT NOT NULL CHECK(length(plugin_name) BETWEEN 1 AND 200),
            plugin_version TEXT NOT NULL CHECK(length(plugin_version) BETWEEN 1 AND 100),
            content_hash TEXT NOT NULL CHECK(length(content_hash)=64),
            component_path TEXT NOT NULL CHECK(length(component_path) BETWEEN 1 AND 260),
            skill_text TEXT NOT NULL CHECK(length(skill_text)<=8192),
            activated_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS task_skill_activations_by_task ON task_skill_activations(task_id);
        INSERT INTO schema_versions(version,applied_at) VALUES(47,datetime('now'));
        """;
}
