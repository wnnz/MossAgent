using MossAgent.Domain;

namespace MossAgent.Tools.Abstractions.SourceControl;

public interface ISourcePullRequestWriter
{
    Task ValidateWriteAsync(SourceRepositoryScope scope, PullRequestRevision revision, CancellationToken cancellationToken);
    Task ValidateWriteAsync(SourceRepositoryScope scope, CancellationToken cancellationToken);
    Task<PullRequestCreateReceipt> CreateAsync(SourceRepositoryScope scope, string title, string? body,
        string head, string baseName, bool draft, CancellationToken cancellationToken);
    Task<PullRequestCommentReceipt> CommentAsync(SourceRepositoryScope scope, PullRequestRevision revision, string body, CancellationToken cancellationToken);
    Task<PullRequestReviewReceipt> ReviewAsync(SourceRepositoryScope scope, PullRequestRevision revision,
        PullRequestReviewDecision decision, string? body, CancellationToken cancellationToken);
    Task<PullRequestCloseReceipt> CloseAsync(SourceRepositoryScope scope, PullRequestRevision revision,
        CancellationToken cancellationToken);
    Task<PullRequestMergeReceipt> MergeAsync(SourceRepositoryScope scope, PullRequestRevision revision,
        PullRequestMergeMethod method, CancellationToken cancellationToken);
}