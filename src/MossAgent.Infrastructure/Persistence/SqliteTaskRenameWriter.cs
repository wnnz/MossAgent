using Microsoft.Data.Sqlite;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

internal sealed class SqliteTaskRenameWriter(SqliteConnectionFactory connections)
{
    public async Task<AgentTask?> RenameAsync(AgentTask expectedTask, string title, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedTask);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE agent_tasks SET title=$title,updated_at=$updated
            WHERE id=$task AND project_id IS $project AND title=$expected
              AND status NOT IN ($running,$approval)
              AND NOT EXISTS (SELECT 1 FROM archived_agent_tasks archived WHERE archived.task_id=$task);
            """;
        command.Parameters.AddWithValue("$task", expectedTask.Id.ToString());
        command.Parameters.AddWithValue("$project", (object?)expectedTask.ProjectId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$expected", expectedTask.Title);
        command.Parameters.AddWithValue("$title", title.Trim());
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$running", (int)AgentTaskStatus.Running);
        command.Parameters.AddWithValue("$approval", (int)AgentTaskStatus.WaitingForApproval);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0) return null;
        var updated = await ReadTaskAsync(connection, transaction, expectedTask.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    private static async Task<AgentTask> ReadTaskAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid taskId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT task.id,task.project_id,task.title,task.approval_policy,task.status,
                   task.created_at,task.updated_at,task.worktree_path,
                   pinned.task_id IS NOT NULL,failure.reason
            FROM agent_tasks task
            LEFT JOIN pinned_agent_tasks pinned ON pinned.task_id=task.id
            LEFT JOIN agent_task_failures failure ON failure.task_id=task.id
            WHERE task.id=$task;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Renamed task was not returned.");
        return SqliteWorkspaceTaskReader.Read(reader);
    }
}
