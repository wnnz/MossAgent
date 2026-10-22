using MossAgent.Domain;

namespace MossAgent.Application.Persistence;

public interface IWorktreeCreationIntentSubmissionRepository
{
    Task SaveTaskWithMessagesAndWorktreeIntentAsync(
        AgentTask task,
        IReadOnlyList<ConversationMessage> messages,
        CancellationToken cancellationToken);

    Task SaveSubmissionAndWorktreeIntentAsync(
        AgentTask task,
        ConversationMessage message,
        IReadOnlyList<ConversationDraftUpdate> drafts,
        CancellationToken cancellationToken);
}
