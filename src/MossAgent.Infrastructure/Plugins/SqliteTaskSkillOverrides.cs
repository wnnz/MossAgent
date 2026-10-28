using Microsoft.Data.Sqlite;
using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>SQLite store for per-task skill enable choices. Writes upsert the
/// (task, plugin, component) row in a single statement; reads return every stored row for the
/// task. Rows belong to the agent_tasks parent and cascade with it, while the plugin id is a
/// bare text column — uninstall/reinstall swaps the plugin row id and stale rows stay inert
/// history rather than being deleted.</summary>
public sealed class SqliteTaskSkillOverrides(SqliteConnectionFactory connections) : ITaskSkillOverrides
{
    public async Task<IReadOnlyList<TaskSkillOverride>> ListForTaskAsync(Guid taskId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT plugin_id,component_path,enabled FROM task_skill_overrides "
            + "WHERE task_id=$task ORDER BY plugin_id,component_path;";
        command.Parameters.AddWithValue("$task", taskId.ToString());
        var rows = new List<TaskSkillOverride>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new TaskSkillOverride(taskId, Guid.Parse(reader.GetString(0)),
                reader.GetString(1), reader.GetInt64(2) != 0));
        return rows;
    }

    public async Task SetSkillEnabledAsync(Guid taskId, Guid pluginId, string componentPath,
        bool enabled, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO task_skill_overrides(task_id,plugin_id,component_path,enabled)
            VALUES($task,$plugin,$path,$enabled)
            ON CONFLICT(task_id,plugin_id,component_path)
            DO UPDATE SET enabled=$enabled;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$plugin", pluginId.ToString());
        command.Parameters.AddWithValue("$path", componentPath);
        command.Parameters.AddWithValue("$enabled", enabled ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearForTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM task_skill_overrides WHERE task_id=$task;";
        command.Parameters.AddWithValue("$task", taskId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
