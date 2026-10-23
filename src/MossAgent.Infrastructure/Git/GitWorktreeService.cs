using MossAgent.Application.Git;
using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Git;

public sealed class GitWorktreeService(AppDataPaths paths, GitProcessRunner git, TimeProvider? timeProvider = null,
    IRootIdentitySessionFactory? roots = null, SqliteConnectionFactory? connections = null,
    ProjectAuthorizationMonitor? authorizations = null, IWorktreeTargetClaimFactory? targets = null)
    : IGitWorktreeService, IGitWorktreeAuthorizationService, IWorktreeCreationIntents
{
    private readonly GitWorktreeInspector _inspector = new(git);
    private readonly GitWorktreeRootAuthorization? _authorization = roots is not null && connections is not null
        ? new(roots, connections) : roots is null && connections is null ? null
        : throw new ArgumentException("Worktree identity and persistence must be configured together.");
    private readonly SqliteWorktreeCreationIntents? _intents = connections is null ? null : new(connections);

    public bool SupportsCreationIntents => _intents is not null;

    public Task ReconcileAsync(CancellationToken cancellationToken) =>
        _intents?.ReconcileAsync(cancellationToken) ?? Task.CompletedTask;

    public Task<IReadOnlyList<WorktreeCreationIntent>> ReadIncompleteAsync(ProjectProfile project, CancellationToken token) =>
        _intents?.ReadIncompleteAsync(project.Id, token) ?? Task.FromResult<IReadOnlyList<WorktreeCreationIntent>>([]);

    public Task AcknowledgeAsync(Guid taskId, CancellationToken token) =>
        _intents?.AcknowledgeAsync(taskId, token) ?? Task.CompletedTask;

    public Task<WorktreeRootAuthorization?> ReadAuthorizationAsync(Guid taskId, CancellationToken token) =>
        Task.Run(() => (_authorization ?? throw new WorktreeAuthorizationException()).ReadAsync(taskId, token), token);

    public async Task ValidateAsync(ProjectProfile project, Guid taskId, string worktreePath, CancellationToken cancellationToken)
    {
        await using var lease = await AcquireAsync(project, taskId, worktreePath, cancellationToken).ConfigureAwait(false);
    }

    public Task<ITaskWorkspaceLease> AcquireAsync(ProjectProfile project, Guid taskId, string path, CancellationToken token)
    {
        var snapshot = Snapshot(project);
        return Task.Run(() => AcquireCoreAsync(snapshot, taskId, path, token), token);
    }

    private async Task<ITaskWorkspaceLease> AcquireCoreAsync(ProjectProfile project, Guid taskId, string worktreePath, CancellationToken cancellationToken)
    {
        IRootIdentitySession? session = null;
        OperationScope? scope = null;
        try
        {
            scope = await OpenScopeAsync(project, cancellationToken).ConfigureAwait(false);
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
        catch (OperationCanceledException failure)
        {
            var mapped = scope?.MapCancellation(failure, cancellationToken, "validation") ?? failure;
            if (!ReferenceEquals(mapped, failure)) throw mapped;
            throw;
        }
        finally
        {
            session?.Dispose();
            if (scope is not null) await scope.DisposeAsync().ConfigureAwait(false);
        }
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
        await using var scope = await OpenScopeAsync(project, token).ConfigureAwait(false);
        var target = ValidateTaskPath(task.Id, task.WorktreePath);
        var source = await _inspector.ReadProjectAsync(project, false, scope.Token).ConfigureAwait(false);
        await _authorization.EnrollAsync(project, task.Id, target, task,
            ct => _inspector.ValidateTargetAsync(source, target, ct), scope.Token).ConfigureAwait(false);
    }, token);

    public Task<string> CreateAsync(
        ProjectProfile project,
        Guid taskId,
        CancellationToken cancellationToken, string? baseReference = null)
    {
        var snapshot = Snapshot(project);
        return Task.Run(() => CreateCoreAsync(snapshot, taskId, baseReference, cancellationToken), cancellationToken);
    }

    private async Task<string> CreateCoreAsync(ProjectProfile project, Guid taskId, string? baseReference, CancellationToken cancellationToken)
    {
        var target = GitWorktreePaths.ManagedTarget(paths, Path.Combine(paths.WorktreesPath, taskId.ToString("N")), false);
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException("The managed worktree target already exists.");
        OperationScope? scope = null;
        try
        {
            scope = await OpenScopeAsync(project, cancellationToken).ConfigureAwait(false);
            var token = scope.Token;
            var source = await _inspector.ReadProjectAsync(project, true, token).ConfigureAwait(false);
            var commit = await new GitWorktreeReferenceResolver(git).ResolveAsync(source, baseReference, token).ConfigureAwait(false);
            GitWorktreePaths.ManagedTarget(paths, target, false);
            GitWorktreePaths.ValidateRepository(source);
            token.ThrowIfCancellationRequested();
            var intentRecorded = false;
            if (_intents is not null)
            {
                await _intents.RecordAsync(taskId, project, source.Root, baseReference, commit, target, token).ConfigureAwait(false);
                intentRecorded = true;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            GitWorktreePaths.ManagedTarget(paths, target, false);
            using var claim = targets is null ? null : await targets.CreateAsync(target, token).ConfigureAwait(false);
            if (claim is not null && _intents is not null)
                await _intents.RecordIdentityAsync(taskId, target, claim.Identity, token).ConfigureAwait(false);
            var result = await git.RunAsync(
                source.Root, source.Command("worktree", "add", "-b", $"mossagent/{taskId:N}", target, commit), token).ConfigureAwait(false);
            if (result.ExitCode != 0) throw new InvalidOperationException("Git worktree creation failed.");
            GitWorktreePaths.ManagedTarget(paths, target, true);
            await _inspector.ValidateTargetAsync(source, target, token).ConfigureAwait(false);
            claim?.Verify();
            if (intentRecorded)
                await _intents!.MarkStageAsync(taskId,
                    claim is null ? WorktreeCreationIntentStage.Recorded : WorktreeCreationIntentStage.TargetClaimed,
                    WorktreeCreationIntentStage.GitCreated, token).ConfigureAwait(false);
            if (_authorization is not null)
                await _authorization.EnrollAsync(project, taskId, target, null,
                    ct => _inspector.ValidateTargetAsync(source, target, ct), token).ConfigureAwait(false);
            return target;
        }
        catch (Exception failure)
        {
            var mapped = failure is OperationCanceledException cancelled
                ? scope?.MapCancellation(cancelled, cancellationToken, "creation") ?? cancelled
                : failure;
            if (_intents is not null && !cancellationToken.IsCancellationRequested)
            {
                try { await _intents.MarkFailedAsync(taskId, CreationErrorCode(mapped), CancellationToken.None).ConfigureAwait(false); }
                catch { }
            }
            if (Directory.Exists(target) || File.Exists(target)) throw new GitWorktreeCreationException(target, mapped);
            if (!ReferenceEquals(mapped, failure)) throw mapped;
            throw;
        }
        finally { if (scope is not null) await scope.DisposeAsync().ConfigureAwait(false); }
    }

    public Task RemoveAsync(
        ProjectProfile project,
        string worktreePath,
        CancellationToken cancellationToken)
    {
        var snapshot = Snapshot(project);
        return Task.Run(() => RemoveCoreAsync(snapshot, worktreePath, cancellationToken), cancellationToken);
    }

    private async Task RemoveCoreAsync(ProjectProfile project, string worktreePath, CancellationToken cancellationToken)
    {
        OperationScope? scope = null;
        try
        {
            scope = await OpenScopeAsync(project, cancellationToken).ConfigureAwait(false);
            var token = scope.Token;
            var target = GitWorktreePaths.ManagedTarget(paths, worktreePath, true);
            using var identity = _authorization is null ? null : await _authorization.OpenAsync(project,
                Guid.ParseExact(Path.GetFileName(target), "N"), target, token).ConfigureAwait(false);
            var source = await _inspector.ReadProjectAsync(project, false, token).ConfigureAwait(false);
            if (GitWorktreePaths.Within(source.Root, target))
                throw new UnauthorizedAccessException("The project's primary repository cannot be removed as a task worktree.");
            await _inspector.ValidateTargetAsync(source, target, token).ConfigureAwait(false);
            GitWorktreePaths.ManagedTarget(paths, target, true);
            GitWorktreePaths.ValidateRepository(source);
            token.ThrowIfCancellationRequested();
            // Git must delete this directory. The final release-to-remove race is documented separately.
            identity?.Dispose();
            var result = await git.RunAsync(source.Root, source.Command("worktree", "remove", target), token).ConfigureAwait(false);
            if (result.ExitCode != 0) throw new InvalidOperationException("Git worktree removal failed; the directory was retained for inspection.");
        }
        catch (OperationCanceledException failure)
        {
            var mapped = scope?.MapCancellation(failure, cancellationToken, "removal") ?? failure;
            if (!ReferenceEquals(mapped, failure)) throw mapped;
            throw;
        }
        finally { if (scope is not null) await scope.DisposeAsync().ConfigureAwait(false); }
    }

    private static string CreationErrorCode(Exception failure) => failure switch
    {
        TimeoutException => "timeout",
        WorktreeAuthorizationException => "authorization",
        GitWorktreeReferenceException => "reference",
        IOException => "io",
        InvalidOperationException => "git",
        _ => "unknown",
    };

    private static ProjectProfile Snapshot(ProjectProfile project) =>
        project with { AuthorizedDirectories = project.AuthorizedDirectories.ToArray() };

    private async Task<OperationScope> OpenScopeAsync(ProjectProfile project, CancellationToken cancellationToken)
    {
        var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
        var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        ITaskWorkspaceLease? projectLease = null;
        try
        {
            projectLease = authorizations is null ? null
                : await authorizations.AcquireAsync(project, operation.Token).ConfigureAwait(false);
            var scope = CancellationTokenSource.CreateLinkedTokenSource(operation.Token, projectLease?.Revocation ?? default);
            return new(deadline, operation, projectLease, scope);
        }
        catch
        {
            if (projectLease is not null) await projectLease.DisposeAsync().ConfigureAwait(false);
            operation.Dispose();
            deadline.Dispose();
            throw;
        }
    }

    private sealed class OperationScope(
        CancellationTokenSource deadline, CancellationTokenSource operation,
        ITaskWorkspaceLease? projectLease, CancellationTokenSource scope) : IAsyncDisposable
    {
        public CancellationToken Token { get; } = scope.Token;

        public Exception MapCancellation(OperationCanceledException failure, CancellationToken userToken, string verb) =>
            userToken.IsCancellationRequested
                ? new OperationCanceledException($"Git worktree {verb} was cancelled.", failure, userToken)
                : deadline.IsCancellationRequested
                    ? new TimeoutException($"Git worktree {verb} timed out.", failure)
                    : failure;

        public async ValueTask DisposeAsync()
        {
            scope.Dispose();
            if (projectLease is not null) await projectLease.DisposeAsync().ConfigureAwait(false);
            operation.Dispose();
            deadline.Dispose();
        }
    }
}
