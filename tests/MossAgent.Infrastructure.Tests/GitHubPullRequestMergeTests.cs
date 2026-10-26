using System.Net;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitHubPullRequestMergeTests
{
    private static SourceAccount Account => SourceAccountValidationTests.Create().WithRevision(1);
    private static PullRequestRevision Revision => new(7, new string('a', 40), new string('b', 40));
    private static SourceRepositoryScope Scope(SourceAccount account, bool write = true) =>
        new(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", write);
    private static string MergeResponse(string? sha = null, bool merged = true) =>
        JsonSerializer.Serialize(new { sha = sha ?? new string('c', 40), merged, message = "Pull Request merged" });
    private static string Detail(SourceAccount account, bool merged = true, string? head = null,
        string? baseSha = null, string? mergeCommit = null) =>
        PullRequestTestData.Merged(merged: merged, head: head, baseSha: baseSha, mergeCommit: mergeCommit)
            .Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal);
    private static SourceAccountHttpProbe Probe(HttpStatusCode status, string body = "{}") => new()
    { RespondAsync = (_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }) };

    [Theory]
    [InlineData(PullRequestMergeMethod.Merge, "merge")]
    [InlineData(PullRequestMergeMethod.Squash, "squash")]
    [InlineData(PullRequestMergeMethod.Rebase, "rebase")]
    public async Task MergePutsThePinnedHeadAndChosenMethodOnce(PullRequestMergeMethod method, string wire)
    {
        var account = Account;
        HttpRequestMessage? sent = null;
        string? payload = null;
        var probe = new SourceAccountHttpProbe { RespondAsync = async (request, token) =>
        {
            sent = request;
            payload = await request.Content!.ReadAsStringAsync(token);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(MergeResponse()) };
        } };

        var sha = await new GitHubPullRequestWriteClient(probe)
            .MergeAsync(account, Scope(account), Revision, method, TestContext.Current.CancellationToken);

        Assert.Equal(new string('c', 40), sha);
        Assert.Equal(HttpMethod.Put, sent!.Method);
        Assert.Equal(new Uri(account.ApiEndpoint, "repos/fixture/repo/pulls/7/merge"), sent.RequestUri);
        Assert.Equal(account.AccessToken, sent.Headers.Authorization!.Parameter);
        Assert.Equal("Bearer", sent.Headers.Authorization.Scheme);
        Assert.Contains(sent.Headers.Accept, value => value.MediaType == "application/vnd.github+json");
        Assert.Equal("2022-11-28", Assert.Single(sent.Headers.GetValues("X-GitHub-Api-Version")));
        var body = JsonDocument.Parse(payload!).RootElement;
        Assert.Equal(Revision.HeadRevision, body.GetProperty("sha").GetString());
        Assert.Equal(wire, body.GetProperty("merge_method").GetString());
        Assert.Equal(2, body.EnumerateObject().Count());
        Assert.Equal(1, probe.RequestCount);
    }

    [Fact]
    public async Task UnknownMergeMethodsAreRejectedBeforeAnyRequest()
    {
        var account = Account;
        var probe = Probe(HttpStatusCode.OK, MergeResponse());
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestWriteClient(probe)
            .MergeAsync(account, Scope(account), Revision, (PullRequestMergeMethod)99,
                TestContext.Current.CancellationToken));
        Assert.Equal("invalid_arguments", failure.ErrorCode);
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
            Probe(status)).MergeAsync(account, Scope(account), Revision, PullRequestMergeMethod.Merge,
            TestContext.Current.CancellationToken));
        Assert.Equal(code, failure.ErrorCode);
        Assert.DoesNotContain(account.AccessToken, failure.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}", "missing fields")]
    [InlineData("{\"merged\":true}", "missing sha")]
    [InlineData("{\"sha\":\"cccc\",\"merged\":true}", "short sha")]
    [InlineData("{\"sha\":\"zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz\",\"merged\":true}", "non-hex sha")]
    [InlineData("{\"sha\":42,\"merged\":true}", "non-string sha")]
    [InlineData("{\"sha\":\"cccccccccccccccccccccccccccccccccccccccc\",\"merged\":false}", "not merged")]
    [InlineData("{\"sha\":\"cccccccccccccccccccccccccccccccccccccccc\",\"merged\":\"yes\"}", "string merged")]
    [InlineData("{", "malformed")]
    [InlineData("[]", "array")]
    public async Task MergeResponsesMustProveMergedAndReturnACommitSha(string body, string scenario)
    {
        var account = Account;
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestWriteClient(
            Probe(HttpStatusCode.OK, body)).MergeAsync(account, Scope(account), Revision,
            PullRequestMergeMethod.Squash, TestContext.Current.CancellationToken));
        Assert.True("source_invalid_response" == failure.ErrorCode, scenario);
    }

    [Fact]
    public async Task MergeScopeAccountAndRevisionMismatchesNeverContactTransport()
    {
        var account = Account;
        var scope = Scope(account);
        var probe = Probe(HttpStatusCode.OK, MergeResponse());
        var client = new GitHubPullRequestWriteClient(probe);
        var token = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.MergeAsync(account, Scope(account, false), Revision, PullRequestMergeMethod.Merge, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.MergeAsync(account, scope with { AccountId = Guid.NewGuid() }, Revision, PullRequestMergeMethod.Merge, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.MergeAsync(account, scope with { AccountRevision = 2 }, Revision, PullRequestMergeMethod.Merge, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.MergeAsync(account.WithRevision(2), scope, Revision, PullRequestMergeMethod.Merge, token));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.MergeAsync(account, scope with { Owner = "../escape" }, Revision, PullRequestMergeMethod.Merge, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.MergeAsync(account, scope, Revision with { HeadRevision = "short" }, PullRequestMergeMethod.Merge, token));
        Assert.Equal(0, probe.RequestCount);
    }

    [Fact]
    public async Task ReconcileReadGetsTheDetailAndReturnsTheMergeCommitSha()
    {
        var account = Account;
        HttpRequestMessage? sent = null;
        var probe = new SourceAccountHttpProbe { RespondAsync = (request, _) =>
        {
            sent = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(Detail(account, baseSha: new string('d', 40), mergeCommit: new string('c', 40))) });
        } };
        var sha = await new GitHubPullRequestWriteClient(probe)
            .MergedAsync(account, Scope(account), Revision, TestContext.Current.CancellationToken);
        Assert.Equal(new string('c', 40), sha);
        Assert.Equal(HttpMethod.Get, sent!.Method);
        Assert.Equal(new Uri(account.ApiEndpoint, "repos/fixture/repo/pulls/7"), sent.RequestUri);
        Assert.Equal(1, probe.RequestCount);
    }

    [Fact]
    public async Task ReconcileReadReturnsNullUnlessMergedUnderTheCapturedHead()
    {
        var account = Account;
        var client = new GitHubPullRequestWriteClient(Probe(HttpStatusCode.OK, Detail(account, merged: false)));
        var token = TestContext.Current.CancellationToken;
        Assert.Null(await client.MergedAsync(account, Scope(account), Revision, token));

        // merged == true under a different head means a newer commit merged, not our write.
        client = new GitHubPullRequestWriteClient(Probe(HttpStatusCode.OK,
            Detail(account, head: new string('e', 40), mergeCommit: new string('c', 40))));
        Assert.Null(await client.MergedAsync(account, Scope(account), Revision, token));

        // The base sha advancing after a landed merge must not defeat reconciliation.
        client = new GitHubPullRequestWriteClient(Probe(HttpStatusCode.OK,
            Detail(account, baseSha: new string('d', 40), mergeCommit: new string('c', 40))));
        Assert.NotNull(await client.MergedAsync(account, Scope(account), Revision, token));
    }

    [Fact]
    public async Task ReconcileReadReturnsEmptyShaWhenTheServerOmitsTheMergeCommit()
    {
        var account = Account;
        var sha = await new GitHubPullRequestWriteClient(Probe(HttpStatusCode.OK,
                Detail(account, mergeCommit: null)))
            .MergedAsync(account, Scope(account), Revision, TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, sha);
    }

    [Theory]
    [InlineData("{}", "missing number")]
    [InlineData("{\"number\":8}", "wrong pull")]
    [InlineData("{\"number\":7,\"merged\":true}", "missing head")]
    [InlineData("{\"number\":7,\"merged\":\"yes\",\"head\":{\"sha\":\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"}}", "string merged")]
    [InlineData("{\"number\":7,\"merged\":true,\"head\":{\"sha\":\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"},\"merge_commit_sha\":\"nope\"}", "bad merge sha")]
    [InlineData("{", "malformed")]
    [InlineData("[]", "array")]
    public async Task ReconcileResponsesMustProveOwnershipAndShape(string body, string scenario)
    {
        var account = Account;
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestWriteClient(
            Probe(HttpStatusCode.OK, body)).MergedAsync(account, Scope(account), Revision,
            TestContext.Current.CancellationToken));
        Assert.True("source_invalid_response" == failure.ErrorCode, scenario);
    }

    [Fact]
    public async Task ReconcileRejectsForgedUrlsBeforeTrustingMerged()
    {
        var account = Account;
        var body = Detail(account).Replace(account.ApiEndpoint.AbsoluteUri,
            "https://attacker.invalid/", StringComparison.Ordinal);
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestWriteClient(
            Probe(HttpStatusCode.OK, body)).MergedAsync(account, Scope(account), Revision,
            TestContext.Current.CancellationToken));
        Assert.Equal("source_invalid_response", failure.ErrorCode);
    }

    [Fact]
    public async Task ReconcileReadOnlyAndForgedScopesNeverContactTransport()
    {
        var account = Account;
        var scope = Scope(account);
        var probe = Probe(HttpStatusCode.OK, Detail(account));
        var client = new GitHubPullRequestWriteClient(probe);
        var token = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.MergedAsync(account, Scope(account, false), Revision, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.MergedAsync(account, scope with { AccountId = Guid.NewGuid() }, Revision, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.MergedAsync(account, scope, Revision with { HeadRevision = "short" }, token));
        Assert.Equal(0, probe.RequestCount);
    }
}
