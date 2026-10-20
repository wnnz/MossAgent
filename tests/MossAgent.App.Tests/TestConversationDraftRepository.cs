using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class TestConversationDraftRepository : IConversationDraftRepository
{
    private readonly Dictionary<ConversationDraftScope, ConversationDraft> _saved = [];
    private readonly object _gate = new();
    private int _readCalls;
    private int _saveCalls;

    public Func<ConversationDraftScope, CancellationToken, Task<ConversationDraft?>>? ReadHandler { get; set; }
    public Func<IReadOnlyList<ConversationDraftUpdate>, CancellationToken, Task<bool>>? SaveHandler { get; set; }
    public int ReadCalls => Volatile.Read(ref _readCalls);
    public int SaveCalls => Volatile.Read(ref _saveCalls);

    public Task<ConversationDraft?> GetAsync(ConversationDraftScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _readCalls);
        if (ReadHandler is { } handler) return handler(scope, cancellationToken);
        lock (_gate) return Task.FromResult(_saved.GetValueOrDefault(scope));
    }

    public Task<bool> SaveAsync(IReadOnlyList<ConversationDraftUpdate> updates, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _saveCalls);
        if (SaveHandler is { } handler) return handler(updates, cancellationToken);
        lock (_gate)
        {
            if (updates.Any(update => (_saved.GetValueOrDefault(update.Scope)?.Revision ?? 0) != update.ExpectedRevision))
                return Task.FromResult(false);
            foreach (var update in updates) _saved[update.Scope] = new(update.Text, update.ExpectedRevision + 1,
                update.AttachmentItems.Count == 0 ? null : update.AttachmentItems.ToArray());
            return Task.FromResult(true);
        }
    }
}
