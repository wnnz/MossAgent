namespace MossAgent.Infrastructure.Persistence;

/// <summary>Schema v50: per-component install-time payload hashes plus the widened
/// plugin_packages state check for the Drifted trust state. Each row pins the SHA-256 of one
/// manifest component's archive bytes; dispatch verifies on-disk payload against this baseline
/// and rows cascade with their plugin_packages parent, so uninstall clears the baseline and
/// reinstall under a new plugin id starts fresh. The state check cannot be altered in place,
/// so plugin_packages is rebuilt with the Drifted value admitted while component settings and
/// hash rows stay intact under foreign_keys=OFF; the rebuild is replay-safe because it copies
/// from and back into the same table. The copy lists columns explicitly instead of SELECT * so a
/// replay on a table widened by later versions does not fail on the column count; columns added
/// after v50 are re-created by their own migrations' column gates. The version row gates the
/// migration as usual.</summary>
internal static class PluginComponentHashDatabaseSchema
{
    public const string VersionFifty = """
        DROP TABLE IF EXISTS plugin_packages_v50;
        CREATE TABLE plugin_packages_v50(
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL CHECK(length(name) BETWEEN 1 AND 200),
            version TEXT NOT NULL CHECK(length(version) BETWEEN 1 AND 100),
            content_hash TEXT NOT NULL CHECK(length(content_hash)=64),
            install_path TEXT NOT NULL CHECK(length(install_path) BETWEEN 1 AND 300),
            state INTEGER NOT NULL CHECK(state IN (0,1,2,3)),
            source_path TEXT NOT NULL CHECK(length(source_path) BETWEEN 1 AND 1024),
            installed_at TEXT NOT NULL,
            components_json TEXT NOT NULL CHECK(length(components_json)<=8192),
            declared_mcp_json TEXT NOT NULL CHECK(length(declared_mcp_json)<=8192),
            overlay_source INTEGER NOT NULL CHECK(overlay_source IN (0,1,2)),
            UNIQUE(name,version)
        );
        INSERT INTO plugin_packages_v50(id,name,version,content_hash,install_path,state,source_path,
            installed_at,components_json,declared_mcp_json,overlay_source)
        SELECT id,name,version,content_hash,install_path,state,source_path,installed_at,components_json,
            declared_mcp_json,overlay_source FROM plugin_packages;
        DROP TABLE plugin_packages;
        ALTER TABLE plugin_packages_v50 RENAME TO plugin_packages;
        CREATE INDEX IF NOT EXISTS plugin_packages_by_hash ON plugin_packages(name,version,content_hash);
        CREATE TABLE IF NOT EXISTS plugin_component_hashes(
            plugin_id TEXT NOT NULL REFERENCES plugin_packages(id) ON DELETE CASCADE,
            component_path TEXT NOT NULL CHECK(length(component_path) BETWEEN 1 AND 260),
            sha256 TEXT NOT NULL CHECK(length(sha256) BETWEEN 1 AND 128),
            PRIMARY KEY(plugin_id,component_path)
        );
        INSERT INTO schema_versions(version,applied_at) VALUES(50,datetime('now'));
        """;
}
