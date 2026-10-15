using Microsoft.Data.Sqlite;
using MossAgent.Application.Agent;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

internal sealed class SqliteWorkspaceWriter(SqliteConnectionFactory connections)
{
    public async Task SaveAsync(AgentTask task, IReadOnlyList<ConversationMessage> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(messages);
        var batch = messages.ToArray();
        if (batch.Any(message => message.TaskId != task.Id))
            throw new ArgumentException("Every message must belong to the committed task.", nameof(messages));
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await WriteTaskAsync(connection, transaction, task, cancellationToken);
        await WriteFailureAsync(connection, transaction, task, cancellationToken);
        foreach (var message in batch)
            await WriteMessageAsync(connection, transaction, message, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AppendAsync(ConversationMessage message, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await WriteMessageAsync(connection, null, message, cancellationToken);
    }

    private static async Task WriteTaskAsync(SqliteConnection connection, SqliteTransaction transaction,
        AgentTask task, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO agent_tasks(id,project_id,title,approval_policy,status,created_at,updated_at,worktree_path)
            VALUES($id,$project,$title,$approval,$status,$created,$updated,$worktree)
            ON CONFLICT(id) DO UPDATE SET title=$title,approval_policy=$approval,status=$status,
            updated_at=$updated,worktree_path=$worktree
            WHERE agent_tasks.project_id IS $project;
            """;
        command.Parameters.AddWithValue("$id", task.Id.ToString());
        command.Parameters.AddWithValue("$project", (object?)task.ProjectId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", task.Title);
        command.Parameters.AddWithValue("$approval", (int)task.ApprovalPolicy);
        command.Parameters.AddWithValue("$status", (int)task.Status);
        command.Parameters.AddWithValue("$created", task.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated", task.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$worktree", (object?)task.WorktreePath ?? DBNull.Value);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("A task cannot change its project scope.");
    }

    private static async Task WriteFailureAsync(SqliteConnection connection, SqliteTransaction transaction,
        AgentTask task, CancellationToken cancellationToken)
    {
        var reason = TaskFailureClassifier.Normalize(task.Status, task.FailureReason);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = reason is null
            ? "DELETE FROM agent_task_failures WHERE task_id=$task;"
            : """
              INSERT INTO agent_task_failures(task_id,reason) VALUES($task,$reason)
              ON CONFLICT(task_id) DO UPDATE SET reason=$reason;
              """;
        command.Parameters.AddWithValue("$task", task.Id.ToString());
        if (reason is not null) command.Parameters.AddWithValue("$reason", (int)reason.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task WriteMessageAsync(SqliteConnection connection, SqliteTransaction? transaction,
        ConversationMessage message, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO messages(id,task_id,role,content,created_at,tool_call_id)
            VALUES($id,$task,$role,$content,$created,$tool);
            """;
        command.Parameters.AddWithValue("$id", message.Id.ToString());
        command.Parameters.AddWithValue("$task", message.TaskId.ToString());
        command.Parameters.AddWithValue("$role", (int)message.Role);
        command.Parameters.AddWithValue("$content", message.Content);
        command.Parameters.AddWithValue("$created", message.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$tool", (object?)message.ToolCallId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
