using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;

namespace MossAgent.Infrastructure.Persistence;

internal static class SqliteConversationDraftAttachments
{
    public static async Task<IReadOnlyList<ConversationDraftAttachmentMetadata>> ReadAsync(SqliteConnection connection,
        SqliteTransaction transaction, ConversationDraftScope scope, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT attachment_id,source_path,display_path,byte_count,sha256,authorization_stamp,media_type
            FROM conversation_draft_attachments WHERE scope_key=$scope ORDER BY ordinal;
            """;
        command.Parameters.AddWithValue("$scope", scope.Key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<ConversationDraftAttachmentMetadata>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(scope, new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
                null, reader.GetInt64(3), reader.GetString(5), reader.GetString(6)), reader.GetString(4)));
        return result;
    }

    public static async Task ReplaceAsync(SqliteConnection connection, SqliteTransaction transaction,
        ConversationDraftScope scope, IReadOnlyList<ConversationDraftAttachmentMetadata> attachments, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM conversation_draft_attachments WHERE scope_key=$scope;";
        command.Parameters.AddWithValue("$scope", scope.Key);
        await command.ExecuteNonQueryAsync(cancellationToken);
        for (var index = 0; index < attachments.Count; index++)
        {
            var metadata = attachments[index];
            var attachment = metadata.Attachment;
            if (metadata.Scope != scope) throw new UnauthorizedAccessException();
            command.CommandText = """
                INSERT INTO conversation_draft_attachments(scope_key,attachment_id,ordinal,source_path,display_path,byte_count,sha256,authorization_stamp,media_type)
                VALUES($scope,$id,$ordinal,$source,$display,$bytes,$hash,$stamp,$media);
                """;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$scope", scope.Key);
            command.Parameters.AddWithValue("$id", attachment.Id.ToString("D"));
            command.Parameters.AddWithValue("$ordinal", index);
            command.Parameters.AddWithValue("$source", attachment.FullPath);
            command.Parameters.AddWithValue("$display", attachment.DisplayPath);
            command.Parameters.AddWithValue("$bytes", attachment.ByteCount);
            command.Parameters.AddWithValue("$hash", metadata.Sha256);
            command.Parameters.AddWithValue("$stamp", attachment.AuthorizationStamp);
            command.Parameters.AddWithValue("$media", attachment.MediaType);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
