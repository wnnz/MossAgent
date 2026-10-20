using MossAgent.Domain;
using MossAgent.Application.Conversations;

namespace MossAgent.Application.Persistence;

public interface IConversationFollowupRepository
{
    bool SupportsDrafts(IConversationDraftRepository drafts);

    Task<ConversationFollowupQueue> GetAsync(Guid taskId, CancellationToken cancellationToken);

    // The supplied revision is the expected current revision. Success advances it once.
    Task<bool> SaveAsync(ConversationFollowupQueue queue, IReadOnlyList<ConversationDraftUpdate> drafts,
        CancellationToken cancellationToken);

    // Recheck prepared configuration within the atomic claim; attachment/worktree I/O stays outside it.
    // This commits a user message and Running status, never invokes a provider or replays a command.
    Task<ConversationFollowupDispatch?> DispatchAsync(PreparedConversationFollowup expected,
        CancellationToken cancellationToken);
}
