using System.Text;
using System.Text.Json;
using MossAgent.Application.Attachments;
using MossAgent.Application.Conversations;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteConversationFollowupRepository(SqliteConnectionFactory connections,
    AppDataPaths? paths = null, IAttachmentImageDecoder? images = null, TimeProvider? timeProvider = null,
    AttachmentSnapshotLeases? leases = null, AttachmentSnapshotReaper? reaper = null)
    : IConversationFollowupRepository
{
    private readonly SqliteConversationDraftWriter _drafts = new(paths, images, leases: leases);

    public bool SupportsDrafts(IConversationDraftRepository drafts) =>
        drafts is SqliteConversationDraftRepository sqlite && sqlite.UsesConnections(connections);

    public Task<ConversationFollowupQueue> GetAsync(Guid taskId, CancellationToken cancellationToken)
    {
        if (taskId == Guid.Empty) throw new ArgumentException("Invalid follow-up task.");
        return RunAsync(async token =>
        {
            await using var connection = connections.Create();
            await connection.OpenAsync(token);
            await using var transaction = connection.BeginTransaction(deferred: true);
            return await SqliteConversationFollowupStorage.ReadAsync(connection, transaction, taskId, token);
        }, cancellationToken);
    }

    public Task<bool> SaveAsync(ConversationFollowupQueue queue, IReadOnlyList<ConversationDraftUpdate> drafts,
        CancellationToken cancellationToken)
    {
        ConversationFollowupValidation.Validate(queue);
        var payloads = queue.Items.Select(item => JsonSerializer.Serialize(item)).ToArray();
        var sizes = payloads.Select(Encoding.UTF8.GetByteCount).ToArray();
        if (sizes.Any(size => size > SqliteConversationFollowupStorage.MaximumPayloadBytes)
            || sizes.Sum() > SqliteConversationFollowupStorage.MaximumQueueBytes)
            throw new ArgumentException("Follow-up queue exceeds its storage budget.");
        var snapshot = queue with { Items = payloads.Select(payload => JsonSerializer.Deserialize<ConversationFollowup>(payload)!).ToArray() };
        var updates = drafts.Select(update => update with { Attachments = update.AttachmentItems.ToArray() }).ToArray();
        _drafts.Validate(updates);
        if (updates.Length > 1 || updates.Any(update => update.Scope.TaskId != queue.TaskId
            || snapshot.Items.Count == 0 || update.Scope.ProjectId != snapshot.Items[0].ProjectId))
            throw new ArgumentException("Invalid queued draft scope.");
        return RunAsync(token => SaveCoreAsync(snapshot, payloads, updates, token), cancellationToken);
    }

    private async Task<bool> SaveCoreAsync(ConversationFollowupQueue queue, string[] payloads,
        ConversationDraftUpdate[] drafts, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        if (!await SqliteConversationFollowupStorage.HasTaskScopeAsync(connection, transaction, queue, cancellationToken)
            || !await SqliteConversationFollowupStorage.AdvanceAsync(connection, transaction, queue.TaskId, queue.Revision, cancellationToken)) return false;
        await SqliteConversationFollowupStorage.ReplaceAsync(connection, transaction, queue.TaskId, queue.Items, payloads, cancellationToken);
        if (drafts.Length > 0 && !await _drafts.WriteAsync(connection, transaction, drafts, cancellationToken)) return false;
        await transaction.CommitAsync(cancellationToken);
        reaper?.QueuePass(null);
        return true;
    }

    public Task<ConversationFollowupDispatch?> DispatchAsync(PreparedConversationFollowup expected,
        CancellationToken cancellationToken)
    {
        var (expectedTask, _, _, _, followup, expectedRevision) = expected;
        var followupId = followup.Id;
        if (expectedTask.Id == Guid.Empty || followupId == Guid.Empty || expectedRevision <= 0 || expectedRevision == long.MaxValue)
            throw new ArgumentException("Invalid queued dispatch identity or revision.");
        return RunAsync(async token =>
        {
            await using var connection = connections.Create();
            await connection.OpenAsync(token);
            return await SqliteConversationFollowupDispatcher.DispatchAsync(connection, expected,
                (timeProvider ?? TimeProvider.System).GetUtcNow(), token);
        }, cancellationToken);
    }

    private static Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            return await action(deadline.Token);
        }, cancellationToken);
}
