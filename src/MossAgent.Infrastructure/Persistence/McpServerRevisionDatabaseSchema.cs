namespace MossAgent.Infrastructure.Persistence;

internal static class McpServerRevisionDatabaseSchema
{
    public const string VersionThirtySix = """
        CREATE TABLE mcp_servers_v36 (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL UNIQUE,
            transport INTEGER NOT NULL,
            command TEXT NULL,
            arguments_json TEXT NOT NULL,
            working_directory TEXT NULL,
            environment_json TEXT NOT NULL,
            url TEXT NULL,
            headers_json TEXT NOT NULL,
            proxy_id TEXT NULL REFERENCES proxies(id) ON DELETE SET NULL,
            is_enabled INTEGER NOT NULL,
            revision INTEGER NOT NULL DEFAULT 1 CHECK(typeof(revision)='integer' AND revision > 0)
        );
        INSERT INTO mcp_servers_v36(id,name,transport,command,arguments_json,working_directory,
            environment_json,url,headers_json,proxy_id,is_enabled)
            SELECT id,name,transport,command,arguments_json,working_directory,
            environment_json,url,headers_json,proxy_id,is_enabled FROM mcp_servers;
        DROP TABLE mcp_servers;
        ALTER TABLE mcp_servers_v36 RENAME TO mcp_servers;
        INSERT INTO schema_versions(version,applied_at) VALUES(36,datetime('now'));
        """;
}
