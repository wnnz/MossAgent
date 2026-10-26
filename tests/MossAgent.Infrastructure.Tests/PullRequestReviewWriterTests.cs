using System.Net;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PullRequestReviewWriterTests
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

    private static PullRequestWriter Writer(SqliteFailurePersistenceFixture fixture,
        SqliteSourceRepositoryGrants grants, SourceAccountHttpProbe probe) =>
        new(new(grants, new SqliteSourceAccountRepository(fixture.Connections)),
            new GitHubPullRequestClient(probe), new GitHubPullRequestWriteClient(probe));

    private static string Review(SourceAccount account, string state = "APPROVED", string? body = "ship it",
        long id = 31, string? commit = null) => PullRequestTestData.Review(id: id, state: state, body: body, commit: commit)
        .Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal);

    [Fact]
    public async Task ReadOnlyGrantNeverSendsHttp()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, false);
        var writer = Writer(fixture, grants, Probe(log, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.ValidateWriteAsync(scope, Revision, fixture.Token))).ErrorCode);
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.ReviewAsync(scope, Revision, PullRequestReviewDecision.Approve, null, fixture.Token))).ErrorCode);
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.ReviewAsync(scope with { CanWrite = true }, Revision, PullRequestReviewDecision.Approve, null,
                fixture.Token))).ErrorCode);
        Assert.Empty(log);
    }

    [Fact]
    public async Task StaleRevisionFailsBeforePosting()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(PullRequestTestData.Item().Replace(new string('b', 40), new string('c', 40), StringComparison.Ordinal)) }));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .ReviewAsync(scope, Revision, PullRequestReviewDecision.Approve, null, fixture.Token));
        Assert.Equal("source_revision_changed", failure.ErrorCode);
        Assert.Single(log);
        Assert.DoesNotContain(log, entry => entry.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task RevocationBetweenRevisionCheckAndPostFailsClosed()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, async (request, token) =>
        {
            await grants.RevokeAsync(scope.GrantId, token);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) };
        });
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .ReviewAsync(scope, Revision, PullRequestReviewDecision.Comment, "ship it", fixture.Token));
        Assert.Equal("source_scope_denied", failure.ErrorCode);
        Assert.DoesNotContain(log, entry => entry.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task AcceptedWritePostsOnceAndReturnsReceipt()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(Review(account)) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) }));
        var receipt = await Writer(fixture, grants, probe)
            .ReviewAsync(scope, Revision, PullRequestReviewDecision.Approve, "ship it", fixture.Token);
        Assert.Equal(new PullRequestReviewReceipt(31, false), receipt);
        Assert.Equal([(HttpMethod.Get, "/api/v3/repos/fixture/repo/pulls/7"),
            (HttpMethod.Post, "/api/v3/repos/fixture/repo/pulls/7/reviews")], log);
    }

    [Fact]
    public async Task UnknownOutcomeReconcilesByRecentReviewWithoutReposting()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : request.RequestUri!.AbsolutePath.EndsWith("/reviews", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"[{Review(account)}]") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) }));
        var receipt = await Writer(fixture, grants, probe)
            .ReviewAsync(scope, Revision, PullRequestReviewDecision.Approve, "ship it", fixture.Token);
        Assert.Equal(new PullRequestReviewReceipt(31, true), receipt);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Post));
        Assert.Single(log, entry => entry.Method == HttpMethod.Get && entry.Path.EndsWith("/reviews", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReconcileRequiresTheSameDecisionStateAndCommit()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : request.RequestUri!.AbsolutePath.EndsWith("/reviews", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                    $"[{Review(account, state: "COMMENTED")},{Review(account, id: 32, commit: new string('c', 40))}]") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) }));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .ReviewAsync(scope, Revision, PullRequestReviewDecision.Approve, "ship it", fixture.Token));
        Assert.Equal("source_write_unconfirmed", failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Post));
    }

    [Theory]
    [InlineData("[]", "source_write_unconfirmed")]
    [InlineData("not-json", "source_write_unknown")]
    public async Task UnmatchedOrFailedReconciliationReportsHonestOutcome(string reconcileBody, string expected)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : request.RequestUri!.AbsolutePath.EndsWith("/reviews", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(reconcileBody) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) }));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .ReviewAsync(scope, Revision, PullRequestReviewDecision.Approve, null, fixture.Token));
        Assert.Equal(expected, failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Post));
    }

    [Fact]
    public async Task DeterministicRejectionsAreNotReconciled()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) }));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .ReviewAsync(scope, Revision, PullRequestReviewDecision.RequestChanges, "needs work", fixture.Token));
        Assert.Equal("source_write_rejected", failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Post));
        Assert.DoesNotContain(log, entry => entry.Path.EndsWith("/reviews", StringComparison.Ordinal) && entry.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task CallerCancellationDuringThePostIsNotReconciled()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
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
        var work = Writer(fixture, grants, probe)
            .ReviewAsync(scope, Revision, PullRequestReviewDecision.Approve, null, cancel.Token);
        await entered.Task;
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Post));
        Assert.DoesNotContain(log, entry => entry.Path.EndsWith("/reviews", StringComparison.Ordinal) && entry.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task BodyBoundAndUnknownDecisionAreRejectedBeforeResolvingOrSending()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var writer = Writer(fixture, grants, Probe(log, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        Assert.Equal("invalid_arguments", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.ReviewAsync(scope, Revision, PullRequestReviewDecision.Approve, new string('x', 4097), fixture.Token))).ErrorCode);
        Assert.Equal("invalid_arguments", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.ReviewAsync(scope, Revision, (PullRequestReviewDecision)99, null, fixture.Token))).ErrorCode);
        Assert.Empty(log);
    }
}
