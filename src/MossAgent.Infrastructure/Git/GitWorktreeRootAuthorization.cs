using MossAgent.Application.Authorization;
using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Git;

internal sealed class GitWorktreeRootAuthorization(
    IRootIdentitySessionFactory roots,
    SqliteConnectionFactory connections,
    IProjectAuthorizationChanges? changes = null)
{
    private readonly SqliteWorktreeRootAuthorizations _store = new(connections);
    public Task<WorktreeRootAuthorization?> ReadAsync(Guid taskId, CancellationToken token) => _store.ReadAsync(taskId, token);

    public async Task<IRootIdentitySession> OpenAsync(ProjectProfile project, Guid taskId, string path, CancellationToken token)
    {
        var grant = await _store.ReadAsync(taskId, token).ConfigureAwait(false);
        if (grant is null || grant.ProjectId != project.Id || grant.GrantId == Guid.Empty) throw new WorktreeAuthorizationException();
        var session = await OpenRootAsync(path, token, transferable: false).ConfigureAwait(false);
        return await ValidateGrantAsync(grant, session, token).ConfigureAwait(false);
    }

    public async Task<IRootIdentitySession> OpenTransferableAsync(
        ProjectProfile project, Guid taskId, string path, CancellationToken token)
    {
        var grant = await _store.ReadAsync(taskId, token).ConfigureAwait(false);
        if (grant is null || grant.ProjectId != project.Id || grant.GrantId == Guid.Empty)
            throw new WorktreeAuthorizationException();
        var session = await OpenRootAsync(path, token, transferable: true).ConfigureAwait(false);
        return await ValidateGrantAsync(grant, session, token).ConfigureAwait(false);
    }

    private static async Task<IRootIdentitySession> ValidateGrantAsync(
        WorktreeRootAuthorization grant, IRootIdentitySession session,
        CancellationToken token)
    {
        try
        {
            if (!SameObject(grant.Identity, session.Roots[0])) throw new WorktreeAuthorizationException();
            token.ThrowIfCancellationRequested();
            return session;
        }
        catch { session.Dispose(); throw; }
    }

    public async Task EnrollAsync(ProjectProfile project, Guid taskId, string path, AgentTask? expected,
        Func<CancellationToken, Task> validate, CancellationToken token,
        DirectoryObjectIdentity? expectedIdentity = null)
    {
        using var session = await OpenRootAsync(
            path, token, transferable: true).ConfigureAwait(false);
        if (expectedIdentity is not null
            && !SameObject(expectedIdentity, session.Roots[0]))
            throw new WorktreeAuthorizationException();
        await validate(token).ConfigureAwait(false);
        await _store.InsertAsync(project, new(taskId, project.Id, Guid.NewGuid(), session.Roots[0]), expected, token).ConfigureAwait(false);
    }

    public async Task<WorktreeRootReauthorization> ReauthorizeAsync(
        ProjectProfile project,
        AgentTask task,
        string path,
        Func<IRootIdentitySession, CancellationToken, Task> validate,
        CancellationToken token)
    {
        if (task.ProjectId != project.Id || task.WorktreePath is null
            || !GitWorktreePaths.Same(task.WorktreePath, path)
            || task.Status is AgentTaskStatus.Running or AgentTaskStatus.WaitingForApproval)
            throw new WorktreeAuthorizationException();
        var previous = await _store.ReadAsync(task.Id, token).ConfigureAwait(false)
            ?? throw new WorktreeAuthorizationException();
        if (previous.ProjectId != project.Id
            || !GitWorktreePaths.Same(previous.Identity.Path, path))
            throw new WorktreeAuthorizationException();
        using var session = await OpenRootAsync(path, token, transferable: false).ConfigureAwait(false);
        await validate(session, token).ConfigureAwait(false);
        var current = new WorktreeRootAuthorization(
            task.Id, project.Id, Guid.NewGuid(), session.Roots[0]);
        var revision = await _store.ReauthorizeAsync(
            project, task, previous, current, token).ConfigureAwait(false);
        changes?.Publish(project.Id, revision);
        return new(task.Id, project.Id, current.GrantId, revision,
            previous.Identity, current.Identity);
    }

    private async Task<IRootIdentitySession> OpenRootAsync(
        string path, CancellationToken token, bool transferable)
    {
        IRootIdentitySession session;
        try
        {
            session = transferable && roots is ITransferableRootIdentitySessionFactory transferableFactory
                ? await RootIdentitySessionOperation.OpenTransferableAsync(
                    transferableFactory, [path], token).ConfigureAwait(false)
                : await RootIdentitySessionOperation.OpenAsync(
                    roots, [path], token).ConfigureAwait(false);
        }
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
