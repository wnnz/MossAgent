using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public interface IPullRequestWriteClient
{
    Task<long> CommentAsync(SourceAccount account, SourceRepositoryScope scope, PullRequestRevision revision,
        string body, CancellationToken cancellationToken);
    Task<long?> FindCommentAsync(SourceAccount account, SourceRepositoryScope scope, int number,
        string body, CancellationToken cancellationToken);
}
