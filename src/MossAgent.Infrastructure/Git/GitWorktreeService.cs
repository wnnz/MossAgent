using MossAgent.Application.Git;
using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Git;

public sealed class GitWorktreeService(AppDataPaths paths, GitProcessRunner git, TimeProvider? timeProvider = null,
    IRootIdentitySessionFactory? roots = null, SqliteConnectionFactory? connections = null,
    ProjectAuthorizationMonitor? authorizations = null,
    IExclusiveDirectoryClaimFactory? exclusiveDirectories = null, IPathQuarantineSessionFactory? quarantineSessions = null,
    IPathDeleteSessionFactory? deleteSessions = null, IWorktreeCleanupIntentService? cleanupIntents = null,
    IProjectAuthorizationChanges? authorizationChanges = null)
    : IGitWorktreeService, IGitWorktreeAuthorizationService, IWorktreeCreationIntentService
{
    private readonly GitWorktreeInspector _inspector = new(git);
    private readonly SqliteWorktreeCreationIntentStore? _intents = connections is null ? null
        : new(connections, timeProvider ?? TimeProvider.System);
    private readonly GitWorktreeRootAuthorization? _authorization = roots is not null && connections is not null
        ? new(roots, connections, authorizationChanges) : roots is null && connections is null ? null
        : throw new ArgumentException("Worktree identity and persistence must be configured together.");
    public Task<WorktreeRootAuthorization?> ReadAuthorizationAsync(Guid taskId, CancellationToken token) =>
        Task.Run(() => (_authorization ?? throw new WorktreeAuthorizationException()).ReadAsync(taskId, token), token);
    public async Task ValidateAsync(ProjectProfile project, Guid taskId, string worktreePath, CancellationToken cancellationToken)
    {
        await using var lease = await AcquireAsync(project, taskId, worktreePath, cancellationToken).ConfigureAwait(false);
    }
    public Task<ITaskWorkspaceLease> AcquireAsync(ProjectProfile project, Guid taskId, string path, CancellationToken token)
    {
        var snapshot = project with { AuthorizedDirectories = project.AuthorizedDirectories.ToArray() };
        return Task.Run(() => AcquireCoreAsync(snapshot, taskId, path, token), token);
    }

    private async Task<ITaskWorkspaceLease> AcquireCoreAsync(ProjectProfile project, Guid taskId, string worktreePath, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        IRootIdentitySession? session = null;
        try
        {
            await using var projectLease = authorizations is null ? null : await authorizations.AcquireAsync(project, operation.Token).ConfigureAwait(false);
            using var scope = CancellationTokenSource.CreateLinkedTokenSource(operation.Token, projectLease?.Revocation ?? default);
            var token = scope.Token;
            var target = ValidateTaskPath(taskId, worktreePath);
            if (_authorization is not null)
                session = await _authorization.OpenAsync(project, taskId, target, token).ConfigureAwait(false);
            var source = await _inspector.ReadProjectAsync(project, false, token).ConfigureAwait(false);
            await _inspector.ValidateTargetAsync(source, target, token).ConfigureAwait(false);
            GitWorktreePaths.ManagedTarget(paths, target, true);
            token.ThrowIfCancellationRequested();
            var lease = new WorktreeRootLease(project.Id, project.AuthorizationRevision, session);
            session = null;
            return lease;
        }
        catch (OperationCanceledException failure) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Git worktree validation was cancelled.", failure, cancellationToken);
        }
        catch (OperationCanceledException failure) when (deadline.IsCancellationRequested)
        {
            throw new TimeoutException("Git worktree validation timed out.", failure);
        }
        finally { session?.Dispose(); }
    }

    private string ValidateTaskPath(Guid taskId, string path)
    {
        var target = GitWorktreePaths.ManagedTarget(paths, path, true);
        var supplied = Path.TrimEndingDirectorySeparator(path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
        if (taskId == Guid.Empty || !GitWorktreePaths.Same(target, supplied)
            || !GitWorktreePaths.Same(target, Path.Combine(GitWorktreePaths.Normalize(paths.WorktreesPath), taskId.ToString("N"))))
            throw new UnauthorizedAccessException("The managed worktree path does not belong to this task.");
        return target;
    }

    public Task ConfirmLegacyAsync(ProjectProfile project, AgentTask task, CancellationToken token) => Task.Run(async () =>
    {
        if (_authorization is null || task.ProjectId != project.Id || task.WorktreePath is null
            || task.Status is AgentTaskStatus.Running or AgentTaskStatus.WaitingForApproval) throw new WorktreeAuthorizationException();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        await using var projectLease = authorizations is null ? null : await authorizations.AcquireAsync(project, operation.Token).ConfigureAwait(false);
        using var scope = CancellationTokenSource.CreateLinkedTokenSource(operation.Token, projectLease?.Revocation ?? default);
        var target = ValidateTaskPath(task.Id, task.WorktreePath);
        var source = await _inspector.ReadProjectAsync(project, false, scope.Token).ConfigureAwait(false);
        await _authorization.EnrollAsync(project, task.Id, target, task,
            ct => _inspector.ValidateTargetAsync(source, target, ct), scope.Token).ConfigureAwait(false);
    }, token);

    public Task<WorktreeRootReauthorization> ReauthorizeAsync(ProjectProfile project, AgentTask task, CancellationToken token) =>
        new GitWorktreeReauthorizer(paths, _inspector, _authorization ?? throw new WorktreeAuthorizationException(), authorizations, timeProvider ?? TimeProvider.System).ReauthorizeAsync(project, task, token);

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
        var target = GitWorktreePaths.ManagedTarget(paths, Path.Combine(paths.WorktreesPath, taskId.ToString("N")), false);
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException("The managed worktree target already exists.");
        var intentStarted = false;
        try
        {
            await using var projectLease = authorizations is null ? null : await authorizations.AcquireAsync(project, operation.Token).ConfigureAwait(false);
            using var scope = CancellationTokenSource.CreateLinkedTokenSource(operation.Token, projectLease?.Revocation ?? default);
            var token = scope.Token;
            var source = await _inspector.ReadProjectAsync(project, true, token).ConfigureAwait(false);
            var commit = await new GitWorktreeReferenceResolver(git).ResolveAsync(source, baseReference, token).ConfigureAwait(false);
            if (_intents is not null)
            {
                await _intents.BeginAsync(project.Id, taskId, source.Root, commit, target, token).ConfigureAwait(false);
                intentStarted = true;
            }
            GitWorktreePaths.ManagedTarget(paths, target, false);
            GitWorktreePaths.ValidateRepository(source);
            token.ThrowIfCancellationRequested();
            var worktreesRoot = GitWorktreePaths.Normalize(paths.WorktreesPath);
            Directory.CreateDirectory(worktreesRoot);
            using var claim = exclusiveDirectories?.Create(
                target, [worktreesRoot], token);
            if (claim is not null && _intents is not null)
                await _intents.MarkClaimedAsync(taskId, target, claim.Identity, token).ConfigureAwait(false);
            if (claim is null) Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            GitWorktreePaths.ManagedTarget(paths, target, false);
            var result = await git.RunAsync(
                source.Root, source.Command("worktree", "add", "-b", $"mossagent/{taskId:N}", target, commit), token).ConfigureAwait(false);
            if (result.ExitCode != 0) throw new InvalidOperationException("Git worktree creation failed.");
            GitWorktreePaths.ManagedTarget(paths, target, true);
            claim?.Validate();
            if (_intents is not null)
                await _intents.MarkGitCreatedAsync(taskId, target, token).ConfigureAwait(false);
            await _inspector.ValidateTargetAsync(source, target, token).ConfigureAwait(false);
            if (_authorization is not null)
            {
                var expectedIdentity = claim?.Identity;
                claim?.Dispose();
                await _authorization.EnrollAsync(project, taskId, target, null,
                    ct => _inspector.ValidateTargetAsync(source, target, ct), token,
                    expectedIdentity).ConfigureAwait(false);
            }
            return target;
        }
        catch (Exception failure)
        {
            if (intentStarted && _intents is not null)
            {
                try
                {
                    await _intents.MarkFailedAsync(
                        taskId, target, GitWorktreeFailureCode.FromException(failure),
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch { }
            }
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

    public Task<IReadOnlyList<WorktreeCreationIntent>> ReadIncompleteIntentsAsync(
        Guid projectId, CancellationToken cancellationToken) =>
        _intents?.ReadIncompleteAsync(projectId, cancellationToken)
        ?? Task.FromResult<IReadOnlyList<WorktreeCreationIntent>>([]);

    public bool SupportsCreationIntents => _intents is not null;

    public Task MarkConsumedAsync(Guid taskId, string worktreePath, CancellationToken cancellationToken) =>
        _intents?.MarkConsumedAsync(taskId, worktreePath, cancellationToken) ?? Task.CompletedTask;

    public Task MarkFailedAsync(Guid taskId, string worktreePath, string errorCode,
        CancellationToken cancellationToken) =>
        _intents?.MarkFailedAsync(taskId, worktreePath, errorCode, cancellationToken) ?? Task.CompletedTask;

    public Task AcknowledgeAsync(Guid taskId, string worktreePath, CancellationToken cancellationToken) =>
        _intents?.AcknowledgeAsync(taskId, worktreePath, cancellationToken) ?? Task.CompletedTask;

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
        try
        {
            await using var projectLease = authorizations is null ? null : await authorizations.AcquireAsync(project, operation.Token).ConfigureAwait(false);
            using var scope = CancellationTokenSource.CreateLinkedTokenSource(operation.Token, projectLease?.Revocation ?? default);
            var token = scope.Token;
            var target = GitWorktreePaths.ManagedTarget(paths, worktreePath, true);
            using var observedIdentity = _authorization is null ? null : await _authorization.OpenAsync(project,
                Guid.ParseExact(Path.GetFileName(target), "N"), target, token).ConfigureAwait(false);
            var source = await _inspector.ReadProjectAsync(project, false, token).ConfigureAwait(false);
            if (GitWorktreePaths.Within(source.Root, target))
                throw new UnauthorizedAccessException("The project's primary repository cannot be removed as a task worktree.");
            await _inspector.ValidateTargetAsync(source, target, token).ConfigureAwait(false);
            GitWorktreePaths.ManagedTarget(paths, target, true);
            GitWorktreePaths.ValidateRepository(source);
            await _inspector.EnsureRemovableAsync(target, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            observedIdentity?.Dispose();
            using var identity = _authorization is null ? null
                : await _authorization.OpenTransferableAsync(project,
                    Guid.ParseExact(Path.GetFileName(target), "N"), target, token)
                    .ConfigureAwait(false);
            var quarantineFactory =
                identity as IPathQuarantineSessionFactory ?? quarantineSessions;
            if (quarantineFactory is null || deleteSessions is null || cleanupIntents is null)
                throw new NotSupportedException("Handle-safe Git worktree removal is not configured.");
            var handoff = new GitWorktreeRemovalHandoff(
                quarantineFactory, deleteSessions, cleanupIntents);
            await handoff.RemoveAsync(project.Id,
                Guid.ParseExact(Path.GetFileName(target), "N"), target, source,
                GitWorktreePaths.Normalize(paths.WorktreesPath), identity is null ? static () => { } : identity.Dispose,
                validateAuthorization: validationToken =>
                {
                    validationToken.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                }, token).ConfigureAwait(false);
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
