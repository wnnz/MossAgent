using MossAgent.Domain;

namespace MossAgent.Application.Git;

public interface IWorktreeCreationIntentService
{
    bool SupportsCreationIntents { get; }
    Task<IReadOnlyList<WorktreeCreationIntent>> ReadIncompleteIntentsAsync(
        Guid projectId, CancellationToken cancellationToken);
    Task MarkConsumedAsync(Guid taskId, string worktreePath, CancellationToken cancellationToken);
    Task MarkFailedAsync(Guid taskId, string worktreePath, string errorCode, CancellationToken cancellationToken);
    Task AcknowledgeAsync(Guid taskId, string worktreePath, CancellationToken cancellationToken);
}
