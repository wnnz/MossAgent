namespace MossAgent.Infrastructure.Persistence;

internal static class DatabaseSchema
{
    public const string VersionOne = """
        PRAGMA journal_mode = WAL;
        CREATE TABLE IF NOT EXISTS schema_versions (
            version INTEGER PRIMARY KEY,
            applied_at TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS proxies (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            protocol INTEGER NOT NULL,
            host TEXT NOT NULL,
            port INTEGER NOT NULL,
            username TEXT NULL,
            password TEXT NULL,
            is_default INTEGER NOT NULL,
            is_enabled INTEGER NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_proxies_default
            ON proxies(is_default) WHERE is_default = 1;
        CREATE TABLE IF NOT EXISTS providers (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            protocol INTEGER NOT NULL,
            base_uri TEXT NOT NULL,
            api_key TEXT NOT NULL,
            proxy_id TEXT NULL REFERENCES proxies(id) ON DELETE SET NULL,
            is_default INTEGER NOT NULL,
            is_enabled INTEGER NOT NULL,
            headers_json TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_providers_default
            ON providers(is_default) WHERE is_default = 1;
        CREATE TABLE IF NOT EXISTS models (
            id TEXT PRIMARY KEY,
            provider_id TEXT NOT NULL REFERENCES providers(id) ON DELETE CASCADE,
            model_id TEXT NOT NULL,
            display_name TEXT NOT NULL,
            context_length INTEGER NOT NULL,
            maximum_output_tokens INTEGER NOT NULL,
            reasoning_effort TEXT NOT NULL,
            temperature REAL NULL,
            top_p REAL NULL,
            supports_images INTEGER NOT NULL,
            supports_files INTEGER NOT NULL,
            is_default INTEGER NOT NULL,
            is_enabled INTEGER NOT NULL,
            advanced_parameters_json TEXT NOT NULL,
            UNIQUE(provider_id, model_id)
        );
        CREATE TABLE IF NOT EXISTS projects (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            primary_directory TEXT NOT NULL,
            authorized_directories_json TEXT NOT NULL,
            created_at TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS agent_tasks (
            id TEXT PRIMARY KEY,
            project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
            title TEXT NOT NULL,
            approval_policy INTEGER NOT NULL,
            status INTEGER NOT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            worktree_path TEXT NULL
        );
        CREATE TABLE IF NOT EXISTS messages (
            id TEXT PRIMARY KEY,
            task_id TEXT NOT NULL REFERENCES agent_tasks(id) ON DELETE CASCADE,
            role INTEGER NOT NULL,
            content TEXT NOT NULL,
            created_at TEXT NOT NULL,
            tool_call_id TEXT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_messages_task_created
            ON messages(task_id, created_at);
        CREATE TABLE IF NOT EXISTS browser_configuration (
            singleton_id INTEGER PRIMARY KEY CHECK(singleton_id = 1),
            default_browser INTEGER NOT NULL,
            profile_preference INTEGER NOT NULL,
            profile_directory TEXT NULL,
            cdp_endpoint TEXT NULL,
            proxy_id TEXT NULL REFERENCES proxies(id) ON DELETE SET NULL
        );
        CREATE TABLE IF NOT EXISTS mcp_servers (
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
            is_enabled INTEGER NOT NULL
        );
        INSERT OR IGNORE INTO schema_versions(version, applied_at)
            VALUES(1, CURRENT_TIMESTAMP);
        """;

    public const string VersionTwo = """
        CREATE TABLE IF NOT EXISTS tool_executions (
            id TEXT PRIMARY KEY,
            task_id TEXT NOT NULL REFERENCES agent_tasks(id) ON DELETE CASCADE,
            call_id TEXT NOT NULL,
            tool_name TEXT NOT NULL,
            risk_level INTEGER NULL,
            approval_policy INTEGER NOT NULL,
            started_at TEXT NOT NULL,
            completed_at TEXT NULL,
            approval_granted INTEGER NULL,
            is_success INTEGER NULL,
            error_code TEXT NULL,
            output_length INTEGER NULL
        );
        CREATE INDEX IF NOT EXISTS ix_tool_executions_task_started
            ON tool_executions(task_id, started_at DESC);
        CREATE TABLE IF NOT EXISTS tool_artifacts (
            id TEXT PRIMARY KEY,
            execution_id TEXT NOT NULL REFERENCES tool_executions(id) ON DELETE CASCADE,
            task_id TEXT NOT NULL REFERENCES agent_tasks(id) ON DELETE CASCADE,
            name TEXT NOT NULL,
            path TEXT NOT NULL,
            media_type TEXT NOT NULL,
            length INTEGER NOT NULL,
            sha256 TEXT NOT NULL,
            created_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_tool_artifacts_task_created
            ON tool_artifacts(task_id, created_at DESC);
        INSERT OR IGNORE INTO schema_versions(version, applied_at)
            VALUES(2, CURRENT_TIMESTAMP);
        """;

    public const string VersionThree = """
        CREATE TABLE IF NOT EXISTS archived_agent_tasks (
            task_id TEXT PRIMARY KEY REFERENCES agent_tasks(id) ON DELETE CASCADE,
            archived_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_archived_agent_tasks_archived_at
            ON archived_agent_tasks(archived_at DESC);
        INSERT OR IGNORE INTO schema_versions(version, applied_at)
            VALUES(3, CURRENT_TIMESTAMP);
        """;
}
