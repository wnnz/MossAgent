using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteConversationSteeringRepository(SqliteConnectionFactory connections, TimeProvider? timeProvider = null)
    : IConversationSteeringRepository
{
    private readonly SqliteConversationDraftWriter _drafts = new(null);
    private DateTimeOffset Now => (timeProvider ?? TimeProvider.System).GetUtcNow();

    public bool SupportsDrafts(IConversationDraftRepository drafts) =>
        drafts is SqliteConversationDraftRepository sqlite && sqlite.UsesConnections(connections);

    public Task<ConversationSteeringRun?> OpenAsync(AgentTask expectedTask, Guid runId, CancellationToken cancellationToken,
        ConversationSteeringAuthorization? authorization = null)
    {
        ValidateIdentity(expectedTask.Id, runId);
        return RunAsync(async token =>
        {
            await using var connection = connections.Create();
            await connection.OpenAsync(token);
            return await SqliteConversationSteeringRuns.OpenAsync(connection, expectedTask, runId, Now, token, authorization);
        }, cancellationToken);
    }

    public Task<ConversationSteeringRun?> GetRunAsync(Guid taskId, Guid runId, CancellationToken cancellationToken)
    {
        ValidateIdentity(taskId, runId);
        return RunAsync(async token =>
        {
            await using var connection = connections.Create();
            await connection.OpenAsync(token);
            return await SqliteConversationSteeringReader.ReadRunAsync(connection, null, taskId, runId, token);
        }, cancellationToken);
    }

    public Task<ConversationSteeringPage> GetInputsAsync(Guid taskId, long beforeSequence, int pageSize, CancellationToken cancellationToken)
    {
        if (taskId == Guid.Empty || beforeSequence <= 0 || pageSize is < 1 or > ConversationSteeringInput.MaximumPageSize)
            throw new ArgumentException("Invalid steering history page.");
        return RunAsync(async token =>
        {
            await using var connection = connections.Create();
            await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {SqliteConversationSteeringReader.Columns} FROM conversation_steering_inputs "
                + "WHERE task_id=$task AND sequence<$before ORDER BY sequence DESC LIMIT $limit;";
            command.Parameters.AddWithValue("$task", taskId.ToString());
            command.Parameters.AddWithValue("$before", beforeSequence);
            command.Parameters.AddWithValue("$limit", pageSize + 1);
            await using var reader = await command.ExecuteReaderAsync(token);
            var items = new List<ConversationSteeringInput>();
            while (await reader.ReadAsync(token)) items.Add(SqliteConversationSteeringReader.ReadInput(reader));
            return new ConversationSteeringPage(items.Take(pageSize).ToArray(), items.Count > pageSize);
        }, cancellationToken);
    }

    public Task<ConversationSteeringAcceptance?> AcceptAsync(Guid taskId, Guid runId, Guid inputId, string text,
        ConversationDraftUpdate? draft, CancellationToken cancellationToken, ConversationSteeringAuthorization? authorization = null)
    {
        ValidateIdentity(taskId, runId);
        ConversationSteeringInput.ValidateText(text);
        if (inputId == Guid.Empty)
            throw new ArgumentException("Invalid steering input.");
        if (draft is not null)
        {
            _drafts.Validate([draft]);
            if (draft.Scope.TaskId != taskId || draft.AttachmentItems.Count != 0)
                throw new ArgumentException("Invalid steering draft consumption.");
        }
        return RunAsync(async token =>
        {
            await using var connection = connections.Create();
            await connection.OpenAsync(token);
            return await SqliteConversationSteeringAcceptance.AcceptAsync(connection, _drafts, taskId, runId,
                inputId, text, draft, Now, token, authorization);
        }, cancellationToken);
    }

    public Task<ConversationSteeringPending?> GetPendingAsync(Guid taskId, Guid runId, CancellationToken cancellationToken)
    {
        ValidateIdentity(taskId, runId);
        return RunAsync<ConversationSteeringPending?>(async token =>
        {
            await using var connection = connections.Create();
            await connection.OpenAsync(token);
            await using var transaction = connection.BeginTransaction(deferred: true);
            var run = await SqliteConversationSteeringReader.ReadRunAsync(connection, transaction, taskId, runId, token);
            return run is null ? null : new(run, await SqliteConversationSteeringReader.ReadPendingAsync(connection, transaction, run, token));
        }, cancellationToken);
    }

    public Task<ConversationSteeringApplication?> ApplyAsync(ConversationSteeringRun expected, IReadOnlyList<Guid> inputIds,
        ConversationMessage? precedingAssistant, CancellationToken cancellationToken, ConversationSteeringAuthorization? authorization = null)
    {
        ValidateRun(expected);
        var ids = inputIds.ToArray();
        if (ids.Length is < 1 or > ConversationSteeringInput.MaximumPendingItems || ids.Any(id => id == Guid.Empty)
            || ids.Distinct().Count() != ids.Length) throw new ArgumentException("Invalid steering boundary prefix.");
        if (precedingAssistant is not null && (precedingAssistant.TaskId != expected.TaskId || precedingAssistant.Id == Guid.Empty
            || precedingAssistant.Role != MessageRole.Assistant || precedingAssistant.ToolCallId is not null
            || precedingAssistant.Attachments is { Count: > 0 } || precedingAssistant.Content.Length > ConversationDraft.MaximumTextLength
            || string.IsNullOrWhiteSpace(precedingAssistant.Content))) throw new ArgumentException("Invalid preceding assistant message.");
        return RunAsync(async token =>
        {
            await using var connection = connections.Create();
            await connection.OpenAsync(token);
            return await SqliteConversationSteeringBoundary.ApplyAsync(connection, expected, ids, precedingAssistant, Now, token, authorization);
        }, cancellationToken);
    }

    public Task<bool> CloseAsync(ConversationSteeringRun expected, bool interruptPending, CancellationToken cancellationToken)
    {
        ValidateRun(expected);
        return RunAsync(async token =>
        {
            await using var connection = connections.Create();
            await connection.OpenAsync(token);
            return await SqliteConversationSteeringRuns.CloseAsync(connection, expected, interruptPending, Now, token);
        }, cancellationToken);
    }

    private static void ValidateIdentity(Guid taskId, Guid runId)
    {
        if (taskId == Guid.Empty || runId == Guid.Empty) throw new ArgumentException("Invalid steering run identity.");
    }

    private static void ValidateRun(ConversationSteeringRun expected)
    {
        ValidateIdentity(expected.TaskId, expected.Id);
        if (expected.Revision is < 0 or long.MaxValue || !expected.IsOpen) throw new ArgumentException("Invalid steering run revision.");
    }

    private static Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        return await action(deadline.Token);
    }, cancellationToken);
}
