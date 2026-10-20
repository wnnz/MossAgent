using MossAgent.Domain;

namespace MossAgent.Application.Git;

public interface IGitWorktreeService
{
    Task ValidateAsync(ProjectProfile project, Guid taskId, string worktreePath, CancellationToken cancellationToken);

    Task<string> CreateAsync(
        ProjectProfile project,
        Guid taskId,
        CancellationToken cancellationToken, string? baseReference = null);
    Task RemoveAsync(
        ProjectProfile project,
        string worktreePath,
        CancellationToken cancellationToken);
}

