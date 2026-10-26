using System.Net;
using MossAgent.Domain;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitHubPullRequestInboxTests
{
    private static SourceAccount Account => SourceAccountValidationTests.Create().WithRevision(1);
    private static SourceAccountHttpProbe Probe(string body) => new()
    { RespondAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }) };
    private static string Endpoint(SourceAccount account, string body) =>
        body.Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal);

    [Theory]
    [InlineData(PullRequestInboxFilter.Involves, "involves")]
    [InlineData(PullRequestInboxFilter.Authored, "author")]
    [InlineData(PullRequestInboxFilter.Assigned, "assignee")]
    [InlineData(PullRequestInboxFilter.ReviewRequested, "review-requested")]
    [InlineData(PullRequestInboxFilter.Mentioned, "mentions")]
    public async Task FilterQualifiesFixedSearchEndpointWithoutFollowingServerLinks(PullRequestInboxFilter filter, string role)
    {
        var account = Account;
        var body = Endpoint(account, PullRequestTestData.InboxPage(25, items: PullRequestTestData.InboxItem(repository: "FixTure/Repo")));
        var probe = Probe(body);
        var respond = probe.RespondAsync;
        probe.RespondAsync = (request, token) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(new Uri(account.ApiEndpoint,
                $"search/issues?q=type:pr+state:open+{role}:@me&sort=updated&order=desc&per_page=20&page=2"), request.RequestUri);
            Assert.Equal(account.AccessToken, request.Headers.Authorization!.Parameter);
            return respond(request, token);
        };
        var client = new GitHubPullRequestInboxClient(probe);
        var page = await client.InboxAsync(account, filter, 2, TestContext.Current.CancellationToken);
        var item = Assert.Single(page.Items);
        Assert.Equal(51, item.Id);
        Assert.Equal(7, item.Number);
        Assert.Equal("Inbox pull request", item.Title);
        Assert.Equal("octocat", item.Author);
        Assert.Equal("fixture", item.Owner);
        Assert.Equal("repo", item.Repository);
        Assert.Equal("open", item.State);
        Assert.False(item.IsDraft);
        Assert.Equal(DateTimeOffset.Parse("2024-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture), item.UpdatedAt);
        Assert.Equal(25, page.TotalCount);
        Assert.False(page.IsIncomplete);
        Assert.False(page.HasNextPage);
        Assert.Equal(1, probe.RequestCount);
        Assert.Equal(1, probe.DisposedHandlers);
    }

    [Fact]
    public async Task InvalidAccountOrPageNeverContactsTransport()
    {
        var account = Account;
        var probe = Probe(PullRequestTestData.InboxPage(items: PullRequestTestData.InboxItem()));
        var client = new GitHubPullRequestInboxClient(probe);
        var token = TestContext.Current.CancellationToken;
        var disabled = new SourceAccount(account.Id, account.Name, account.ApiEndpoint, account.AccessToken, null, false, account.Revision);
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(
            () => client.InboxAsync(disabled, PullRequestInboxFilter.Involves, 1, token))).ErrorCode);
        Assert.Equal("invalid_arguments", (await Assert.ThrowsAsync<SourceReviewException>(
            () => client.InboxAsync(account, PullRequestInboxFilter.Involves, 0, token))).ErrorCode);
        Assert.Equal("invalid_arguments", (await Assert.ThrowsAsync<SourceReviewException>(
            () => client.InboxAsync(account, PullRequestInboxFilter.Involves, 1001, token))).ErrorCode);
        Assert.Equal("invalid_arguments", (await Assert.ThrowsAsync<SourceReviewException>(
            () => client.InboxAsync(account, (PullRequestInboxFilter)99, 1, token))).ErrorCode);
        Assert.Equal(0, probe.RequestCount);
    }

    [Theory]
    [InlineData("missing-pull-request")]
    [InlineData("foreign-host")]
    [InlineData("malformed-repository")]
    [InlineData("nested-repository")]
    [InlineData("duplicate-ids")]
    [InlineData("oversized-title")]
    [InlineData("unknown-state")]
    [InlineData("negative-total")]
    [InlineData("bad-updated")]
    [InlineData("string-draft")]
    [InlineData("oversized-page")]
    [InlineData("envelope-array")]
    public async Task MalformedEnvelopeAndItemsAreRejected(string kind)
    {
        var account = Account;
        var body = kind switch
        {
            "missing-pull-request" => PullRequestTestData.InboxPage(items: PullRequestTestData.InboxItem(pullRequest: false)),
            "foreign-host" => PullRequestTestData.InboxPage(items: PullRequestTestData.InboxItem(
                repositoryUrl: "https://attacker.invalid/repos/fixture/repo")),
            "malformed-repository" => PullRequestTestData.InboxPage(items: PullRequestTestData.InboxItem(
                repositoryUrl: Endpoint(account, "https://api.github.com/repos/fixture"))),
            "nested-repository" => PullRequestTestData.InboxPage(items: PullRequestTestData.InboxItem(
                repositoryUrl: Endpoint(account, "https://api.github.com/repos/fixture/repo/extra"))),
            "duplicate-ids" => PullRequestTestData.InboxPage(items:
                [PullRequestTestData.InboxItem(), PullRequestTestData.InboxItem()]),
            "oversized-title" => PullRequestTestData.InboxPage(items: PullRequestTestData.InboxItem(title: new string('x', 513))),
            "unknown-state" => PullRequestTestData.InboxPage(items: PullRequestTestData.InboxItem(state: "merged")),
            "negative-total" => PullRequestTestData.InboxPage(total: -1, items: PullRequestTestData.InboxItem()),
            "bad-updated" => PullRequestTestData.InboxPage(items: PullRequestTestData.InboxItem(updated: "not-a-date")),
            "string-draft" => PullRequestTestData.InboxPage(items: PullRequestTestData.InboxItem(draft: "yes")),
            "oversized-page" => PullRequestTestData.InboxPage(total: 21,
                items: Enumerable.Range(1, 21).Select(index => PullRequestTestData.InboxItem(id: index)).ToArray()),
            _ => $"[{PullRequestTestData.InboxItem()}]"
        };
        var probe = Probe(Endpoint(account, body));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestInboxClient(probe)
            .InboxAsync(account, PullRequestInboxFilter.Involves, 1, TestContext.Current.CancellationToken));
        Assert.Equal("source_invalid_response", failure.ErrorCode);
    }

    [Fact]
    public async Task CredentialEchoedInFieldsIsRejectedWithoutCredentialReflection()
    {
        var account = Account;
        var body = Endpoint(account, PullRequestTestData.InboxPage(items: PullRequestTestData.InboxItem(title: account.AccessToken)));
        var probe = Probe(body);
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestInboxClient(probe)
            .InboxAsync(account, PullRequestInboxFilter.Involves, 1, TestContext.Current.CancellationToken));
        Assert.Equal("source_invalid_response", failure.ErrorCode);
        Assert.DoesNotContain(account.AccessToken, failure.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 40, true)]
    [InlineData(1, 20, false)]
    [InlineData(1000, 5000, false)]
    public async Task NextPageRequiresFullPageWithinBoundsAndTotal(int page, int total, bool expected)
    {
        var account = Account;
        var items = Enumerable.Range(1, 20).Select(index => PullRequestTestData.InboxItem(id: index, number: index)).ToArray();
        var probe = Probe(Endpoint(account, PullRequestTestData.InboxPage(total, items: items)));
        var result = await new GitHubPullRequestInboxClient(probe)
            .InboxAsync(account, PullRequestInboxFilter.Involves, page, TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.HasNextPage);
    }

    [Fact]
    public async Task ClosedStateNullAuthorMissingDraftAndIncompleteFlagAreTolerated()
    {
        var account = Account;
        var closed = PullRequestTestData.InboxItem(id: 52, state: "closed", user: null);
        closed = closed.Replace("\"user\":{\"login\":\"octocat\"}", "\"user\":null", StringComparison.Ordinal)
            .Replace("\"draft\":false,", "", StringComparison.Ordinal);
        var probe = Probe(Endpoint(account, PullRequestTestData.InboxPage(total: 1, incomplete: true, items: closed)));
        var page = await new GitHubPullRequestInboxClient(probe)
            .InboxAsync(account, PullRequestInboxFilter.Mentioned, 1, TestContext.Current.CancellationToken);
        var item = Assert.Single(page.Items);
        Assert.Equal("closed", item.State);
        Assert.Equal(string.Empty, item.Author);
        Assert.False(item.IsDraft);
        Assert.True(page.IsIncomplete);
        Assert.False(page.HasNextPage);
    }
}
