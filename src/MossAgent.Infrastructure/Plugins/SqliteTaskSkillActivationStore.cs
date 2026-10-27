using System.Globalization;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>SQLite store for per-task skill activation snapshots. A save deletes the task's prior
/// rows and inserts the new set inside one transaction, so the stored snapshot always equals the
/// last activation exactly; rows cascade away with their task.</summary>
public sealed class SqliteTaskSkillActivationStore(SqliteConnectionFactory connections) : ITaskSkillActivationStore
{
    private const string Columns =
        "id,task_id,plugin_id,plugin_name,plugin_version,content_hash,component_path,skill_text,activated_at";

    public async Task SaveForTaskAsync(Guid taskId, IReadOnlyList<TaskSkillActivation> rows,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        await using var clear = connection.CreateCommand();
        clear.Transaction = transaction;
        clear.CommandText = "DELETE FROM task_skill_activations WHERE task_id=$task;";
        clear.Parameters.AddWithValue("$task", taskId.ToString());
        await clear.ExecuteNonQueryAsync(cancellationToken);
        foreach (var row in rows)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"INSERT INTO task_skill_activations({Columns}) "
                + "VALUES($id,$task,$plugin,$name,$version,$hash,$path,$text,$at);";
            AddParameters(command, taskId, row);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaskSkillActivation>> ListForTaskAsync(Guid taskId,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM task_skill_activations "
            + "WHERE task_id=$task ORDER BY plugin_name,component_path;";
        command.Parameters.AddWithValue("$task", taskId.ToString());
        var rows = new List<TaskSkillActivation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(Map(reader));
        return rows;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddParameters(SqliteCommand command, Guid taskId, TaskSkillActivation row)
    {
        command.Parameters.AddWithValue("$id", row.Id.ToString());
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$plugin", row.PluginId.ToString());
        command.Parameters.AddWithValue("$name", row.PluginName);
        command.Parameters.AddWithValue("$version", row.PluginVersion);
        command.Parameters.AddWithValue("$hash", row.ContentHash);
        command.Parameters.AddWithValue("$path", row.ComponentPath);
        command.Parameters.AddWithValue("$text", row.SkillText);
        command.Parameters.AddWithValue("$at", row.ActivatedAt.ToString("O"));
    }

    private static TaskSkillActivation Map(SqliteDataReader reader) =>
        new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), Guid.Parse(reader.GetString(2)),
            reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
            reader.GetString(7), DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture));
}
