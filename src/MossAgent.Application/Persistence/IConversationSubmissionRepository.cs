using MossAgent.Domain;

namespace MossAgent.Application.Persistence;

public interface IConversationSubmissionRepository
{
    bool SupportsDrafts(IConversationDraftRepository drafts);

    Task SaveSubmissionAsync(AgentTask task, ConversationMessage message,
        IReadOnlyList<ConversationDraftUpdate> drafts, CancellationToken cancellationToken);
}
