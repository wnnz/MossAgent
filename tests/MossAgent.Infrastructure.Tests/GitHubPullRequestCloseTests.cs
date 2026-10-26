using System.Net;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitHubPullRequestCloseTests
{
    private static SourceAccount Account => SourceAccountValidationTests.Create().WithRevision(1);
    private static PullRequestRevision Revision => new(7, new string('a', 40), new string('b', 40));
    private static SourceRepositoryScope Scope(SourceAccount account, bool write = true) =>
        new(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", write);
    private static string Closed(SourceAccount account, string state = "closed") =>
        PullRequestTestData.Item(state: state)
            .Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal);
    private static SourceAccountHttpProbe Probe(HttpStatusCode status, string body = "{}") => new()
    { RespondAsync = (_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }) };

    [Fact]
    public async Task ClosePatchesThePullRequestOnceWithOnlyTheStateField()
    {
        var account = Account;
        HttpRequestMessage? sent = null;
        string? payload = null;
        var probe = new SourceAccountHttpProbe { RespondAsync = async (request, token) =>
        {
            sent = request;
            payload = await request.Content!.ReadAsStringAsync(token);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Closed(account)) };
        } };

        var state = await new GitHubPullRequestWriteClient(probe)
            .CloseAsync(account, Scope(account), Revision, TestContext.Current.CancellationToken);

        Assert.Equal("closed", state);
        Assert.Equal(HttpMethod.Patch, sent!.Method);
        Assert.Equal(new Uri(account.ApiEndpoint, "repos/fixture/repo/pulls/7"), sent.RequestUri);
        Assert.Equal(account.AccessToken, sent.Headers.Authorization!.Parameter);
        Assert.Equal("Bearer", sent.Headers.Authorization.Scheme);
        Assert.Contains(sent.Headers.Accept, value => value.MediaType == "application/vnd.github+json");
        Assert.Equal("2022-11-28", Assert.Single(sent.Headers.GetValues("X-GitHub-Api-Version")));
        var body = JsonDocument.Parse(payload!).RootElement;
        Assert.Equal("closed", body.GetProperty("state").GetString());
        Assert.Equal("state", Assert.Single(body.EnumerateObject()).Name);
        Assert.Equal(1, probe.RequestCount);
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
            Probe(status)).CloseAsync(account, Scope(account), Revision, TestContext.Current.CancellationToken));
        Assert.Equal(code, failure.ErrorCode);
        Assert.DoesNotContain(account.AccessToken, failure.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}", "missing number")]
    [InlineData("{\"number\":\"7\",\"state\":\"closed\"}", "string number")]
    [InlineData("{\"number\":8,\"state\":\"closed\"}", "wrong pull")]
    [InlineData("{\"number\":7}", "missing state")]
    [InlineData("{\"number\":7,\"state\":42}", "non-string state")]
    [InlineData("{\"number\":7,\"state\":\"closed\",\"url\":\"https://enterprise.example.test/api/v3/repos/other/repo/pulls/7\"}", "wrong owner")]
    [InlineData("{\"number\":7,\"state\":\"closed\",\"url\":\"https://enterprise.example.test/api/v3/repos/fixture/repo/pulls/8\"}", "forged url pull")]
    [InlineData("{", "malformed")]
    [InlineData("[]", "array")]
    public async Task CloseResponsesMustProveIdentityOwnershipAndState(string body, string scenario)
    {
        var account = Account;
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestWriteClient(
            Probe(HttpStatusCode.OK, body)).CloseAsync(account, Scope(account), Revision,
            TestContext.Current.CancellationToken));
        Assert.True("source_invalid_response" == failure.ErrorCode, scenario);
    }

    [Fact]
    public async Task ScopeAccountAndRevisionMismatchesNeverContactTransport()
    {
        var account = Account;
        var scope = Scope(account);
        var probe = Probe(HttpStatusCode.OK, Closed(account));
        var client = new GitHubPullRequestWriteClient(probe);
        var token = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.CloseAsync(account, Scope(account, false), Revision, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.CloseAsync(account, scope with { AccountId = Guid.NewGuid() }, Revision, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.CloseAsync(account, scope with { AccountRevision = 2 }, Revision, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.CloseAsync(account.WithRevision(2), scope, Revision, token));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.CloseAsync(account, scope with { Owner = "../escape" }, Revision, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.CloseAsync(account, scope, Revision with { HeadRevision = "short" }, token));
        Assert.Equal(0, probe.RequestCount);
    }

    [Fact]
    public async Task TheClientReturnsTheStatedStateForTheWriterToJudge()
    {
        var account = Account;
        var state = await new GitHubPullRequestWriteClient(Probe(HttpStatusCode.OK, Closed(account, "open")))
            .CloseAsync(account, Scope(account), Revision, TestContext.Current.CancellationToken);
        Assert.Equal("open", state);
    }
}
