using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Attachments;

internal static class AttachmentSnapshotReferences
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static Task<bool> IsReferencedAsync(SqliteConnection connection, AttachmentReclaimCandidate candidate,
        CancellationToken cancellationToken) => candidate.Kind switch
    {
        AttachmentCandidateKind.Message => IsMessageReferencedAsync(connection, candidate, cancellationToken),
        AttachmentCandidateKind.Draft => IsDraftReferencedAsync(connection, candidate, cancellationToken),
        _ => Task.FromResult(false)
    };

    private static async Task<bool> IsMessageReferencedAsync(SqliteConnection connection,
        AttachmentReclaimCandidate candidate, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM conversation_attachments
            WHERE task_id=$task AND message_id=$message AND id=$attachment;
            """;
        command.Parameters.AddWithValue("$task", candidate.TaskId!.Value.ToString("D"));
        command.Parameters.AddWithValue("$message", candidate.ContainerId!.Value.ToString("D"));
        command.Parameters.AddWithValue("$attachment", candidate.AttachmentId!.Value.ToString("D"));
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 0) return true;
        if (await IsFollowupReferencedAsync(connection, candidate, cancellationToken)) return true;
        return await IsToolArtifactPathAsync(connection, candidate, cancellationToken);
    }

    private static async Task<bool> IsFollowupReferencedAsync(SqliteConnection connection,
        AttachmentReclaimCandidate candidate, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM conversation_followups WHERE task_id=$task;";
        command.Parameters.AddWithValue("$task", candidate.TaskId!.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = JsonSerializer.Deserialize<ConversationFollowup>(reader.GetString(0))
                ?? throw new InvalidDataException("Invalid follow-up payload.");
            if (item.Attachments.Any(attachment => attachment.TaskId == candidate.TaskId
                && attachment.MessageId == candidate.ContainerId && attachment.Id == candidate.AttachmentId))
                return true;
        }
        return false;
    }

    private static async Task<bool> IsToolArtifactPathAsync(SqliteConnection connection,
        AttachmentReclaimCandidate candidate, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT path FROM tool_artifacts WHERE task_id=$task;";
        command.Parameters.AddWithValue("$task", candidate.TaskId!.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string? normalized = null;
            try { normalized = Path.GetFullPath(reader.GetString(0)); }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }
            if (string.Equals(normalized, candidate.Path, PathComparison)) return true;
        }
        return false;
    }

    private static async Task<bool> IsDraftReferencedAsync(SqliteConnection connection,
        AttachmentReclaimCandidate candidate, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT scope_key FROM conversation_draft_attachments WHERE attachment_id=$attachment;";
        command.Parameters.AddWithValue("$attachment", candidate.AttachmentId!.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reader.GetString(0))));
            if (hash.Equals(candidate.ScopeHash, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
