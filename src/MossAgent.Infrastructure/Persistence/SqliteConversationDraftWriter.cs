using Microsoft.Data.Sqlite;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;

namespace MossAgent.Infrastructure.Persistence;

internal sealed class SqliteConversationDraftWriter(AppDataPaths? paths, IAttachmentImageDecoder? images = null,
    IAttachmentDocumentTextExtractor? documents = null)
{
    private readonly ConversationDraftAttachmentFiles? _files = paths is null ? null : new(paths, images, documents);
    internal const string ScopeExists = """
        (($task IS NULL AND ($project IS NULL OR EXISTS(SELECT 1 FROM projects WHERE id=$project)))
        OR ($task IS NOT NULL AND EXISTS(SELECT 1 FROM agent_tasks task
            WHERE task.id=$task AND task.project_id IS $project
            AND NOT EXISTS(SELECT 1 FROM archived_agent_tasks archived WHERE archived.task_id=task.id))))
        """;

    public void Validate(IReadOnlyList<ConversationDraftUpdate> updates)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var update in updates)
        {
            update.Scope.Validate();
            if (update.Text is null || update.Text.Length > ConversationDraft.MaximumTextLength
                || update.ExpectedRevision is < 0 or long.MaxValue || !keys.Add(update.Scope.Key))
                throw new ArgumentException("Invalid conversation draft update.");
            ConversationDraftAttachmentValidation.Validate(update.Scope, update.AttachmentItems);
            if (_files is null && update.AttachmentItems.Count != 0)
                throw new InvalidOperationException("Conversation draft attachment storage is not configured.");
        }
    }

    public async Task<bool> WriteAsync(SqliteConnection connection, SqliteTransaction transaction,
        IReadOnlyList<ConversationDraftUpdate> updates, CancellationToken cancellationToken)
    {
        foreach (var update in updates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
                INSERT INTO conversation_drafts(scope_key,project_id,task_id,text,revision)
                SELECT $scope,$project,$task,$text,$expected+1 WHERE {ScopeExists}
                AND ($expected=0 OR EXISTS(SELECT 1 FROM conversation_drafts WHERE scope_key=$scope AND revision=$expected))
                ON CONFLICT(scope_key) DO UPDATE SET text=$text,revision=conversation_drafts.revision+1
                WHERE conversation_drafts.revision=$expected
                AND conversation_drafts.project_id IS $project AND conversation_drafts.task_id IS $task;
                """;
            command.Parameters.AddWithValue("$scope", update.Scope.Key);
            command.Parameters.AddWithValue("$project", (object?)update.Scope.ProjectId?.ToString("D") ?? DBNull.Value);
            command.Parameters.AddWithValue("$task", (object?)update.Scope.TaskId?.ToString("D") ?? DBNull.Value);
            command.Parameters.AddWithValue("$text", update.Text);
            command.Parameters.AddWithValue("$expected", update.ExpectedRevision);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) return false;
            var metadata = _files is null ? [] : await _files.WriteAsync(update.Scope, update.AttachmentItems, cancellationToken);
            await SqliteConversationDraftAttachments.ReplaceAsync(connection, transaction, update.Scope, metadata, cancellationToken);
        }
        return true;
    }
}
