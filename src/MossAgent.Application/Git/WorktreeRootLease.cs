using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Git;

public sealed class WorktreeRootLease(Guid projectId, long revision, IRootIdentitySession? session) : ITaskWorkspaceLease
{
    private IRootIdentitySession? _session = session;
    public Guid ProjectId => projectId;
    public long Revision => revision;
    public CancellationToken Revocation => CancellationToken.None;
    public bool IsRevoked => false;
    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _session, null)?.Dispose();
        return ValueTask.CompletedTask;
    }
}
