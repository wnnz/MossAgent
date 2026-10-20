using Microsoft.Data.Sqlite;
using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

internal static class SqliteConversationAttachments
{
    public static async Task WriteAsync(SqliteConnection connection, SqliteTransaction transaction,
        ConversationMessage message, CancellationToken cancellationToken)
    {
        if (message.Attachments is not { Count: > 0 } attachments) return;
        ConversationAttachmentValidation.Validate(message.TaskId, message.Id, attachments);
        if (message.Role != MessageRole.User) throw new InvalidDataException("Only user messages can own attachments.");
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM agent_tasks WHERE id=$task;";
        command.Parameters.AddWithValue("$task", message.TaskId.ToString());
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new UnauthorizedAccessException("Attachment task does not exist.");
        for (var index = 0; index < attachments.Count; index++)
        {
            var attachment = attachments[index];
            command.CommandText = """
                INSERT INTO conversation_attachments(id,task_id,message_id,ordinal,display_path,byte_count,sha256,media_type)
                VALUES($id,$task,$message,$ordinal,$display,$bytes,$hash,$media);
                """;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$id", attachment.Id.ToString());
            command.Parameters.AddWithValue("$task", attachment.TaskId.ToString());
            command.Parameters.AddWithValue("$message", attachment.MessageId.ToString());
            command.Parameters.AddWithValue("$ordinal", index);
            command.Parameters.AddWithValue("$display", attachment.DisplayPath);
            command.Parameters.AddWithValue("$bytes", attachment.ByteCount);
            command.Parameters.AddWithValue("$hash", attachment.Sha256);
            command.Parameters.AddWithValue("$media", attachment.MediaType);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public static async Task<IReadOnlyList<ConversationMessage>> RestoreAsync(SqliteConnection connection,
        SqliteTransaction transaction, Guid taskId, IReadOnlyList<ConversationMessage> messages, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id,message_id,display_path,byte_count,sha256,media_type FROM conversation_attachments
            WHERE task_id=$task ORDER BY message_id,ordinal;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var attachments = new Dictionary<Guid, List<ConversationAttachment>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var messageId = Guid.Parse(reader.GetString(1));
            if (!attachments.TryGetValue(messageId, out var items)) attachments.Add(messageId, items = []);
            items.Add(new ConversationAttachment(Guid.Parse(reader.GetString(0)), taskId, messageId,
                reader.GetString(2), reader.GetInt64(3), reader.GetString(4), reader.GetString(5)));
        }
        return messages.Select(message => attachments.TryGetValue(message.Id, out var items)
            ? message with { Attachments = items.ToArray() } : message).ToArray();
    }
}
