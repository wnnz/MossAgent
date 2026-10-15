using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class ConversationPreferenceProbeRepository : IConversationPreferenceRepository
{
    public Dictionary<Guid, ConversationModelPreference> Stored { get; } = [];
    public List<(Guid TaskId, ConversationModelPreference Preference)> Writes { get; } = [];
    public Func<Guid, CancellationToken, Task<ConversationModelPreference?>>? Read { get; set; }
    public Func<CancellationToken, Task<bool>>? Save { get; set; }
    public CancellationToken ReadToken { get; private set; }
    public CancellationToken WriteToken { get; private set; }

    public Task<ConversationModelPreference?> GetAsync(Guid taskId, CancellationToken cancellationToken)
    {
        ReadToken = cancellationToken;
        return Read is { } read ? read(taskId, cancellationToken)
            : Task.FromResult<ConversationModelPreference?>(Stored.GetValueOrDefault(taskId));
    }

    public async Task<bool> SaveAsync(Guid taskId, ConversationModelPreference preference, CancellationToken cancellationToken)
    {
        WriteToken = cancellationToken;
        Writes.Add((taskId, preference));
        var saved = Save is null || await Save(cancellationToken);
        if (saved) Stored[taskId] = preference;
        return saved;
    }
}
