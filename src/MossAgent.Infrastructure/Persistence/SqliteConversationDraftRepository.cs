using Microsoft.Data.Sqlite;
using MossAgent.Application.Attachments;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteConversationDraftRepository(SqliteConnectionFactory connections,
    AppDataPaths? paths = null, TimeProvider? timeProvider = null, IAttachmentImageDecoder? images = null) : IConversationDraftRepository
{
    private readonly ConversationDraftAttachmentFiles? _files = paths is null ? null : new(paths, images);
    private readonly SqliteConversationDraftWriter _writer = new(paths, images);
    internal bool UsesConnections(SqliteConnectionFactory candidate) => ReferenceEquals(connections, candidate);

    public Task<ConversationDraft?> GetAsync(ConversationDraftScope scope, CancellationToken cancellationToken) =>
        Task.Run(() => RunBoundedAsync(token => ReadCoreAsync(scope, token), cancellationToken), cancellationToken);

    private async Task<ConversationDraft?> ReadCoreAsync(ConversationDraftScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        scope.Validate();
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: true);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT text,revision FROM conversation_drafts
            WHERE scope_key=$scope AND project_id IS $project AND task_id IS $task AND {SqliteConversationDraftWriter.ScopeExists};
            """;
        AddScope(command, scope);
        string text;
        long revision;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return null;
            text = reader.GetString(0);
            revision = reader.GetInt64(1);
        }
        if (text.Length > ConversationDraft.MaximumTextLength || revision <= 0)
            throw new InvalidOperationException("Invalid stored conversation draft.");
        var metadata = await SqliteConversationDraftAttachments.ReadAsync(connection, transaction, scope, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var attachments = new List<ConversationDraftAttachment>();
        foreach (var item in metadata)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConversationDraftAttachmentValidation.Validate(scope, [item.Attachment], requireContent: false);
            try { attachments.Add(_files is null ? item.Attachment : await _files.ReadAsync(item, cancellationToken)); }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                attachments.Add(item.Attachment);
            }
        }
        ConversationDraftAttachmentValidation.Validate(scope, attachments, requireContent: false);
        return new(text, revision, attachments.Count == 0 ? null : attachments.ToArray());
    }

    public Task<bool> SaveAsync(IReadOnlyList<ConversationDraftUpdate> updates, CancellationToken cancellationToken)
    {
        var snapshot = updates.Select(update => update with
        {
            Attachments = update.AttachmentItems.Count == 0 ? null : update.AttachmentItems.ToArray()
        }).ToArray();
        return Task.Run(() => RunBoundedAsync(token => SaveCoreAsync(snapshot, token), cancellationToken), cancellationToken);
    }

    private async Task<bool> SaveCoreAsync(IReadOnlyList<ConversationDraftUpdate> updates, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _writer.Validate(updates);
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        if (!await _writer.WriteAsync(connection, transaction, updates, cancellationToken)) return false;
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<TResult> RunBoundedAsync<TResult>(Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        return await operation(linked.Token).WaitAsync(linked.Token);
    }

    private static void AddScope(SqliteCommand command, ConversationDraftScope scope)
    {
        command.Parameters.AddWithValue("$scope", scope.Key);
        command.Parameters.AddWithValue("$project", (object?)scope.ProjectId?.ToString("D") ?? DBNull.Value);
        command.Parameters.AddWithValue("$task", (object?)scope.TaskId?.ToString("D") ?? DBNull.Value);
    }
}
