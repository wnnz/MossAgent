using MossAgent.Domain;

namespace MossAgent.Application.Git;

public interface IGitWorktreeService
{
    Task<string> CreateAsync(
        ProjectProfile project,
        Guid taskId,
        CancellationToken cancellationToken);
    Task RemoveAsync(
        ProjectProfile project,
        string worktreePath,
        CancellationToken cancellationToken);
}

