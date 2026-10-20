using MossAgent.Domain;

namespace MossAgent.Application.Persistence;

public interface IConversationDraftRepository
{
    Task<ConversationDraft?> GetAsync(ConversationDraftScope scope, CancellationToken cancellationToken);
    Task<bool> SaveAsync(IReadOnlyList<ConversationDraftUpdate> updates, CancellationToken cancellationToken);
}
