using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public interface IPullRequestInboxClient
{
    Task<PullRequestInboxPage> InboxAsync(SourceAccount account, PullRequestInboxFilter filter, int page,
        CancellationToken cancellationToken);
}
