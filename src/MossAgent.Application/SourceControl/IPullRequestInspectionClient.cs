using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public interface IPullRequestInspectionClient
{
    Task<PullRequestInspectionPage<PullRequestFile>> FilesAsync(SourceAccount account, SourceRepositoryScope scope, PullRequestRevision revision, int page, CancellationToken cancellationToken);
    Task<PullRequestInspectionPage<PullRequestComment>> CommentsAsync(SourceAccount account, SourceRepositoryScope scope, PullRequestRevision revision, string kind, int page, CancellationToken cancellationToken);
    Task<PullRequestInspectionPage<PullRequestCheck>> ChecksAsync(SourceAccount account, SourceRepositoryScope scope, PullRequestRevision revision, string kind, int page, CancellationToken cancellationToken);
    Task<PullRequestInspectionPage<PullRequestReview>> ReviewsAsync(SourceAccount account, SourceRepositoryScope scope, PullRequestRevision revision, int page, CancellationToken cancellationToken);
    Task<PullRequestInspectionPage<PullRequestActivity>> ActivityAsync(SourceAccount account, SourceRepositoryScope scope, PullRequestRevision revision, int page, CancellationToken cancellationToken);
}
