using System.Net;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PullRequestCommentWriterTests
{
    private static PullRequestRevision Revision => new(7, new string('a', 40), new string('b', 40));

    private static async Task<(SqliteSourceRepositoryGrants Grants, SourceRepositoryScope Scope, SourceAccount Account,
        List<(HttpMethod Method, string Path)> Log)> SetupAsync(SqliteFailurePersistenceFixture fixture, bool canWrite)
    {
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var grants = new SqliteSourceRepositoryGrants(fixture.Connections);
        var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", canWrite);
        await grants.AddAsync(scope, fixture.Token);
        return (grants, scope, account, []);
    }

    private static SourceAccountHttpProbe Probe(List<(HttpMethod Method, string Path)> log,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) => new()
    { RespondAsync = (request, token) => { log.Add((request.Method, request.RequestUri!.AbsolutePath)); return respond(request, token); } };

    private static string Comment(SourceAccount account, string body = "ship it") => PullRequestTestData.Comment(body: body)
        .Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal);

    [Fact]
    public async Task ReadOnlyGrantNeverSendsHttp()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, false);
        var writer = new PullRequestCommentWriter(new(grants, new SqliteSourceAccountRepository(fixture.Connections)),
            new GitHubPullRequestClient(Probe(log, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))),
            new GitHubPullRequestWriteClient(Probe(log, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))));
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.ValidateWriteAsync(scope, Revision, fixture.Token))).ErrorCode);
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.CommentAsync(scope, Revision, "ship it", fixture.Token))).ErrorCode);
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.CommentAsync(scope with { CanWrite = true }, Revision, "ship it", fixture.Token))).ErrorCode);
        Assert.Empty(log);
    }

    [Fact]
    public async Task StaleRevisionFailsBeforePosting()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(PullRequestTestData.Item().Replace(new string('b', 40), new string('c', 40), StringComparison.Ordinal)) }));
        var writer = new PullRequestCommentWriter(new(grants, new SqliteSourceAccountRepository(fixture.Connections)),
            new GitHubPullRequestClient(probe), new GitHubPullRequestWriteClient(probe));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => writer.CommentAsync(scope, Revision, "ship it", fixture.Token));
        Assert.Equal("source_revision_changed", failure.ErrorCode);
        Assert.Single(log);
        Assert.DoesNotContain(log, entry => entry.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task RevocationBetweenRevisionCheckAndPostFailsClosed()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, async (request, token) =>
        {
            await grants.RevokeAsync(scope.GrantId, token);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) };
        });
        var writer = new PullRequestCommentWriter(new(grants, new SqliteSourceAccountRepository(fixture.Connections)),
            new GitHubPullRequestClient(probe), new GitHubPullRequestWriteClient(probe));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => writer.CommentAsync(scope, Revision, "ship it", fixture.Token));
        Assert.Equal("source_scope_denied", failure.ErrorCode);
        Assert.DoesNotContain(log, entry => entry.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task AcceptedWritePostsOnceAndReturnsReceipt()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(Comment(account)) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) }));
        var writer = new PullRequestCommentWriter(new(grants, new SqliteSourceAccountRepository(fixture.Connections)),
            new GitHubPullRequestClient(probe), new GitHubPullRequestWriteClient(probe));
        var receipt = await writer.CommentAsync(scope, Revision, "ship it", fixture.Token);
        Assert.Equal(new PullRequestCommentReceipt(11, false), receipt);
        Assert.Equal([(HttpMethod.Get, "/api/v3/repos/fixture/repo/pulls/7"),
            (HttpMethod.Post, "/api/v3/repos/fixture/repo/issues/7/comments")], log);
    }

    [Fact]
    public async Task UnknownOutcomeReconcilesByExactRecentBodyWithoutReposting()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : request.RequestUri!.AbsolutePath.EndsWith("/comments", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"[{Comment(account)}]") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) }));
        var writer = new PullRequestCommentWriter(new(grants, new SqliteSourceAccountRepository(fixture.Connections)),
            new GitHubPullRequestClient(probe), new GitHubPullRequestWriteClient(probe));
        var receipt = await writer.CommentAsync(scope, Revision, "ship it", fixture.Token);
        Assert.Equal(new PullRequestCommentReceipt(11, true), receipt);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Post));
        Assert.Single(log, entry => entry.Method == HttpMethod.Get && entry.Path.EndsWith("/comments", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("[]", "source_write_unconfirmed")]
    [InlineData("not-json", "source_write_unknown")]
    public async Task UnmatchedOrFailedReconciliationReportsHonestOutcome(string reconcileBody, string expected)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : request.RequestUri!.AbsolutePath.EndsWith("/comments", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(reconcileBody) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) }));
        var writer = new PullRequestCommentWriter(new(grants, new SqliteSourceAccountRepository(fixture.Connections)),
            new GitHubPullRequestClient(probe), new GitHubPullRequestWriteClient(probe));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => writer.CommentAsync(scope, Revision, "ship it", fixture.Token));
        Assert.Equal(expected, failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Post));
    }

    [Fact]
    public async Task TransportFailureTriggersTheSameQueryOnlyReconciliation()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => request.Method == HttpMethod.Post
            ? throw new HttpRequestException("connection dropped")
            : Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/comments", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) }));
        var writer = new PullRequestCommentWriter(new(grants, new SqliteSourceAccountRepository(fixture.Connections)),
            new GitHubPullRequestClient(probe), new GitHubPullRequestWriteClient(probe));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => writer.CommentAsync(scope, Revision, "ship it", fixture.Token));
        Assert.Equal("source_write_unconfirmed", failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Post));
    }

    [Fact]
    public async Task DeterministicRejectionsAreNotReconciled()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) }));
        var writer = new PullRequestCommentWriter(new(grants, new SqliteSourceAccountRepository(fixture.Connections)),
            new GitHubPullRequestClient(probe), new GitHubPullRequestWriteClient(probe));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => writer.CommentAsync(scope, Revision, "ship it", fixture.Token));
        Assert.Equal("source_write_rejected", failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Post));
        Assert.DoesNotContain(log, entry => entry.Path.EndsWith("/comments", StringComparison.Ordinal) && entry.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task CallerCancellationDuringThePostIsNotReconciled()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var entered = new TaskCompletionSource();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var probe = Probe(log, async (request, token) =>
        {
            if (request.Method != HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) };
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var writer = new PullRequestCommentWriter(new(grants, new SqliteSourceAccountRepository(fixture.Connections)),
            new GitHubPullRequestClient(probe), new GitHubPullRequestWriteClient(probe));
        var work = writer.CommentAsync(scope, Revision, "ship it", cancel.Token);
        await entered.Task;
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Post));
        Assert.DoesNotContain(log, entry => entry.Path.EndsWith("/comments", StringComparison.Ordinal) && entry.Method == HttpMethod.Get);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4097)]
    public async Task BodyBoundsAreRejectedBeforeResolvingOrSending(int length)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var writer = new PullRequestCommentWriter(new(grants, new SqliteSourceAccountRepository(fixture.Connections)),
            new GitHubPullRequestClient(probe), new GitHubPullRequestWriteClient(probe));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.CommentAsync(scope, Revision, new string('x', length), fixture.Token));
        Assert.Equal("invalid_arguments", failure.ErrorCode);
        Assert.Empty(log);
    }
}
