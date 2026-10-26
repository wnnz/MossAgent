using System.Net;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitHubPullRequestReviewWriteTests
{
    private static SourceAccount Account => SourceAccountValidationTests.Create().WithRevision(1);
    private static PullRequestRevision Revision => new(7, new string('a', 40), new string('b', 40));
    private static SourceRepositoryScope Scope(SourceAccount account, bool write = true) =>
        new(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", write);
    private static string Created(SourceAccount account, long id = 99, string state = "APPROVED") =>
        PullRequestTestData.Review(id: id, state: state, body: null)
            .Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal);
    private static SourceAccountHttpProbe Probe(HttpStatusCode status, string body = "{}") => new()
    { RespondAsync = (_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }) };

    [Fact]
    public async Task SubmitPinsTheHeadCommitAndParsesTheCreatedReview()
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
            .SubmitReviewAsync(account, Scope(account), Revision, PullRequestReviewDecision.Approve,
                "looks good", TestContext.Current.CancellationToken);

        Assert.Equal(99, id);
        Assert.Equal(HttpMethod.Post, sent!.Method);
        Assert.Equal(new Uri(account.ApiEndpoint, "repos/fixture/repo/pulls/7/reviews"), sent.RequestUri);
        Assert.Equal(account.AccessToken, sent.Headers.Authorization!.Parameter);
        Assert.Equal("Bearer", sent.Headers.Authorization.Scheme);
        Assert.Contains(sent.Headers.Accept, value => value.MediaType == "application/vnd.github+json");
        Assert.Equal("2022-11-28", Assert.Single(sent.Headers.GetValues("X-GitHub-Api-Version")));
        var body = JsonDocument.Parse(payload!).RootElement;
        Assert.Equal("APPROVE", body.GetProperty("event").GetString());
        Assert.Equal(new string('b', 40), body.GetProperty("commit_id").GetString());
        Assert.Equal("looks good", body.GetProperty("body").GetString());
        Assert.Equal(1, probe.RequestCount);
    }

    [Theory]
    [InlineData(PullRequestReviewDecision.Approve, "APPROVE")]
    [InlineData(PullRequestReviewDecision.RequestChanges, "REQUEST_CHANGES")]
    [InlineData(PullRequestReviewDecision.Comment, "COMMENT")]
    public async Task DecisionMapsToTheClosedGitHubEventSet(PullRequestReviewDecision decision, string expected)
    {
        var account = Account;
        string? payload = null;
        var probe = new SourceAccountHttpProbe { RespondAsync = async (request, token) =>
        {
            payload = await request.Content!.ReadAsStringAsync(token);
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(Created(account)) };
        } };
        await new GitHubPullRequestWriteClient(probe).SubmitReviewAsync(account, Scope(account), Revision,
            decision, null, TestContext.Current.CancellationToken);
        Assert.Equal(expected, JsonDocument.Parse(payload!).RootElement.GetProperty("event").GetString());
        Assert.False(JsonDocument.Parse(payload!).RootElement.TryGetProperty("body", out _), "null body must be omitted");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task MissingOrEmptyBodyIsOmittedFromThePayload(string? body)
    {
        var account = Account;
        string? payload = null;
        var probe = new SourceAccountHttpProbe { RespondAsync = async (request, token) =>
        {
            payload = await request.Content!.ReadAsStringAsync(token);
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(Created(account)) };
        } };
        await new GitHubPullRequestWriteClient(probe).SubmitReviewAsync(account, Scope(account), Revision,
            PullRequestReviewDecision.Comment, body, TestContext.Current.CancellationToken);
        Assert.False(JsonDocument.Parse(payload!).RootElement.TryGetProperty("body", out _));
    }

    [Fact]
    public async Task UnknownDecisionAndOversizedBodyNeverContactTransport()
    {
        var account = Account;
        var probe = Probe(HttpStatusCode.Created, Created(account));
        var client = new GitHubPullRequestWriteClient(probe);
        var token = TestContext.Current.CancellationToken;
        Assert.Equal("invalid_arguments", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.SubmitReviewAsync(account, Scope(account), Revision, (PullRequestReviewDecision)99, null, token))).ErrorCode);
        Assert.Equal("invalid_arguments", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.SubmitReviewAsync(account, Scope(account), Revision, PullRequestReviewDecision.Approve,
                new string('x', 4097), token))).ErrorCode);
        Assert.Equal("invalid_arguments", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.FindReviewAsync(account, Scope(account), Revision, (PullRequestReviewDecision)99, null, token))).ErrorCode);
        Assert.Equal(0, probe.RequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "source_auth_failed")]
    [InlineData(HttpStatusCode.Forbidden, "source_forbidden")]
    [InlineData(HttpStatusCode.NotFound, "source_not_found")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "source_write_rejected")]
    [InlineData(HttpStatusCode.InternalServerError, "source_unavailable")]
    public async Task FailureStatusesMapToStableErrorCodes(HttpStatusCode status, string code)
    {
        var account = Account;
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestWriteClient(
            Probe(status)).SubmitReviewAsync(account, Scope(account), Revision, PullRequestReviewDecision.Approve,
            null, TestContext.Current.CancellationToken));
        Assert.Equal(code, failure.ErrorCode);
        Assert.DoesNotContain(account.AccessToken, failure.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}", "missing id")]
    [InlineData("{\"id\":0}", "zero id")]
    [InlineData("{\"id\":\"11\"}", "string id")]
    [InlineData("{\"id\":11,\"pull_request_url\":\"https://enterprise.example.test/api/v3/repos/other/repo/pulls/7\"}", "wrong owner")]
    [InlineData("{\"id\":11,\"pull_request_url\":\"https://enterprise.example.test/api/v3/repos/fixture/repo/pulls/8\"}", "wrong pull")]
    [InlineData("{", "malformed")]
    [InlineData("[]", "array")]
    public async Task CreatedResponsesMustProveIdentityAndOwnership(string body, string scenario)
    {
        var account = Account;
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestWriteClient(
            Probe(HttpStatusCode.Created, body)).SubmitReviewAsync(account, Scope(account), Revision,
            PullRequestReviewDecision.Approve, null, TestContext.Current.CancellationToken));
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
        await Assert.ThrowsAsync<SourceReviewException>(() => client.SubmitReviewAsync(account,
            Scope(account, false), Revision, PullRequestReviewDecision.Approve, null, token));
        await Assert.ThrowsAsync<SourceReviewException>(() => client.SubmitReviewAsync(account,
            scope with { AccountId = Guid.NewGuid() }, Revision, PullRequestReviewDecision.Approve, null, token));
        await Assert.ThrowsAsync<SourceReviewException>(() => client.SubmitReviewAsync(account,
            scope with { AccountRevision = 2 }, Revision, PullRequestReviewDecision.Approve, null, token));
        await Assert.ThrowsAsync<SourceReviewException>(() => client.SubmitReviewAsync(account.WithRevision(2),
            scope, Revision, PullRequestReviewDecision.Approve, null, token));
        await Assert.ThrowsAsync<ArgumentException>(() => client.SubmitReviewAsync(account,
            scope with { Owner = "../escape" }, Revision, PullRequestReviewDecision.Approve, null, token));
        await Assert.ThrowsAsync<SourceReviewException>(() => client.SubmitReviewAsync(account, scope,
            Revision with { HeadRevision = "short" }, PullRequestReviewDecision.Approve, null, token));
        Assert.Equal(0, probe.RequestCount);
    }

    [Fact]
    public async Task ReconcileMatchesOnlyOnStateCommitAndBody()
    {
        var account = Account;
        var head = new string('b', 40);
        var probe = new SourceAccountHttpProbe { RespondAsync = (request, token) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(new Uri(account.ApiEndpoint, "repos/fixture/repo/pulls/7/reviews?per_page=20&page=1"), request.RequestUri);
            var list = "[" + string.Join(',', new[]
            {
                PullRequestTestData.Review(id: 31, state: "CHANGES_REQUESTED", body: "fix this", commit: head),
                PullRequestTestData.Review(id: 32, state: "APPROVED", body: "other head", commit: new string('c', 40)),
                PullRequestTestData.Review(id: 33, state: "APPROVED", body: "other body", commit: head),
                PullRequestTestData.Review(id: 34, state: "APPROVED", body: "needle", commit: head)
            }) + "]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(list.Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal)) });
        } };
        var client = new GitHubPullRequestWriteClient(probe);
        var token = TestContext.Current.CancellationToken;
        Assert.Equal(34, await client.FindReviewAsync(account, Scope(account), Revision,
            PullRequestReviewDecision.Approve, "needle", token));
        Assert.Null(await client.FindReviewAsync(account, Scope(account), Revision,
            PullRequestReviewDecision.Comment, "fix this", token));
    }

    [Fact]
    public async Task ReconcileWalksAscendingPagesToTheLastReview()
    {
        var account = Account;
        var head = new string('b', 40);
        var probe = new SourceAccountHttpProbe { RespondAsync = (request, token) =>
        {
            var list = request.RequestUri!.Query.Contains("page=1")
                ? "[" + string.Join(',', Enumerable.Range(1, 20).Select(index =>
                    PullRequestTestData.Review(id: index, state: "COMMENTED", body: "older", commit: head))) + "]"
                : "[" + string.Join(',', new[]
                    {
                        PullRequestTestData.Review(id: 40, state: "APPROVED", body: "mid", commit: head),
                        PullRequestTestData.Review(id: 41, state: "APPROVED", body: "needle", commit: head)
                    }) + "]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(list.Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal)) });
        } };
        var client = new GitHubPullRequestWriteClient(probe);
        var token = TestContext.Current.CancellationToken;
        Assert.Equal(41, await client.FindReviewAsync(account, Scope(account), Revision,
            PullRequestReviewDecision.Approve, "needle", token));
        Assert.Equal(2, probe.RequestCount);
        Assert.Null(await client.FindReviewAsync(account, Scope(account), Revision,
            PullRequestReviewDecision.Comment, "needle", token));
    }

    [Theory]
    [InlineData(null, "remote-null")]
    [InlineData(null, "remote-empty")]
    [InlineData("", "remote-empty")]
    public async Task ReconcileNormalizesAbsentBodyOnBothSides(string? sent, string remote)
    {
        var account = Account;
        var head = new string('b', 40);
        var probe = Probe(HttpStatusCode.OK, "[" + PullRequestTestData.Review(id: 35, state: "COMMENTED",
            body: remote == "remote-empty" ? "" : null, commit: head)
            .Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal) + "]");
        Assert.Equal(35, await new GitHubPullRequestWriteClient(probe).FindReviewAsync(account, Scope(account),
            Revision, PullRequestReviewDecision.Comment, sent, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReconcileRejectsAStatedBodyMismatch()
    {
        var account = Account;
        var probe = Probe(HttpStatusCode.OK, "[" + PullRequestTestData.Review(id: 36, state: "COMMENTED",
            body: "different", commit: new string('b', 40))
            .Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal) + "]");
        Assert.Null(await new GitHubPullRequestWriteClient(probe).FindReviewAsync(account, Scope(account),
            Revision, PullRequestReviewDecision.Comment, "", TestContext.Current.CancellationToken));
    }
}
