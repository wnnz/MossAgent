using MossAgent.Domain;

namespace MossAgent.Application.Git;

public interface IWorktreeCreationIntents
{
    bool SupportsCreationIntents { get; }

    Task ReconcileAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<WorktreeCreationIntent>> ReadIncompleteAsync(
        ProjectProfile project,
        CancellationToken cancellationToken);

    Task AcknowledgeAsync(Guid taskId, CancellationToken cancellationToken);
}
