using System.Text.Json;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

internal static class SqliteConversationFollowupStorage
{
    internal const int MaximumPayloadBytes = 1048576;
    internal const int MaximumQueueBytes = 2097152;

    public static async Task<ConversationFollowupQueue> ReadAsync(SqliteConnection connection,
        SqliteTransaction transaction, Guid taskId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT revision FROM conversation_followup_queues WHERE task_id=$task;";
        command.Parameters.AddWithValue("$task", taskId.ToString());
        var revision = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        command.CommandText = "SELECT payload FROM conversation_followups WHERE task_id=$task ORDER BY ordinal LIMIT 17;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<ConversationFollowup>();
        var bytes = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            var payload = reader.GetString(0);
            var payloadBytes = System.Text.Encoding.UTF8.GetByteCount(payload);
            if (payloadBytes > MaximumPayloadBytes) throw new InvalidDataException("Follow-up payload exceeds its storage budget.");
            bytes = checked(bytes + payloadBytes);
            if (bytes > MaximumQueueBytes) throw new InvalidDataException("Follow-up queue exceeds its storage budget.");
            items.Add(JsonSerializer.Deserialize<ConversationFollowup>(payload) ?? throw new InvalidDataException("Invalid follow-up payload."));
        }
        var queue = new ConversationFollowupQueue(taskId, revision, items.ToArray());
        ConversationFollowupValidation.Validate(queue);
        return queue;
    }

    public static async Task<bool> AdvanceAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid taskId, long expectedRevision, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = expectedRevision == 0
            ? "INSERT INTO conversation_followup_queues(task_id,revision) VALUES($task,$next) ON CONFLICT(task_id) DO NOTHING;"
            : "UPDATE conversation_followup_queues SET revision=$next WHERE task_id=$task AND revision=$expected;";
        command.Parameters.AddWithValue("$task", taskId.ToString());
        command.Parameters.AddWithValue("$next", checked(expectedRevision + 1));
        command.Parameters.AddWithValue("$expected", expectedRevision);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public static async Task<bool> HasTaskScopeAsync(SqliteConnection connection, SqliteTransaction transaction,
        ConversationFollowupQueue queue, CancellationToken cancellationToken)
    {
        var item = queue.Items.FirstOrDefault();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(*) FROM agent_tasks WHERE id=$task
              AND ($empty=1 OR (project_id IS $project AND worktree_path IS $worktree))
              AND NOT EXISTS(SELECT 1 FROM archived_agent_tasks WHERE task_id=$task)
              AND NOT EXISTS(SELECT 1 FROM source_review_sessions WHERE task_id=$task);
            """;
        command.Parameters.AddWithValue("$task", queue.TaskId.ToString());
        command.Parameters.AddWithValue("$empty", item is null ? 1 : 0);
        command.Parameters.AddWithValue("$project", (object?)item?.ProjectId?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$worktree", (object?)item?.WorktreePath ?? DBNull.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    public static async Task ReplaceAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid taskId, IReadOnlyList<ConversationFollowup> items, string[] payloads, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM conversation_followups WHERE task_id=$task;";
        command.Parameters.AddWithValue("$task", taskId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
        command.CommandText = """
            INSERT INTO conversation_followups(id,task_id,ordinal,payload)
            SELECT $id,$task,$ordinal,$payload WHERE NOT EXISTS(SELECT 1 FROM messages WHERE id=$id);
            """;
        for (var index = 0; index < items.Count; index++)
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$task", taskId.ToString());
            command.Parameters.AddWithValue("$id", items[index].Id.ToString());
            command.Parameters.AddWithValue("$ordinal", index);
            command.Parameters.AddWithValue("$payload", payloads[index]);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("A committed message cannot return to the follow-up queue.");
        }
    }
}
