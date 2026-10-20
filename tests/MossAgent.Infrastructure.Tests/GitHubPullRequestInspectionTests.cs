using System.Net;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitHubPullRequestInspectionTests
{
    private static SourceAccount Account => SourceAccountValidationTests.Create().WithRevision(1);
    private static PullRequestRevision Revision => new(7, new string('a', 40), new string('b', 40));
    private static SourceRepositoryScope Scope(SourceAccount account) => new(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo");
    private static SourceAccountHttpProbe Probe(string body) => new()
    { RespondAsync = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }) };

    [Theory]
    [InlineData("@@ -1 +1 @@\n-old\n+new", 1, 1, PullRequestPatchState.Available)]
    [InlineData("@@ -1,2 +1,2 @@\n context\n-old\n+new\n", 1, 1, PullRequestPatchState.Available)]
    [InlineData("@@ -0,0 +1 @@\n+new", 1, 0, PullRequestPatchState.Available)]
    [InlineData("@@ -1 +0,0 @@\n-old", 0, 1, PullRequestPatchState.Available)]
    [InlineData("@@ -1 +1 @@\n-old\n+new\n\\ No newline at end of file", 1, 1, PullRequestPatchState.Available)]
    [InlineData("@@ -1 +1 @@\n-old", 1, 1, PullRequestPatchState.Incomplete)]
    [InlineData("@@ -1 +1 @@\n-old\n+new", 2, 1, PullRequestPatchState.Incomplete)]
    [InlineData("@@ -1,999999999999999 +1 @@\n-old\n+new", 1, 1, PullRequestPatchState.Incomplete)]
    [InlineData("not a diff", 1, 1, PullRequestPatchState.Incomplete)]
    [InlineData("@@ -999999999999 +1 @@\n-old\n+new", 1, 1, PullRequestPatchState.Incomplete)]
    [InlineData("@@ -2147483647 +1 @@\n-old\n+new", 1, 1, PullRequestPatchState.Incomplete)]
    [InlineData("@@ -1 +1 @@\n-old\n+new\n@@ -1 +1 @@\n-old\n+new", 2, 2, PullRequestPatchState.Incomplete)]
    [InlineData("", 0, 0, PullRequestPatchState.Available)]
    [InlineData(null, 0, 0, PullRequestPatchState.Missing)]
    public async Task PatchCompletenessIsExplicit(string? patch, int additions, int deletions, PullRequestPatchState expected)
    {
        var account = Account;
        var probe = Probe($"[{PullRequestTestData.File(patch, additions: additions, deletions: deletions)}]");
        var result = await new GitHubPullRequestInspectionClient(probe).FilesAsync(account, Scope(account), Revision, 1, TestContext.Current.CancellationToken);
        var file = Assert.Single(result.Items);
        Assert.Equal(expected, file.PatchState);
        Assert.Equal(patch ?? string.Empty, file.Patch);
        Assert.Equal(1, probe.DisposedHandlers);
    }

    [Fact]
    public async Task FileAndPageBudgetsKeepMetadataAndExposeLimits()
    {
        var account = Account;
        var files = new[] { PullRequestTestData.File(new string('x', 32769), "large") }
            .Concat(Enumerable.Range(1, 19).Select(index => PullRequestTestData.File(new string('x', 32768), $"file-{index}")));
        var probe = Probe("[" + string.Join(',', files) + "]");
        var result = await new GitHubPullRequestInspectionClient(probe).FilesAsync(account, Scope(account), Revision, 150, TestContext.Current.CancellationToken);
        Assert.Equal(20, result.Items.Count);
        Assert.Equal(65536, result.Items.Sum(file => file.Patch.Length));
        Assert.Equal(PullRequestPatchState.TooLarge, result.Items[0].PatchState);
        Assert.Empty(result.Items[0].Patch);
        Assert.True(result.IsLimited);
        Assert.False(result.HasNextPage);
    }

    [Theory]
    [InlineData("conversation", "issues/7/comments")]
    [InlineData("inline", "pulls/7/comments")]
    [InlineData("runs", "commits/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/check-runs")]
    [InlineData("statuses", "commits/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/status")]
    public async Task FixedEndpointsReadBothCommentAndCheckKindsWithoutFollowingUrls(string kind, string endpoint)
    {
        var account = Account;
        var probe = Probe(kind is "runs" or "statuses" ? PullRequestTestData.Checks(kind == "runs")
            : $"[{PullRequestTestData.Comment()}]".Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal));
        var respond = probe.RespondAsync;
        probe.RespondAsync = (request, token) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(new Uri(account.ApiEndpoint, $"repos/fixture/repo/{endpoint}?per_page=20&page=2"), request.RequestUri);
            Assert.Equal(account.AccessToken, request.Headers.Authorization!.Parameter);
            return respond(request, token);
        };
        var client = new GitHubPullRequestInspectionClient(probe);
        if (kind is "runs" or "statuses") Assert.Single((await client.ChecksAsync(account, Scope(account), Revision, kind, 2, TestContext.Current.CancellationToken)).Items);
        else Assert.Single((await client.CommentsAsync(account, Scope(account), Revision, kind, 2, TestContext.Current.CancellationToken)).Items);
        Assert.Equal(1, probe.RequestCount);
        Assert.Equal(1, probe.DisposedHandlers);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChecksFromAnotherCommitAreNeverPresented(bool runs)
    {
        var account = Account;
        var probe = Probe(PullRequestTestData.Checks(runs, new string('c', 40)));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestInspectionClient(probe)
            .ChecksAsync(account, Scope(account), Revision, runs ? "runs" : "statuses", 1, TestContext.Current.CancellationToken));
        Assert.Equal("source_revision_changed", failure.ErrorCode);
    }

    [Theory]
    [InlineData("secret")]
    [InlineData("duplicate")]
    [InlineData("control")]
    [InlineData("malformed")]
    [InlineData("oversized")]
    public async Task InvalidFilePagesAreRejectedWithoutCredentialReflection(string kind)
    {
        var account = Account;
        var file = PullRequestTestData.File(path: kind == "secret" ? account.AccessToken : kind == "control" ? "bad\u0001" : "same");
        var body = kind switch { "duplicate" => $"[{file},{file}]", "malformed" => "{", "oversized" => new string('x', 2 * 1024 * 1024 + 1), _ => $"[{file}]" };
        var probe = Probe(body);
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestInspectionClient(probe)
            .FilesAsync(account, Scope(account), Revision, 1, TestContext.Current.CancellationToken));
        Assert.Equal("source_invalid_response", failure.ErrorCode);
        Assert.DoesNotContain(account.AccessToken, failure.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("inline")]
    [InlineData("conversation")]
    public async Task ForeignOrOversizedCommentsAreNotSilentlyTruncated(string kind)
    {
        var account = Account;
        foreach (var comment in new[] { PullRequestTestData.Comment().Replace("fixture/repo", "other/repo", StringComparison.Ordinal),
            PullRequestTestData.Comment(body: new string('x', 16385)), PullRequestTestData.Comment(body: account.AccessToken) })
        {
            var probe = Probe($"[{comment}]".Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal));
            Assert.Equal("source_invalid_response", (await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestInspectionClient(probe)
                .CommentsAsync(account, Scope(account), Revision, kind, 1, TestContext.Current.CancellationToken))).ErrorCode);
        }
    }

    [Fact]
    public async Task InvalidScopeRevisionKindAndPageNeverContactTransport()
    {
        var account = Account;
        var scope = Scope(account);
        var probe = Probe("[]");
        var client = new GitHubPullRequestInspectionClient(probe);
        var token = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<SourceReviewException>(() => client.FilesAsync(account, scope with { AccountId = Guid.NewGuid() }, Revision, 1, token));
        await Assert.ThrowsAsync<SourceReviewException>(() => client.FilesAsync(account, scope, Revision with { HeadRevision = "../escape" }, 1, token));
        await Assert.ThrowsAsync<SourceReviewException>(() => client.FilesAsync(account, scope, Revision, 151, token));
        await Assert.ThrowsAsync<SourceReviewException>(() => client.CommentsAsync(account, scope, Revision, "https://attacker.invalid", 1, token));
        await Assert.ThrowsAsync<SourceReviewException>(() => client.ChecksAsync(account, scope, Revision, "runs", 1001, token));
        Assert.Equal(0, probe.RequestCount);
    }
}
