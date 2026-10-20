using MossAgent.Domain;

namespace MossAgent.Tools.Abstractions.SourceControl;

public interface ISourceReviewReader
{
    Task ValidateAsync(SourceRepositoryScope scope, CancellationToken cancellationToken);
    Task<PullRequestPage> ListAsync(SourceRepositoryScope scope, int page, CancellationToken cancellationToken);
    Task<PullRequestSummary> GetAsync(SourceRepositoryScope scope, int number, CancellationToken cancellationToken);
}
