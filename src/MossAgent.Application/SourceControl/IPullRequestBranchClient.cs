using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public interface IPullRequestBranchClient
{
    Task<IReadOnlyList<PullRequestBranch>> ListAsync(SourceAccount account, SourceRepositoryScope scope,
        CancellationToken cancellationToken);
}
