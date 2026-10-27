using MossAgent.Application.Authorization;
using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Git;

internal sealed class GitWorktreeRootAuthorization(IRootIdentitySessionFactory roots, SqliteConnectionFactory connections)
{
    private readonly SqliteWorktreeRootAuthorizations _store = new(connections);
    public Task<WorktreeRootAuthorization?> ReadAsync(Guid taskId, CancellationToken token) => _store.ReadAsync(taskId, token);

    public async Task<IRootIdentitySession> OpenAsync(ProjectProfile project, Guid taskId, string path, CancellationToken token)
    {
        var grant = await _store.ReadAsync(taskId, token).ConfigureAwait(false);
        if (grant is null || grant.ProjectId != project.Id || grant.GrantId == Guid.Empty) throw new WorktreeAuthorizationException();
        var session = await OpenRootAsync(path, token).ConfigureAwait(false);
        try
        {
            if (!SameObject(grant.Identity, session.Roots[0])) throw new WorktreeAuthorizationException();
            token.ThrowIfCancellationRequested();
            return session;
        }
        catch { session.Dispose(); throw; }
    }

    public async Task EnrollAsync(ProjectProfile project, Guid taskId, string path, AgentTask? expected,
        Func<CancellationToken, Task> validate, CancellationToken token)
    {
        using var session = await OpenRootAsync(path, token).ConfigureAwait(false);
        await validate(token).ConfigureAwait(false);
        await _store.InsertAsync(project, new(taskId, project.Id, Guid.NewGuid(), session.Roots[0]), expected, token).ConfigureAwait(false);
    }

    // Re-enrolls the live replacement object after the caller proved it is a registered
    // worktree of the same repository. Fails closed when the enrolled object still matches
    // (another re-authorization may have landed) or when the persisted snapshot moved on.
    public async Task ReauthorizeAsync(ProjectProfile project, Guid taskId, string path, AgentTask expected,
        Func<CancellationToken, Task> validate, CancellationToken token)
    {
        var grant = await _store.ReadAsync(taskId, token).ConfigureAwait(false);
        if (grant is null || grant.ProjectId != project.Id || !GitWorktreePaths.Same(grant.Identity.Path, path))
            throw new WorktreeAuthorizationException();
        using var session = await OpenRootAsync(path, token).ConfigureAwait(false);
        if (SameObject(grant.Identity, session.Roots[0])) throw new WorktreeAuthorizationException();
        await validate(token).ConfigureAwait(false);
        await _store.ReplaceAsync(project, new(taskId, project.Id, Guid.NewGuid(), session.Roots[0]),
            expected, grant.Identity, token).ConfigureAwait(false);
    }

    // Best-effort classification for UI recovery: the grant is re-read so the identity
    // comparison stays consistent, and any local failure means the panel cannot verify.
    public async Task<WorktreeEnrollmentState> ProbeAsync(Guid taskId, string path, CancellationToken token)
    {
        var grant = await _store.ReadAsync(taskId, token).ConfigureAwait(false);
        if (grant is null) return WorktreeEnrollmentState.Unconfirmed;
        if (!GitWorktreePaths.Same(grant.Identity.Path, path)) return WorktreeEnrollmentState.Unverifiable;
        try
        {
            using var session = await OpenRootAsync(path, token).ConfigureAwait(false);
            return SameObject(grant.Identity, session.Roots[0])
                ? WorktreeEnrollmentState.Enrolled : WorktreeEnrollmentState.Replaced;
        }
        catch (OperationCanceledException) { throw; }
        catch { return WorktreeEnrollmentState.Unverifiable; }
    }

    private async Task<IRootIdentitySession> OpenRootAsync(string path, CancellationToken token)
    {
        IRootIdentitySession session;
        try { session = await RootIdentitySessionOperation.OpenAsync(roots, [path], token).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch { throw new WorktreeAuthorizationException(); }
        if (session.Roots.Count == 1 && GitWorktreePaths.Same(session.Roots[0].Path, path)
            && !string.IsNullOrWhiteSpace(session.Roots[0].VolumeId) && !string.IsNullOrWhiteSpace(session.Roots[0].FileId)) return session;
        session.Dispose();
        throw new WorktreeAuthorizationException();
    }

    private static bool SameObject(DirectoryObjectIdentity left, DirectoryObjectIdentity right) =>
        GitWorktreePaths.Same(left.Path, right.Path) && StringComparer.OrdinalIgnoreCase.Equals(left.VolumeId, right.VolumeId)
        && StringComparer.OrdinalIgnoreCase.Equals(left.FileId, right.FileId);
}
