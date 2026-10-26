using MossAgent.Domain;

namespace MossAgent.Tools.Abstractions.SourceControl;

public interface ISourcePullRequestWriter
{
    Task ValidateWriteAsync(SourceRepositoryScope scope, PullRequestRevision revision, CancellationToken cancellationToken);
    Task<PullRequestCommentReceipt> CommentAsync(SourceRepositoryScope scope, PullRequestRevision revision, string body, CancellationToken cancellationToken);
}
