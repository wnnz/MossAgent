using MossAgent.Application.Git;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Git;

internal sealed class GitWorktreeRemovalHandoff(
    IPathQuarantineSessionFactory quarantines,
    IPathDeleteSessionFactory deletes,
    IWorktreeCleanupIntentService cleanups)
{
    public async Task RemoveAsync(
        Guid projectId,
        Guid taskId,
        string target,
        GitWorktreeRepository source,
        string worktreesRoot,
        Action releaseIdentity,
        Func<CancellationToken, Task> validateAuthorization,
        CancellationToken cancellationToken)
    {
        var worktree = GitWorktreeMetadata.Read(target);
        ValidateRegistration(target, source, worktree);
        var quarantinePath = Path.Combine(worktreesRoot,
            $"{Guid.NewGuid():N}.quarantine-{Guid.NewGuid():N}");
        await cleanups.BeginAsync(projectId, taskId, target, quarantinePath,
            cancellationToken).ConfigureAwait(false);
        try
        {
            using var registration = deletes.Create(
                worktree.GitDirectory, [source.CommonDirectory], recursive: true,
                fileOnly: false, cancellationToken);
            using var quarantine = quarantines.Create(
                target, quarantinePath, [worktreesRoot], cancellationToken);
            releaseIdentity();
            await quarantine.MoveAsync(
                validateAuthorization, cancellationToken).ConfigureAwait(false);
            await cleanups.MarkQuarantinedAsync(
                taskId, quarantinePath, CancellationToken.None).ConfigureAwait(false);
            await registration.DeleteAsync(
                validateAuthorization, cancellationToken).ConfigureAwait(false);
            await cleanups.MarkRegistrationDeletedAsync(
                taskId, quarantinePath, CancellationToken.None).ConfigureAwait(false);
            await quarantine.DeleteMovedAsync(
                validateAuthorization, cancellationToken).ConfigureAwait(false);
            await cleanups.MarkCompletedAsync(
                taskId, quarantinePath, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            try
            {
                await cleanups.MarkFailedAsync(taskId, quarantinePath,
                    GitWorktreeFailureCode.CleanupFromException(failure),
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch { }
            throw;
        }
    }

    private static void ValidateRegistration(
        string target, GitWorktreeRepository source, GitWorktreeRepository worktree)
    {
        var registrations = Path.Combine(source.CommonDirectory, "worktrees");
        GitWorktreePaths.ValidateRepository(worktree);
        var locked = Path.Combine(worktree.GitDirectory, "locked");
        GitWorktreePaths.RejectLinks(locked);
        if (File.Exists(locked) || Directory.Exists(locked))
            throw new InvalidOperationException("The Git worktree is locked and must be unlocked manually.");
        if (!GitWorktreePaths.Same(worktree.Root, target)
            || !GitWorktreePaths.Same(worktree.CommonDirectory, source.CommonDirectory)
            || !GitWorktreePaths.Within(worktree.GitDirectory, registrations)
            || GitWorktreePaths.Same(worktree.GitDirectory, registrations)
            || !GitWorktreePaths.Same(
                Path.GetDirectoryName(worktree.GitDirectory) ?? string.Empty, registrations))
            throw new UnauthorizedAccessException(
                "The worktree Git registration does not match the authorized repository.");
    }
}
