using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Infrastructure.Git;

internal sealed class GitWorktreeAuthorizationScope : IAsyncDisposable
{
    private ProjectRootOperationScope? _lease;
    public GitWorktreeRootAuthorization? Authorization { get; }
    public CancellationToken Revocation => _lease?.Revocation ?? CancellationToken.None;

    private GitWorktreeAuthorizationScope(ProjectRootOperationScope? lease, GitWorktreeRootAuthorization? authorization)
    {
        _lease = lease;
        Authorization = lease is null ? authorization : authorization?.WithScope(lease);
    }

    internal static async Task<GitWorktreeAuthorizationScope> AcquireAsync(ProjectAuthorizationMonitor? monitor,
        GitWorktreeRootAuthorization? authorization, ProjectProfile project, CancellationToken token)
    {
        var lease = monitor is null ? null : await monitor.AcquireRootOperationsAsync(project, token).ConfigureAwait(false);
        try { return new(lease, authorization); }
        catch { if (lease is not null) await lease.DisposeAfterOperationAsync().ConfigureAwait(false); throw; }
    }

    internal ITaskWorkspaceLease? DetachLease() => Interlocked.Exchange(ref _lease, null);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _lease, null) is { } lease)
            await lease.DisposeAfterOperationAsync().ConfigureAwait(false);
    }
}
