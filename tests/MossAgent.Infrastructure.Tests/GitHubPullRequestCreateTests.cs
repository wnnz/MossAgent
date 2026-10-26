using System.Net;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitHubPullRequestCreateTests
{
    private static SourceAccount Account => SourceAccountValidationTests.Create().WithRevision(1);
    private static SourceRepositoryScope Scope(SourceAccount account, bool write = true) =>
        new(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", write);
    private static string Created(SourceAccount account, int number = 9, string title = "Add feature",
        string? headRef = "feature", string? baseRef = "main", string? head = null, string? baseSha = null) =>
        PullRequestTestData.CreatedItem(number, title: title, headRef: headRef, baseRef: baseRef,
                head: head, baseSha: baseSha)
            .Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal);
    private static SourceAccountHttpProbe Probe(HttpStatusCode status, string body = "{}") => new()
    { RespondAsync = (_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }) };

    [Fact]
    public async Task CreatePostsTheExactPayloadOnce()
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

        var result = await new GitHubPullRequestWriteClient(probe)
            .CreateAsync(account, Scope(account), "Add feature", null, "feature", "main", draft: true,
                TestContext.Current.CancellationToken);

        Assert.Equal(9, result.Number);
        Assert.Equal(new string('b', 40), result.HeadRevision);
        Assert.Equal(new string('a', 40), result.BaseRevision);
        Assert.Equal(HttpMethod.Post, sent!.Method);
        Assert.Equal(new Uri(account.ApiEndpoint, "repos/fixture/repo/pulls"), sent.RequestUri);
        Assert.Equal(account.AccessToken, sent.Headers.Authorization!.Parameter);
        var body = JsonDocument.Parse(payload!).RootElement;
        Assert.Equal("Add feature", body.GetProperty("title").GetString());
        Assert.Equal("feature", body.GetProperty("head").GetString());
        Assert.Equal("main", body.GetProperty("base").GetString());
        Assert.True(body.GetProperty("draft").GetBoolean());
        Assert.False(body.TryGetProperty("body", out _));
        Assert.Equal(4, body.EnumerateObject().Count());
        Assert.Equal(1, probe.RequestCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("notes")]
    public async Task CreateSendsBodyOnlyWhenNonEmpty(string? body)
    {
        var account = Account;
        string? payload = null;
        var probe = new SourceAccountHttpProbe { RespondAsync = async (request, token) =>
        {
            payload = await request.Content!.ReadAsStringAsync(token);
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(Created(account)) };
        } };

        await new GitHubPullRequestWriteClient(probe)
            .CreateAsync(account, Scope(account), "T", body, "h", "b", false,
                TestContext.Current.CancellationToken);

        var element = JsonDocument.Parse(payload!).RootElement;
        if (string.IsNullOrEmpty(body))
            Assert.False(element.TryGetProperty("body", out _));
        else
            Assert.Equal(body, element.GetProperty("body").GetString());
    }

    [Theory]
    [InlineData("{}", "missing number")]
    [InlineData("{\"number\":0}", "non-positive number")]
    [InlineData("{\"number\":\"9\"}", "string number")]
    [InlineData("{\"number\":9,\"head\":{\"sha\":\"zz\"}}", "invalid head sha")]
    [InlineData("{\"number\":9,\"head\":{\"sha\":\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"},\"base\":{\"sha\":\"short\"}}", "invalid base sha")]
    [InlineData("[]", "array")]
    [InlineData("{", "malformed")]
    public async Task CreateResponsesMustProveNumberAndCommitShas(string body, string scenario)
    {
        var account = Account;
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestWriteClient(
            Probe(HttpStatusCode.Created, body)).CreateAsync(account, Scope(account), "T", null, "h", "b", false,
            TestContext.Current.CancellationToken));
        Assert.True("source_invalid_response" == failure.ErrorCode, scenario);
    }

    [Fact]
    public async Task CreateRejectsForgedResponseUrl()
    {
        var account = Account;
        var forged = Created(account).Replace("repos/fixture/repo/pulls/9",
            "repos/other/thing/pulls/9", StringComparison.Ordinal);
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new GitHubPullRequestWriteClient(
            Probe(HttpStatusCode.Created, forged)).CreateAsync(account, Scope(account), "T", null, "h", "b", false,
            TestContext.Current.CancellationToken));
        Assert.Equal("source_invalid_response", failure.ErrorCode);
        Assert.DoesNotContain(account.AccessToken, failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRejectsInvalidArgumentsBeforeAnyRequest()
    {
        var account = Account;
        var scope = Scope(account);
        var probe = Probe(HttpStatusCode.Created, Created(account));
        var client = new GitHubPullRequestWriteClient(probe);
        var token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.CreateAsync(account, scope, "", null, "h", "b", false, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.CreateAsync(account, scope, new string('t', 257), null, "h", "b", false, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.CreateAsync(account, scope, "T", new string('b', 65537), "h", "b", false, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.CreateAsync(account, scope, "T", null, "has..dots", "b", false, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.CreateAsync(account, scope, "T", null, "Main", "main", false, token));
        Assert.Equal(0, probe.RequestCount);
    }

    [Fact]
    public async Task CreateScopeAccountAndRevisionMismatchesNeverContactTransport()
    {
        var account = Account;
        var scope = Scope(account);
        var probe = Probe(HttpStatusCode.Created, Created(account));
        var client = new GitHubPullRequestWriteClient(probe);
        var token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.CreateAsync(account, Scope(account, false), "T", null, "h", "b", false, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.CreateAsync(account, scope with { AccountId = Guid.NewGuid() }, "T", null, "h", "b", false, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.CreateAsync(account, scope with { AccountRevision = 2 }, "T", null, "h", "b", false, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.CreateAsync(account.WithRevision(2), scope, "T", null, "h", "b", false, token));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.CreateAsync(account, scope with { Owner = "../escape" }, "T", null, "h", "b", false, token));
        Assert.Equal(0, probe.RequestCount);
    }

    [Fact]
    public async Task ReconcileQueriesOpenPullsForTheHeadBranchAndMatchesExactly()
    {
        var account = Account;
        HttpRequestMessage? sent = null;
        var probe = new SourceAccountHttpProbe { RespondAsync = (request, _) =>
        {
            sent = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent($"[{Created(account)}]") });
        } };

        var found = await new GitHubPullRequestWriteClient(probe)
            .FindCreatedAsync(account, Scope(account), "feature", "main", "Add feature",
                TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal(9, found!.Number);
        Assert.Equal(new string('b', 40), found.HeadRevision);
        Assert.Equal(HttpMethod.Get, sent!.Method);
        Assert.Equal(new Uri(account.ApiEndpoint,
            "repos/fixture/repo/pulls?state=open&head=fixture%3Afeature&per_page=5"), sent.RequestUri);
        Assert.Equal(1, probe.RequestCount);
    }

    [Fact]
    public async Task ReconcileReturnsNullUnlessHeadBaseAndTitleMatch()
    {
        var account = Account;
        var token = TestContext.Current.CancellationToken;
        // Wrong title.
        var client = new GitHubPullRequestWriteClient(Probe(HttpStatusCode.OK,
            $"[{Created(account, title: "Different title")}]"));
        Assert.Null(await client.FindCreatedAsync(account, Scope(account), "feature", "main", "Add feature", token));
        // Wrong base ref.
        client = new GitHubPullRequestWriteClient(Probe(HttpStatusCode.OK,
            $"[{Created(account, baseRef: "release")}]"));
        Assert.Null(await client.FindCreatedAsync(account, Scope(account), "feature", "main", "Add feature", token));
        // An open PR authored by a different head branch must not match.
        client = new GitHubPullRequestWriteClient(Probe(HttpStatusCode.OK,
            $"[{Created(account, headRef: "other")}]"));
        Assert.Null(await client.FindCreatedAsync(account, Scope(account), "feature", "main", "Add feature", token));
        // Empty list.
        client = new GitHubPullRequestWriteClient(Probe(HttpStatusCode.OK, "[]"));
        Assert.Null(await client.FindCreatedAsync(account, Scope(account), "feature", "main", "Add feature", token));
        // A null ref is malformed, not a non-match.
        client = new GitHubPullRequestWriteClient(Probe(HttpStatusCode.OK,
            $"[{Created(account, headRef: null)}]"));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.FindCreatedAsync(account, Scope(account), "feature", "main", "Add feature", token));
        Assert.Equal("source_invalid_response", failure.ErrorCode);
    }

    [Fact]
    public async Task ReconcileRejectsForgedUrlsOversizedListsAndBadCommits()
    {
        var account = Account;
        var token = TestContext.Current.CancellationToken;
        var forged = Created(account).Replace(account.ApiEndpoint.AbsoluteUri,
            "https://attacker.invalid/", StringComparison.Ordinal);
        var client = new GitHubPullRequestWriteClient(Probe(HttpStatusCode.OK, $"[{forged}]"));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.FindCreatedAsync(account, Scope(account), "feature", "main", "Add feature", token));
        Assert.Equal("source_invalid_response", failure.ErrorCode);

        var many = string.Join(",", Enumerable.Range(0, 6).Select(_ => Created(account)));
        client = new GitHubPullRequestWriteClient(Probe(HttpStatusCode.OK, $"[{many}]"));
        failure = await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.FindCreatedAsync(account, Scope(account), "feature", "main", "Add feature", token));
        Assert.Equal("source_invalid_response", failure.ErrorCode);

        client = new GitHubPullRequestWriteClient(Probe(HttpStatusCode.OK,
            $"[{Created(account, head: "short")}]"));
        failure = await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.FindCreatedAsync(account, Scope(account), "feature", "main", "Add feature", token));
        Assert.Equal("source_invalid_response", failure.ErrorCode);
    }

    [Fact]
    public async Task BranchesListReadsOneBoundedPageOfCommitTips()
    {
        var account = Account;
        HttpRequestMessage? sent = null;
        var names = string.Join(",", Enumerable.Range(0, 30)
            .Select(i => PullRequestTestData.Branch($"branch-{i}")));
        var probe = new SourceAccountHttpProbe { RespondAsync = (request, _) =>
        {
            sent = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"[{names}]") });
        } };

        // Branch listing is a read operation: a read-only scope is authorized.
        var branches = await new GitHubPullRequestBranchClient(probe)
            .ListAsync(account, Scope(account, write: false), TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, sent!.Method);
        Assert.Equal(new Uri(account.ApiEndpoint, "repos/fixture/repo/branches?per_page=100"), sent.RequestUri);
        Assert.Equal(30, branches.Count);
        Assert.Equal("branch-0", branches[0].Name);
        Assert.Equal(new string('a', 40), branches[0].Sha);
    }

    [Fact]
    public async Task BranchesRejectInvalidNamesShasDuplicatesAndOversizedLists()
    {
        var account = Account;
        var scope = Scope(account);
        var token = TestContext.Current.CancellationToken;
        var list = new[] { "", "has..dots", "ctrl\nchar", new string('n', 256) };
        foreach (var name in list)
        {
            var client = new GitHubPullRequestBranchClient(
                Probe(HttpStatusCode.OK, $"[{PullRequestTestData.Branch(name)}]"));
            await Assert.ThrowsAsync<SourceReviewException>(() => client.ListAsync(account, scope, token));
        }

        var badSha = new GitHubPullRequestBranchClient(
            Probe(HttpStatusCode.OK, $"[{PullRequestTestData.Branch("main", "short")}]"));
        await Assert.ThrowsAsync<SourceReviewException>(() => badSha.ListAsync(account, scope, token));
        var duplicates = new GitHubPullRequestBranchClient(Probe(HttpStatusCode.OK,
            $"[{PullRequestTestData.Branch("main")},{PullRequestTestData.Branch("main")}]"));
        await Assert.ThrowsAsync<SourceReviewException>(() => duplicates.ListAsync(account, scope, token));
        var many = string.Join(",", Enumerable.Range(0, 101).Select(i => PullRequestTestData.Branch($"b{i}")));
        var oversized = new GitHubPullRequestBranchClient(Probe(HttpStatusCode.OK, $"[{many}]"));
        await Assert.ThrowsAsync<SourceReviewException>(() => oversized.ListAsync(account, scope, token));
    }

    [Fact]
    public async Task BranchesAccountAndRevisionMismatchesNeverContactTransport()
    {
        var account = Account;
        var scope = Scope(account);
        var probe = Probe(HttpStatusCode.OK, "[]");
        var client = new GitHubPullRequestBranchClient(probe);
        var token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.ListAsync(account, scope with { AccountId = Guid.NewGuid() }, token));
        await Assert.ThrowsAsync<SourceReviewException>(() =>
            client.ListAsync(account, scope with { AccountRevision = 2 }, token));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ListAsync(account, scope with { Owner = "../escape" }, token));
        Assert.Equal(0, probe.RequestCount);
    }
}
