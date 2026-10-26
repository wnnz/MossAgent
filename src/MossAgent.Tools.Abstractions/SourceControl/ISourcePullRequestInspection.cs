using MossAgent.Domain;

namespace MossAgent.Tools.Abstractions.SourceControl;

public interface ISourcePullRequestInspection
{
    Task ValidateAsync(SourceRepositoryScope scope, PullRequestRevision revision, CancellationToken cancellationToken);
    Task<PullRequestInspectionPage<PullRequestFile>> FilesAsync(SourceRepositoryScope scope, PullRequestRevision revision, int page, CancellationToken cancellationToken);
    Task<PullRequestInspectionPage<PullRequestComment>> CommentsAsync(SourceRepositoryScope scope, PullRequestRevision revision, string kind, int page, CancellationToken cancellationToken);
    Task<PullRequestInspectionPage<PullRequestCheck>> ChecksAsync(SourceRepositoryScope scope, PullRequestRevision revision, string kind, int page, CancellationToken cancellationToken);
    Task<PullRequestInspectionPage<PullRequestReview>> ReviewsAsync(SourceRepositoryScope scope, PullRequestRevision revision, int page, CancellationToken cancellationToken);
    Task<PullRequestInspectionPage<PullRequestActivity>> ActivityAsync(SourceRepositoryScope scope, PullRequestRevision revision, int page, CancellationToken cancellationToken);
}
