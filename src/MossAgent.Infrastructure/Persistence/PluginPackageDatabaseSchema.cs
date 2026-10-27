namespace MossAgent.Infrastructure.Persistence;

/// <summary>Schema v46: installed local plugin packages. Rows are metadata only — the
/// extracted payload lives under the plugin root and is never stored. (name,version) is
/// unique so reinstalls are compare-and-swap on content_hash; the second index serves the
/// same-content lookup directly. CREATE TABLE IF NOT EXISTS plus the version-row gate makes
/// the migration replayable without touching stored rows.</summary>
internal static class PluginPackageDatabaseSchema
{
    public const string VersionFortySix = """
        CREATE TABLE IF NOT EXISTS plugin_packages(
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL CHECK(length(name) BETWEEN 1 AND 200),
            version TEXT NOT NULL CHECK(length(version) BETWEEN 1 AND 100),
            content_hash TEXT NOT NULL CHECK(length(content_hash)=64),
            install_path TEXT NOT NULL CHECK(length(install_path) BETWEEN 1 AND 300),
            state INTEGER NOT NULL CHECK(state IN (0,1,2)),
            source_path TEXT NOT NULL CHECK(length(source_path) BETWEEN 1 AND 1024),
            installed_at TEXT NOT NULL,
            components_json TEXT NOT NULL CHECK(length(components_json)<=8192),
            declared_mcp_json TEXT NOT NULL CHECK(length(declared_mcp_json)<=8192),
            overlay_source INTEGER NOT NULL CHECK(overlay_source IN (0,1,2)),
            UNIQUE(name,version)
        );
        CREATE INDEX IF NOT EXISTS plugin_packages_by_hash ON plugin_packages(name,version,content_hash);
        INSERT INTO schema_versions(version,applied_at) VALUES(46,datetime('now'));
        """;
}
