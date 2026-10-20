using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public interface IPullRequestClient
{
    Task<PullRequestPage> ListAsync(SourceAccount account, SourceRepositoryScope scope, int page, CancellationToken cancellationToken);
    Task<PullRequestSummary> GetAsync(SourceAccount account, SourceRepositoryScope scope, int number, CancellationToken cancellationToken);
}
