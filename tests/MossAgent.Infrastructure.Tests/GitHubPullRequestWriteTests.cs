using System.Net;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitHubPullRequestWriteTests
{
    private static SourceAccount Account => SourceAccountValidationTests.Create().WithRevision(1);
    private static PullRequestRevision Revision => new(7, new string('a', 40), new string('b', 40));
    private static SourceRepositoryScope Scope(SourceAccount account, bool write = true) =>
        new(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", write);
    private static string Created(SourceAccount account, long id = 99) => PullRequestTestData.Comment(id: id)
        .Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal);
    private static SourceAccountHttpProbe Probe(HttpStatusCode status, string body = "{}") => new()
    { RespondAsync = (_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }) };

    [Fact]
    public async Task PostSendsSerializedJsonBodyWithReadHeadersAndParsesTheCreatedComment()
    {
        var account = Account;
        HttpRequestMessage? sent = null;
        string? payload = null;
        var probe = new SourceAccountHttpProbe { RespondAsync = async (request, token) =>
        {
            sent = request;
            payload = await request.Content!.ReadAsStringAsync(token);
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(Created(account)) };
        } };

        var id = await new GitHubPullRequestWriteClient(probe)
            .CommentAsync(account, Scope(account), Revision, "looks good **ship it**\nsecond line", TestContext.Current.CancellationToken);

        Assert.Equal(99, id);
        Assert.Equal(HttpMethod.Post, sent!.Method);
        Assert.Equal(new Uri(account.ApiEndpoint, "repos/fixture/repo/issues/7/comments"), sent.RequestUri);
        Assert.Equal(account.AccessToken, sent.Headers.Authorization!.Parameter);
        Assert.Equal("Bearer", sent.Headers.Authorization.Scheme);
        Assert.Contains(sent.Headers.Accept, value => value.MediaType == "application/vnd.github+json");
        Assert.Equal("2022-11-28", Assert.Single(sent.Headers.GetValues("X-GitHub-Api-Version")));
        Assert.Contains(sent.Headers.UserAgent, value => value.Product!.Name == "MossAgent");
        Assert.Equal("application/json", sent.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("looks good **ship it**\nsecond line", JsonDocument.Parse(payload!).RootElement.GetProperty("body").GetString());
        Assert.Equal(1, probe.RequestCount);
        Assert.Equal(1, probe.DisposedHandlers);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Created)]
    public async Task SuccessStatusesReturnTheCommentId(HttpStatusCode status)
    {
        var account = Account;
        var id = await new GitHubPullRequestWriteClient(Probe(status, Created(account)))
            .CommentAsync(account, Scope(account), Revision, "body", TestContext.Current.CancellationToken);
        Assert.Equal(99, id);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "source_auth_failed")]
    [InlineData(HttpStatusCode.Forbidden, "source_forbidden")]
    [InlineData(HttpStatusCode.NotFound, "source_not_found")]
    [InlineData(HttpStatusCode.Gone, "source_not_found")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "source_write_rejected")]
    [InlineData(HttpStatusCode.TooManyRequests, "source_rate_limited")]
    [InlineData(HttpStatusCode.InternalServerError, "source_unavailable")]
    [InlineData(HttpStatusCode.Found, "source_redirect_denied")]
    public async Task FailureStatusesMapToStableErrorCodes(HttpStatusCode status, string code)
    {
        var account = Account;
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestWriteClient(
            Probe(status)).CommentAsync(account, Scope(account), Revision, "body", TestContext.Current.CancellationToken));
        Assert.Equal(code, failure.ErrorCode);
        Assert.DoesNotContain(account.AccessToken, failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RateLimitedForbiddenIsClassifiedByHeaders()
    {
        var account = Account;
        var probe = new SourceAccountHttpProbe { RespondAsync = (_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Forbidden) { Headers = { { "X-RateLimit-Remaining", "0" } } }) };
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestWriteClient(probe)
            .CommentAsync(account, Scope(account), Revision, "body", TestContext.Current.CancellationToken));
        Assert.Equal("source_rate_limited", failure.ErrorCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4097)]
    public async Task BodyBoundsAreEnforcedBeforeAnyRequest(int length)
    {
        var account = Account;
        var probe = Probe(HttpStatusCode.Created, Created(account));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestWriteClient(probe)
            .CommentAsync(account, Scope(account), Revision, new string('x', length), TestContext.Current.CancellationToken));
        Assert.Equal("invalid_arguments", failure.ErrorCode);
        Assert.Equal(0, probe.RequestCount);
    }

    [Theory]
    [InlineData("{}", "missing id")]
    [InlineData("{\"id\":0}", "zero id")]
    [InlineData("{\"id\":-4}", "negative id")]
    [InlineData("{\"id\":\"11\"}", "string id")]
    [InlineData("{\"id\":11,\"issue_url\":\"https://enterprise.example.test/api/v3/repos/other/repo/issues/7\"}", "wrong owner")]
    [InlineData("{\"id\":11,\"issue_url\":\"https://enterprise.example.test/api/v3/repos/fixture/repo/issues/8\"}", "wrong issue")]
    [InlineData("{", "malformed")]
    [InlineData("[]", "array")]
    public async Task CreatedResponsesMustProveIdentityAndOwnership(string body, string scenario)
    {
        var account = Account;
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestWriteClient(
            Probe(HttpStatusCode.Created, body)).CommentAsync(account, Scope(account), Revision, "body", TestContext.Current.CancellationToken));
        Assert.True("source_invalid_response" == failure.ErrorCode, scenario);
    }

    [Fact]
    public async Task ScopeAccountAndRevisionMismatchesNeverContactTransport()
    {
        var account = Account;
        var scope = Scope(account);
        var probe = Probe(HttpStatusCode.Created, Created(account));
        var client = new GitHubPullRequestWriteClient(probe);
        var token = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<SourceReviewException>(() => client.CommentAsync(account, Scope(account, false), Revision, "b", token));
        await Assert.ThrowsAsync<SourceReviewException>(() => client.CommentAsync(account, scope with { AccountId = Guid.NewGuid() }, Revision, "b", token));
        await Assert.ThrowsAsync<SourceReviewException>(() => client.CommentAsync(account, scope with { AccountRevision = 2 }, Revision, "b", token));
        await Assert.ThrowsAsync<SourceReviewException>(() => client.CommentAsync(account.WithRevision(2), scope, Revision, "b", token));
        await Assert.ThrowsAsync<ArgumentException>(() => client.CommentAsync(account, scope with { Owner = "../escape" }, Revision, "b", token));
        await Assert.ThrowsAsync<SourceReviewException>(() => client.CommentAsync(account, scope, Revision with { HeadRevision = "short" }, "b", token));
        Assert.Equal(0, probe.RequestCount);
    }

    [Fact]
    public async Task ReconcileReadFindsOnlyAnExactBodyMatch()
    {
        var account = Account;
        var probe = new SourceAccountHttpProbe { RespondAsync = (request, token) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(new Uri(account.ApiEndpoint,
                "repos/fixture/repo/issues/7/comments?per_page=5&sort=created&direction=desc"), request.RequestUri);
            var list = "[" + string.Join(',', new[]
            {
                PullRequestTestData.Comment(id: 9, body: "needle"),
                PullRequestTestData.Comment(id: 10, body: "needle "),
                PullRequestTestData.Comment(id: 11, body: " needle")
            }) + "]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(list.Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal)) });
        } };

        var client = new GitHubPullRequestWriteClient(probe);
        var token = TestContext.Current.CancellationToken;
        Assert.Equal(9, await client.FindCommentAsync(account, Scope(account), 7, "needle", token));
        Assert.Equal(10, await client.FindCommentAsync(account, Scope(account), 7, "needle ", token));
        Assert.Null(await client.FindCommentAsync(account, Scope(account), 7, "needle  ", token));
    }

    [Fact]
    public async Task ReconcileReadReturnsNullWithoutAMatch()
    {
        var account = Account;
        var probe = Probe(HttpStatusCode.OK, "[]");
        Assert.Null(await new GitHubPullRequestWriteClient(probe)
            .FindCommentAsync(account, Scope(account), 7, "needle", TestContext.Current.CancellationToken));
    }
}
