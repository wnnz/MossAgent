using Microsoft.Data.Sqlite;
using MossAgent.Application.Conversations;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

internal static class SqliteConversationFollowupDispatcher
{
    public static async Task<ConversationFollowupDispatch?> DispatchAsync(SqliteConnection connection,
        PreparedConversationFollowup expected, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expectedTask = expected.Task;
        await using var transaction = connection.BeginTransaction();
        var queue = await SqliteConversationFollowupStorage.ReadAsync(connection, transaction, expectedTask.Id, cancellationToken);
        if (queue.Revision != expected.QueueRevision || queue.Items.FirstOrDefault() is not { } item || item.Id != expected.Followup.Id
            || !await SqliteConversationFollowupStorage.HasTaskScopeAsync(connection, transaction, queue, cancellationToken)
            || !await MatchesIdleTaskAsync(connection, transaction, expectedTask, cancellationToken)) return null;
        await SqliteConversationFollowupAuthorization.ValidateAsync(connection, transaction, expected, item, cancellationToken);
        if (!await SqliteConversationFollowupStorage.AdvanceAsync(connection, transaction, queue.TaskId, queue.Revision, cancellationToken))
            return null;
        var task = expectedTask with { Status = AgentTaskStatus.Running, ApprovalPolicy = item.ApprovalPolicy,
            UpdatedAt = now, FailureReason = null };
        var message = new ConversationMessage(item.Id, task.Id, MessageRole.User, item.Prompt.Trim(), now,
            Attachments: item.Attachments.Count == 0 ? null : item.Attachments.ToArray());
        await SqliteWorkspaceWriter.WriteTaskAsync(connection, transaction, task, cancellationToken);
        await SqliteWorkspaceWriter.WriteFailureAsync(connection, transaction, task, cancellationToken);
        await SqliteWorkspaceWriter.WriteMessageAsync(connection, transaction, message, cancellationToken);
        await using var remove = connection.CreateCommand();
        remove.Transaction = transaction;
        remove.CommandText = "DELETE FROM conversation_followups WHERE task_id=$task AND id=$id;";
        remove.Parameters.AddWithValue("$task", task.Id.ToString());
        remove.Parameters.AddWithValue("$id", item.Id.ToString());
        if (await remove.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("Follow-up claim failed.");
        await transaction.CommitAsync(cancellationToken);
        return new(task, item, message, checked(queue.Revision + 1));
    }

    private static async Task<bool> MatchesIdleTaskAsync(SqliteConnection connection, SqliteTransaction transaction,
        AgentTask task, CancellationToken cancellationToken)
    {
        if (task.Status is AgentTaskStatus.Running or AgentTaskStatus.WaitingForApproval) return false;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(*) FROM agent_tasks WHERE id=$task AND project_id IS $project AND worktree_path IS $worktree
              AND status=$status AND updated_at=$updated AND created_at=$created AND title=$title AND approval_policy=$policy;
            """;
        command.Parameters.AddWithValue("$task", task.Id.ToString());
        command.Parameters.AddWithValue("$project", (object?)task.ProjectId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$worktree", (object?)task.WorktreePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", (int)task.Status);
        command.Parameters.AddWithValue("$updated", task.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$created", task.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$title", task.Title);
        command.Parameters.AddWithValue("$policy", (int)task.ApprovalPolicy);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }
}
