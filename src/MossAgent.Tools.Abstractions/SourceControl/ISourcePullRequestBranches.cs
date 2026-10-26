using MossAgent.Domain;

namespace MossAgent.Tools.Abstractions.SourceControl;

// Read-only branch listing for the PR create dialog; it resolves the read grant,
// never the write grant.
public interface ISourcePullRequestBranches
{
    Task<IReadOnlyList<PullRequestBranch>> ListAsync(SourceRepositoryScope scope, CancellationToken cancellationToken);
}
