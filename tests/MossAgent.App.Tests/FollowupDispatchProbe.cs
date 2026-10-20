using MossAgent.Application.Persistence;
using MossAgent.Application.Conversations;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class FollowupDispatchProbe(IConversationFollowupRepository inner) : IConversationFollowupRepository
{
    public Action? Dispatched { get; set; }
    public Func<Task>? Dispatching { get; set; }
    public bool SupportsDrafts(IConversationDraftRepository drafts) => inner.SupportsDrafts(drafts);
    public Task<ConversationFollowupQueue> GetAsync(Guid taskId, CancellationToken cancellationToken) => inner.GetAsync(taskId, cancellationToken);
    public Task<bool> SaveAsync(ConversationFollowupQueue queue, IReadOnlyList<ConversationDraftUpdate> drafts, CancellationToken cancellationToken) =>
        inner.SaveAsync(queue, drafts, cancellationToken);
    public async Task<ConversationFollowupDispatch?> DispatchAsync(PreparedConversationFollowup expected,
        CancellationToken cancellationToken)
    {
        if (Dispatching is not null) await Dispatching();
        var result = await inner.DispatchAsync(expected, cancellationToken);
        if (result is not null) Dispatched?.Invoke();
        return result;
    }
}
