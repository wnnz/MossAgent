using System.Globalization;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class PullRequestInboxClientProbe : IPullRequestInboxClient
{
    public List<(PullRequestInboxFilter Filter, int Page)> Calls { get; } = [];

    public Func<SourceAccount, PullRequestInboxFilter, int, CancellationToken, Task<PullRequestInboxPage>> RespondAsync { get; set; } =
        (_, _, page, _) => Task.FromResult(new PullRequestInboxPage(
            [new PullRequestInboxItem(51, 7, "Inbox pull request", "octocat", "fixture", "repo", "open", false,
                DateTimeOffset.Parse("2024-01-02T03:04:05Z", CultureInfo.InvariantCulture))],
            page, page == 1, 21, false));

    public Task<PullRequestInboxPage> InboxAsync(SourceAccount account, PullRequestInboxFilter filter, int page,
        CancellationToken cancellationToken)
    {
        Calls.Add((filter, page));
        return RespondAsync(account, filter, page, cancellationToken);
    }
}
