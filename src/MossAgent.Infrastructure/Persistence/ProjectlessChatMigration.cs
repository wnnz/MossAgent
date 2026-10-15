using Microsoft.Data.Sqlite;

namespace MossAgent.Infrastructure.Persistence;

internal static class ProjectlessChatMigration
{
    public static async Task ApplyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_keys=OFF;";
        try
        {
            await foreignKeys.ExecuteNonQueryAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT COUNT(*) FROM schema_versions WHERE version=12;";
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 0) return;
            command.CommandText = """
                CREATE TABLE agent_tasks_projectless (
                    id TEXT PRIMARY KEY,
                    project_id TEXT NULL REFERENCES projects(id) ON DELETE CASCADE,
                    title TEXT NOT NULL,
                    approval_policy INTEGER NOT NULL,
                    status INTEGER NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    worktree_path TEXT NULL,
                    CHECK(project_id IS NOT NULL OR worktree_path IS NULL)
                );
                INSERT INTO agent_tasks_projectless
                    SELECT id,project_id,title,approval_policy,status,created_at,updated_at,worktree_path FROM agent_tasks;
                DROP TABLE agent_tasks;
                ALTER TABLE agent_tasks_projectless RENAME TO agent_tasks;
                INSERT INTO schema_versions(version,applied_at) VALUES(12,datetime('now'));
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.CommandText = "PRAGMA foreign_key_check;";
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (await reader.ReadAsync(cancellationToken))
                    throw new InvalidOperationException("Workspace migration failed foreign-key validation.");
            }
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            foreignKeys.CommandText = "PRAGMA foreign_keys=ON;";
            await foreignKeys.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
