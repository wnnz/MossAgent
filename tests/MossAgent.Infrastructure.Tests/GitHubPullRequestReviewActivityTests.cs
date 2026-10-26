using System.Net;
using MossAgent.Domain;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitHubPullRequestReviewActivityTests
{
    private static SourceAccount Account => SourceAccountValidationTests.Create().WithRevision(1);
    private static PullRequestRevision Revision => new(7, new string('a', 40), new string('b', 40));
    private static SourceRepositoryScope Scope(SourceAccount account) => new(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo");
    private static SourceAccountHttpProbe Probe(string body) => new()
    { RespondAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }) };
    private static string Endpoint(SourceAccount account, string body) =>
        body.Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal);

    [Theory]
    [InlineData("reviews", "pulls/7/reviews")]
    [InlineData("activity", "issues/7/events")]
    public async Task FixedEndpointsReadOnlyAuthorizedRepositoryWithoutFollowingUrls(string kind, string endpoint)
    {
        var account = Account;
        var body = kind == "reviews" ? $"[{PullRequestTestData.Review()}]" : $"[{PullRequestTestData.Event()}]";
        var probe = Probe(Endpoint(account, body));
        var respond = probe.RespondAsync;
        probe.RespondAsync = (request, token) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(new Uri(account.ApiEndpoint, $"repos/fixture/repo/{endpoint}?per_page=20&page=2"), request.RequestUri);
            Assert.Equal(account.AccessToken, request.Headers.Authorization!.Parameter);
            return respond(request, token);
        };
        var client = new GitHubPullRequestInspectionClient(probe);
        if (kind == "reviews")
        {
            var review = Assert.Single((await client.ReviewsAsync(account, Scope(account), Revision, 2, TestContext.Current.CancellationToken)).Items);
            Assert.Equal("reviewer", review.Author);
            Assert.Equal("APPROVED", review.State);
            Assert.Equal("Untrusted review", review.Body);
            Assert.Equal(new string('b', 40), review.CommitRevision);
            Assert.Equal(DateTimeOffset.Parse("2024-01-02T03:04:05Z", System.Globalization.CultureInfo.InvariantCulture), review.SubmittedAt);
        }
        else
        {
            var item = Assert.Single((await client.ActivityAsync(account, Scope(account), Revision, 2, TestContext.Current.CancellationToken)).Items);
            Assert.Equal("actor", item.Actor);
            Assert.Equal("labeled", item.Event);
            Assert.Equal("bug", item.Detail);
            Assert.Null(item.CommitRevision);
            Assert.NotNull(item.CreatedAt);
        }
        Assert.Equal(1, probe.RequestCount);
        Assert.Equal(1, probe.DisposedHandlers);
    }

    [Theory]
    [InlineData("reviews")]
    [InlineData("activity")]
    public async Task ForeignSelfUrlsAreRejectedWithoutCredentialReflection(string kind)
    {
        var account = Account;
        var body = kind == "reviews" ? PullRequestTestData.Review(repository: "other/repo") : PullRequestTestData.Event(repository: "other/repo");
        var probe = Probe(Endpoint(account, $"[{body}]"));
        var client = new GitHubPullRequestInspectionClient(probe);
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => kind == "reviews"
            ? client.ReviewsAsync(account, Scope(account), Revision, 1, TestContext.Current.CancellationToken)
            : client.ActivityAsync(account, Scope(account), Revision, 1, TestContext.Current.CancellationToken));
        Assert.Equal("source_invalid_response", failure.ErrorCode);
        Assert.DoesNotContain(account.AccessToken, failure.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("APPROVED")]
    [InlineData("CHANGES_REQUESTED")]
    [InlineData("COMMENTED")]
    [InlineData("DISMISSED")]
    [InlineData("PENDING")]
    public async Task KnownReviewStatesAreAccepted(string state)
    {
        var account = Account;
        var probe = Probe(Endpoint(account, $"[{PullRequestTestData.Review(state: state)}]"));
        var review = Assert.Single((await new GitHubPullRequestInspectionClient(probe)
            .ReviewsAsync(account, Scope(account), Revision, 1, TestContext.Current.CancellationToken)).Items);
        Assert.Equal(state, review.State);
    }

    [Theory]
    [InlineData("LGTM")]
    [InlineData("approved")]
    [InlineData("")]
    public async Task UnknownReviewStatesAreRejected(string state)
    {
        var account = Account;
        var probe = Probe(Endpoint(account, $"[{PullRequestTestData.Review(state: state)}]"));
        Assert.Equal("source_invalid_response", (await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestInspectionClient(probe)
            .ReviewsAsync(account, Scope(account), Revision, 1, TestContext.Current.CancellationToken))).ErrorCode);
    }

    [Fact]
    public async Task ReviewBodyBudgetDuplicateIdsAndMalformedFieldsAreRejected()
    {
        var account = Account;
        var oversized = $"[{PullRequestTestData.Review(body: new string('x', 16385))}]";
        var budget = "[" + string.Join(',', Enumerable.Range(1, 5).Select(index => PullRequestTestData.Review(id: index, body: new string('x', 16384)))) + "]";
        var duplicate = $"[{PullRequestTestData.Review()},{PullRequestTestData.Review()}]";
        var badCommit = $"[{PullRequestTestData.Review(commit: "../escape")}]";
        var badDate = $"[{PullRequestTestData.Review(submitted: "not-a-date")}]";
        var secret = $"[{PullRequestTestData.Review(body: Account.AccessToken)}]";
        foreach (var body in new[] { oversized, budget, duplicate, badCommit, badDate })
        {
            var probe = Probe(Endpoint(account, body));
            Assert.Equal("source_invalid_response", (await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestInspectionClient(probe)
                .ReviewsAsync(account, Scope(account), Revision, 1, TestContext.Current.CancellationToken))).ErrorCode);
        }
        var tokenProbe = Probe(Endpoint(account, secret));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestInspectionClient(tokenProbe)
            .ReviewsAsync(account, Scope(account), Revision, 1, TestContext.Current.CancellationToken));
        Assert.Equal("source_invalid_response", failure.ErrorCode);
        Assert.DoesNotContain(account.AccessToken, failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NullReviewAuthorBodyAndSubmittedDateAreTolerated()
    {
        var account = Account;
        var item = PullRequestTestData.Review(user: null, body: null, submitted: null)
            .Replace("\"user\":{\"login\":\"reviewer\"}", "\"user\":null", StringComparison.Ordinal);
        var probe = Probe(Endpoint(account, $"[{item}]"));
        var review = Assert.Single((await new GitHubPullRequestInspectionClient(probe)
            .ReviewsAsync(account, Scope(account), Revision, 1, TestContext.Current.CancellationToken)).Items);
        Assert.Equal(string.Empty, review.Author);
        Assert.Equal(string.Empty, review.Body);
        Assert.Null(review.SubmittedAt);
    }

    [Theory]
    [InlineData("assigned", null, "assignee")]
    [InlineData("renamed", null, "rename")]
    public async Task ActivityDetailProjectsLabelAssigneeAndRename(string name, string? label, string kind)
    {
        var account = Account;
        var item = PullRequestTestData.Event(name: name, label: label);
        item = kind == "assignee" ? item.Replace("\"label\":null", "\"assignee\":{\"login\":\"delegate\"}", StringComparison.Ordinal)
            : item.Replace("\"label\":null", "\"rename\":{\"from\":\"old title\",\"to\":\"new title\"}", StringComparison.Ordinal);
        var probe = Probe(Endpoint(account, $"[{item}]"));
        var activity = Assert.Single((await new GitHubPullRequestInspectionClient(probe)
            .ActivityAsync(account, Scope(account), Revision, 1, TestContext.Current.CancellationToken)).Items);
        Assert.Equal(kind == "assignee" ? "delegate" : "old title → new title", activity.Detail);
    }

    [Fact]
    public async Task ActivityRejectsMalformedFieldsWithoutCredentialReflection()
    {
        var account = Account;
        var badId = PullRequestTestData.Event().Replace("/issues/events/41", "/issues/events/42", StringComparison.Ordinal);
        var badEvent = PullRequestTestData.Event(name: new string('e', 65));
        var badCommit = PullRequestTestData.Event(commit: "../escape");
        var badDate = PullRequestTestData.Event(created: "not-a-date");
        var secret = PullRequestTestData.Event(label: Account.AccessToken);
        foreach (var body in new[] { badId, badEvent, badCommit, badDate })
        {
            var probe = Probe(Endpoint(account, $"[{body}]"));
            Assert.Equal("source_invalid_response", (await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestInspectionClient(probe)
                .ActivityAsync(account, Scope(account), Revision, 1, TestContext.Current.CancellationToken))).ErrorCode);
        }
        var tokenProbe = Probe(Endpoint(account, $"[{secret}]"));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestInspectionClient(tokenProbe)
            .ActivityAsync(account, Scope(account), Revision, 1, TestContext.Current.CancellationToken));
        Assert.Equal("source_invalid_response", failure.ErrorCode);
        Assert.DoesNotContain(account.AccessToken, failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NullActivityActorAndMissingOptionalFieldsAreTolerated()
    {
        var account = Account;
        var item = PullRequestTestData.Event(actor: null, label: null, created: null)
            .Replace("\"actor\":{\"login\":\"actor\"}", "\"actor\":null", StringComparison.Ordinal);
        var probe = Probe(Endpoint(account, $"[{item}]"));
        var activity = Assert.Single((await new GitHubPullRequestInspectionClient(probe)
            .ActivityAsync(account, Scope(account), Revision, 1, TestContext.Current.CancellationToken)).Items);
        Assert.Equal(string.Empty, activity.Actor);
        Assert.Null(activity.Detail);
        Assert.Null(activity.CreatedAt);
    }

    [Fact]
    public async Task InvalidScopeRevisionAndPageNeverContactTransport()
    {
        var account = Account;
        var scope = Scope(account);
        var probe = Probe("[]");
        var client = new GitHubPullRequestInspectionClient(probe);
        var token = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<SourceReviewException>(() => client.ReviewsAsync(account, scope with { AccountId = Guid.NewGuid() }, Revision, 1, token));
        await Assert.ThrowsAsync<SourceReviewException>(() => client.ActivityAsync(account, scope, Revision with { HeadRevision = "../escape" }, 1, token));
        await Assert.ThrowsAsync<SourceReviewException>(() => client.ReviewsAsync(account, scope, Revision, 0, token));
        await Assert.ThrowsAsync<SourceReviewException>(() => client.ActivityAsync(account, scope, Revision, 1001, token));
        Assert.Equal(0, probe.RequestCount);
    }
}
