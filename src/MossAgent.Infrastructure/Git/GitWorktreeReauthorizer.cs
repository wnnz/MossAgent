using MossAgent.Application.Authorization;
using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Infrastructure.Git;

internal sealed class GitWorktreeReauthorizer(
    AppDataPaths paths,
    GitWorktreeInspector inspector,
    GitWorktreeRootAuthorization authorization,
    ProjectAuthorizationMonitor? authorizations,
    TimeProvider timeProvider)
{
    public Task<WorktreeRootReauthorization> ReauthorizeAsync(
        ProjectProfile project, AgentTask task, CancellationToken cancellationToken)
    {
        var snapshot = project with
        {
            AuthorizedDirectories = project.AuthorizedDirectories.ToArray(),
            RootAuthorizations = project.RootAuthorizations.ToArray()
        };
        return Task.Run(() => ReauthorizeCoreAsync(
            snapshot, task, cancellationToken), cancellationToken);
    }

    private async Task<WorktreeRootReauthorization> ReauthorizeCoreAsync(
        ProjectProfile project, AgentTask task, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(
            TimeSpan.FromSeconds(30), timeProvider);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, deadline.Token);
        await using var projectScope = await GitWorktreeAuthorizationScope.AcquireAsync(
            authorizations, authorization, project, operation.Token).ConfigureAwait(false);
        using var scope = CancellationTokenSource.CreateLinkedTokenSource(
            operation.Token, projectScope.Revocation);
        try
        {
            if (task.WorktreePath is null) throw new WorktreeAuthorizationException();
            var target = GitWorktreePaths.ManagedTarget(
                paths, task.WorktreePath, mustExist: true);
            if (task.Id == Guid.Empty || !GitWorktreePaths.Same(
                    target, Path.Combine(GitWorktreePaths.Normalize(paths.WorktreesPath),
                        task.Id.ToString("N"))))
                throw new WorktreeAuthorizationException();
            var source = await inspector.ReadProjectAsync(
                project, false, scope.Token).ConfigureAwait(false);
            await inspector.ValidateTargetAsync(source, target, scope.Token).ConfigureAwait(false);
            return await projectScope.Authorization!.ReauthorizeAsync(
                project, task, target,
                (_, token) => inspector.ValidateTargetAsync(source, target, token),
                scope.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException failure) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                "Git worktree reauthorization was cancelled.", failure, cancellationToken);
        }
        catch (OperationCanceledException failure) when (deadline.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Git worktree reauthorization timed out.", failure);
        }
    }
}
