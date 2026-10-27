namespace MossAgent.Infrastructure.Persistence;

/// <summary>Schema v49: per-skill-component enable choices for installed plugins. A row records
/// the last explicit choice for one component of one plugin; no row means the component keeps its
/// default enabled state. Rows cascade with their plugin_packages parent, so uninstall clears the
/// choices and reinstall under a new plugin id intentionally resets selection to defaults. Only
/// skill components are selectable today, so the table carries no kind column. CREATE TABLE IF
/// NOT EXISTS plus the version-row gate makes the migration replayable without touching stored
/// rows.</summary>
internal static class PluginComponentSettingsDatabaseSchema
{
    public const string VersionFortyNine = """
        CREATE TABLE IF NOT EXISTS plugin_component_settings(
            plugin_id TEXT NOT NULL REFERENCES plugin_packages(id) ON DELETE CASCADE,
            component_path TEXT NOT NULL CHECK(length(component_path) BETWEEN 1 AND 260),
            is_enabled INTEGER NOT NULL CHECK(is_enabled IN (0,1)),
            updated_at TEXT NOT NULL,
            PRIMARY KEY(plugin_id,component_path)
        );
        INSERT INTO schema_versions(version,applied_at) VALUES(49,datetime('now'));
        """;
}
