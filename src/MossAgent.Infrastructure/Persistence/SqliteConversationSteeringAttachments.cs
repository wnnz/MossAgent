using Microsoft.Data.Sqlite;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;

namespace MossAgent.Infrastructure.Persistence;

internal static class SqliteConversationSteeringAttachments
{
    public static async Task WriteAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid taskId, Guid inputId, IReadOnlyList<ConversationDraftAttachmentMetadata> items, CancellationToken token)
    {
        if (items.Count == 0) return;
        var attachments = items.Select((item, index) => new ConversationAttachment(item.Attachment.Id, taskId, inputId,
            item.Attachment.DisplayPath, item.Attachment.ByteCount, item.Sha256, item.Attachment.MediaType)).ToArray();
        ConversationAttachmentValidation.Validate(taskId, inputId, attachments);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        for (var index = 0; index < attachments.Length; index++)
        {
            var attachment = attachments[index];
            command.CommandText = """
                INSERT INTO conversation_steering_input_attachments(input_id,task_id,ordinal,display_path,byte_count,sha256,media_type,attachment_id)
                VALUES($input,$task,$ordinal,$display,$bytes,$hash,$media,$id);
                """;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$input", inputId.ToString());
            command.Parameters.AddWithValue("$task", taskId.ToString());
            command.Parameters.AddWithValue("$ordinal", index);
            command.Parameters.AddWithValue("$display", attachment.DisplayPath);
            command.Parameters.AddWithValue("$bytes", attachment.ByteCount);
            command.Parameters.AddWithValue("$hash", attachment.Sha256);
            command.Parameters.AddWithValue("$media", attachment.MediaType);
            command.Parameters.AddWithValue("$id", attachment.Id.ToString());
            await command.ExecuteNonQueryAsync(token);
        }
    }

    public static async Task<ConversationSteeringInput[]> RestoreAsync(SqliteConnection connection,
        SqliteTransaction? transaction, Guid taskId, IReadOnlyList<ConversationSteeringInput> inputs, CancellationToken token)
    {
        if (inputs.Count == 0) return [];
        var ids = string.Join(",", inputs.Select((_, index) => "$a" + index));
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT input_id,attachment_id,display_path,byte_count,sha256,media_type
            FROM conversation_steering_input_attachments WHERE task_id=$task AND input_id IN ({ids})
            ORDER BY input_id,ordinal;
            """;
        command.Parameters.AddWithValue("$task", taskId.ToString());
        for (var index = 0; index < inputs.Count; index++)
            command.Parameters.AddWithValue("$a" + index, inputs[index].Id.ToString());
        await using var reader = await command.ExecuteReaderAsync(token);
        var attachments = new Dictionary<Guid, List<ConversationAttachment>>();
        while (await reader.ReadAsync(token))
        {
            var inputId = Guid.Parse(reader.GetString(0));
            if (!attachments.TryGetValue(inputId, out var items)) attachments.Add(inputId, items = []);
            items.Add(new ConversationAttachment(Guid.Parse(reader.GetString(1)), taskId, inputId,
                reader.GetString(2), reader.GetInt64(3), reader.GetString(4), reader.GetString(5)));
        }
        return inputs.Select(input => attachments.TryGetValue(input.Id, out var items)
            ? input with { Attachments = items.ToArray() } : input).ToArray();
    }
}
