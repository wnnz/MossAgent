using MossAgent.Domain;

namespace MossAgent.Application.Git;

public interface IWorktreeCleanupIntentService
{
    string InstanceId { get; }

    Task BeginAsync(Guid projectId, Guid taskId, string targetPath,
        string quarantinePath, CancellationToken cancellationToken);

    Task MarkQuarantinedAsync(Guid taskId, string quarantinePath,
        CancellationToken cancellationToken);

    Task MarkRegistrationDeletedAsync(Guid taskId, string quarantinePath,
        CancellationToken cancellationToken);

    Task MarkCompletedAsync(Guid taskId, string quarantinePath,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(Guid taskId, string quarantinePath, string errorCode,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorktreeCleanupIntent>> ReadIncompleteAsync(
        Guid projectId, CancellationToken cancellationToken);

    Task AcknowledgeAsync(WorktreeCleanupIntent intent,
        CancellationToken cancellationToken);
}
