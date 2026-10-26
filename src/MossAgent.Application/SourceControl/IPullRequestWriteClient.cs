using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public interface IPullRequestWriteClient
{
    Task<long> CommentAsync(SourceAccount account, SourceRepositoryScope scope, PullRequestRevision revision,
        string body, CancellationToken cancellationToken);
    Task<long?> FindCommentAsync(SourceAccount account, SourceRepositoryScope scope, int number,
        string body, CancellationToken cancellationToken);
    Task<long> SubmitReviewAsync(SourceAccount account, SourceRepositoryScope scope, PullRequestRevision revision,
        PullRequestReviewDecision decision, string? body, CancellationToken cancellationToken);
    Task<long?> FindReviewAsync(SourceAccount account, SourceRepositoryScope scope, PullRequestRevision revision,
        PullRequestReviewDecision decision, string? body, CancellationToken cancellationToken);
    Task<string> CloseAsync(SourceAccount account, SourceRepositoryScope scope, PullRequestRevision revision,
        CancellationToken cancellationToken);
}
