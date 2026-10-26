using MossAgent.Domain;
using MossAgent.Tools.Abstractions.SourceControl;

namespace MossAgent.Application.SourceControl;

// Branch listing is a read-only operation: it resolves the read grant before the
// remote GET and resolves it once more afterwards, so a revocation landing around
// the read still fails closed before the result is handed out.
public sealed class SourcePullRequestBranches(SourceRepositoryAccess access, IPullRequestBranchClient branches)
    : ISourcePullRequestBranches
{
    public async Task<IReadOnlyList<PullRequestBranch>> ListAsync(SourceRepositoryScope scope,
        CancellationToken cancellationToken)
    {
        var account = await access.ResolveAsync(scope, cancellationToken);
        var result = await branches.ListAsync(account, scope, cancellationToken);
        _ = await access.ResolveAsync(scope, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
