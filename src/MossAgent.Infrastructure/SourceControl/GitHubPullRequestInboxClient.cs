using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

public sealed class GitHubPullRequestInboxClient(ISourceAccountHttpClientFactory clients) : IPullRequestInboxClient
{
    private readonly GitHubSourceTransport _transport = new(clients);

    public async Task<PullRequestInboxPage> InboxAsync(SourceAccount account, PullRequestInboxFilter filter,
        int page, CancellationToken cancellationToken)
    {
        if (!account.IsEnabled) throw new SourceReviewException("source_scope_denied");
        if (page is < 1 or > 1000) throw new SourceReviewException("invalid_arguments");
        var role = filter switch
        {
            PullRequestInboxFilter.Involves => "involves",
            PullRequestInboxFilter.Authored => "author",
            PullRequestInboxFilter.Assigned => "assignee",
            PullRequestInboxFilter.ReviewRequested => "review-requested",
            PullRequestInboxFilter.Mentioned => "mentions",
            _ => throw new SourceReviewException("invalid_arguments")
        };
        var body = await _transport.GetAsync(account,
            $"search/issues?q=type:pr+state:open+{role}:@me&sort=updated&order=desc&per_page=20&page={page}",
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        try { return GitHubPullRequestInboxParser.Parse(body, account, page); }
        catch (SourceReviewException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_invalid_response"); }
    }
}
