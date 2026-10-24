using MossAgent.Domain;

namespace MossAgent.Application.Git;

public interface IWorktreeCleanupIntents
{
    bool SupportsCleanupIntents { get; }

    Task ReconcileAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<WorktreeCleanupIntent>> ReadIncompleteAsync(
        ProjectProfile project,
        CancellationToken cancellationToken);

    Task AcknowledgeAsync(Guid taskId, CancellationToken cancellationToken);
}
