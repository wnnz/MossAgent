using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Infrastructure.Git;

public sealed class GitWorktreeService(AppDataPaths paths, GitProcessRunner git, TimeProvider? timeProvider = null) : IGitWorktreeService
{
    private readonly GitWorktreeInspector _inspector = new(git);

    public Task ValidateAsync(ProjectProfile project, Guid taskId, string worktreePath, CancellationToken cancellationToken)
    {
        var snapshot = project with { AuthorizedDirectories = project.AuthorizedDirectories.ToArray() };
        return Task.Run(() => ValidateCoreAsync(snapshot, taskId, worktreePath, cancellationToken), cancellationToken);
    }

    private async Task ValidateCoreAsync(ProjectProfile project, Guid taskId, string worktreePath, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            var target = GitWorktreePaths.ManagedTarget(paths, worktreePath, true);
            var supplied = Path.TrimEndingDirectorySeparator(worktreePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
            if (!GitWorktreePaths.Same(target, supplied)
                || !GitWorktreePaths.Same(target, Path.Combine(GitWorktreePaths.Normalize(paths.WorktreesPath), taskId.ToString("N"))))
                throw new UnauthorizedAccessException("The managed worktree path does not belong to this task.");
            var source = await _inspector.ReadProjectAsync(project, false, operation.Token).ConfigureAwait(false);
            await _inspector.ValidateTargetAsync(source, target, operation.Token).ConfigureAwait(false);
            GitWorktreePaths.ManagedTarget(paths, target, true);
            operation.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException failure) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Git worktree validation was cancelled.", failure, cancellationToken);
        }
        catch (OperationCanceledException failure) when (deadline.IsCancellationRequested)
        {
            throw new TimeoutException("Git worktree validation timed out.", failure);
        }
    }

    public Task<string> CreateAsync(
        ProjectProfile project,
        Guid taskId,
        CancellationToken cancellationToken, string? baseReference = null)
    {
        var snapshot = project with { AuthorizedDirectories = project.AuthorizedDirectories.ToArray() };
        return Task.Run(() => CreateCoreAsync(snapshot, taskId, baseReference, cancellationToken), cancellationToken);
    }

    private async Task<string> CreateCoreAsync(ProjectProfile project, Guid taskId, string? baseReference, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var token = operation.Token;
        var target = GitWorktreePaths.ManagedTarget(paths, Path.Combine(paths.WorktreesPath, taskId.ToString("N")), false);
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException("The managed worktree target already exists.");
        try
        {
            var source = await _inspector.ReadProjectAsync(project, true, token).ConfigureAwait(false);
            var commit = await new GitWorktreeReferenceResolver(git).ResolveAsync(source, baseReference, token).ConfigureAwait(false);
            GitWorktreePaths.ManagedTarget(paths, target, false);
            GitWorktreePaths.ValidateRepository(source);
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            GitWorktreePaths.ManagedTarget(paths, target, false);
            var result = await git.RunAsync(
                source.Root, source.Command("worktree", "add", "-b", $"mossagent/{taskId:N}", target, commit), token).ConfigureAwait(false);
            if (result.ExitCode != 0) throw new InvalidOperationException("Git worktree creation failed.");
            GitWorktreePaths.ManagedTarget(paths, target, true);
            await _inspector.ValidateTargetAsync(source, target, token).ConfigureAwait(false);
            return target;
        }
        catch (Exception failure)
        {
            var mapped = failure is OperationCanceledException
                ? cancellationToken.IsCancellationRequested
                    ? new OperationCanceledException("Git worktree creation was cancelled.", failure, cancellationToken)
                    : deadline.IsCancellationRequested ? new TimeoutException("Git worktree creation timed out.", failure) : failure
                : failure;
            if (Directory.Exists(target) || File.Exists(target)) throw new GitWorktreeCreationException(target, mapped);
            if (!ReferenceEquals(mapped, failure)) throw mapped;
            throw;
        }
    }

    public Task RemoveAsync(
        ProjectProfile project,
        string worktreePath,
        CancellationToken cancellationToken)
    {
        var snapshot = project with { AuthorizedDirectories = project.AuthorizedDirectories.ToArray() };
        return Task.Run(() => RemoveCoreAsync(snapshot, worktreePath, cancellationToken), cancellationToken);
    }

    private async Task RemoveCoreAsync(ProjectProfile project, string worktreePath, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var token = operation.Token;
        try
        {
            var target = GitWorktreePaths.ManagedTarget(paths, worktreePath, true);
            var source = await _inspector.ReadProjectAsync(project, false, token).ConfigureAwait(false);
            if (GitWorktreePaths.Within(source.Root, target))
                throw new UnauthorizedAccessException("The project's primary repository cannot be removed as a task worktree.");
            await _inspector.ValidateTargetAsync(source, target, token).ConfigureAwait(false);
            GitWorktreePaths.ManagedTarget(paths, target, true);
            GitWorktreePaths.ValidateRepository(source);
            token.ThrowIfCancellationRequested();
            var result = await git.RunAsync(source.Root, source.Command("worktree", "remove", target), token).ConfigureAwait(false);
            if (result.ExitCode != 0) throw new InvalidOperationException("Git worktree removal failed; the directory was retained for inspection.");
        }
        catch (OperationCanceledException failure) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Git worktree removal was cancelled.", failure, cancellationToken);
        }
        catch (OperationCanceledException failure) when (deadline.IsCancellationRequested)
        {
            throw new TimeoutException("Git worktree removal timed out.", failure);
        }
    }
}
