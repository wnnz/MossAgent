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

    // Removes only the Git registration slot (.git/worktrees/<slot>) whose gitdir
    // pointer still references a managed worktree directory that no longer exists.
    // Refuses when the target directory is present, when no slot points back at it,
    // or when more than one slot does; never runs a global worktree prune.
    Task RemoveStaleRegistrationAsync(
        ProjectProfile project,
        string worktreePath,
        CancellationToken cancellationToken);
}

