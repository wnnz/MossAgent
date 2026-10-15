using MossAgent.Domain;

namespace MossAgent.Application.Persistence;

public interface IConversationPreferenceRepository
{
    Task<ConversationModelPreference?> GetAsync(Guid taskId, CancellationToken cancellationToken);
    Task<bool> SaveAsync(Guid taskId, ConversationModelPreference preference, CancellationToken cancellationToken);
}
