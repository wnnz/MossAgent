using System.Net;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PullRequestMergeWriterTests
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

    private static string Summary(string state = "open", string? head = null) => PullRequestTestData
        .Item(state: state).Replace(new string('b', 40), head ?? new string('b', 40), StringComparison.Ordinal);

    private static string MergeResponse(string? sha = null) =>
        $"{{\"sha\":\"{sha ?? new string('c', 40)}\",\"merged\":true,\"message\":\"merged\"}}";

    // A landed merge advances the base sha server-side; the detail keeps the merged head.
    // The url host must match the account endpoint or the client's ownership check rejects it.
    private static string MergedDetail(SourceAccount account, string? head = null, bool merged = true,
        string? mergeCommit = null) => PullRequestTestData
        .Merged(merged: merged, head: head, baseSha: new string('d', 40), mergeCommit: mergeCommit ?? new string('c', 40))
        .Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal);

    [Fact]
    public async Task ReadOnlyGrantNeverSendsHttp()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, false);
        var writer = Writer(fixture, grants, Probe(log, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.MergeAsync(scope, Revision, PullRequestMergeMethod.Merge, fixture.Token))).ErrorCode);
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.MergeAsync(scope with { CanWrite = true }, Revision, PullRequestMergeMethod.Merge, fixture.Token))).ErrorCode);
        Assert.Empty(log);
    }

    [Fact]
    public async Task UnknownMergeMethodIsRejectedBeforeResolvingOrSending()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var writer = Writer(fixture, grants, Probe(log, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.MergeAsync(scope, Revision, (PullRequestMergeMethod)99, fixture.Token));
        Assert.Equal("invalid_arguments", failure.ErrorCode);
        Assert.Empty(log);
    }

    [Fact]
    public async Task StaleRevisionFailsBeforePutting()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(Summary(head: new string('c', 40))) }));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .MergeAsync(scope, Revision, PullRequestMergeMethod.Merge, fixture.Token));
        Assert.Equal("source_revision_changed", failure.ErrorCode);
        Assert.Single(log);
        Assert.DoesNotContain(log, entry => entry.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task RevocationBetweenRevisionCheckAndPutFailsClosed()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, async (request, token) =>
        {
            await grants.RevokeAsync(scope.GrantId, token);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Summary()) };
        });
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .MergeAsync(scope, Revision, PullRequestMergeMethod.Squash, fixture.Token));
        Assert.Equal("source_scope_denied", failure.ErrorCode);
        Assert.DoesNotContain(log, entry => entry.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task AcceptedMergePutsOnceAndReturnsTheMergeSha()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Put
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(MergeResponse()) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Summary()) }));
        var receipt = await Writer(fixture, grants, probe)
            .MergeAsync(scope, Revision, PullRequestMergeMethod.Rebase, fixture.Token);
        Assert.Equal(new PullRequestMergeReceipt(new string('c', 40), false), receipt);
        Assert.Equal([(HttpMethod.Get, "/api/v3/repos/fixture/repo/pulls/7"),
            (HttpMethod.Put, "/api/v3/repos/fixture/repo/pulls/7/merge")], log);
    }

    [Fact]
    public async Task UnknownOutcomeReconcilesByMergedDetailWithoutReputting()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var put = false;
        var probe = Probe(log, (request, _) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                put = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(put ? MergedDetail(account) : Summary()) });
        });
        var receipt = await Writer(fixture, grants, probe)
            .MergeAsync(scope, Revision, PullRequestMergeMethod.Merge, fixture.Token);
        Assert.Equal(new PullRequestMergeReceipt(new string('c', 40), true), receipt);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Put));
        Assert.Equal(2, log.Count(entry => entry.Method == HttpMethod.Get));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnmatchedReconciliationReportsUnconfirmed(bool headDrifted)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var put = false;
        var probe = Probe(log, (request, _) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                put = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            // merged == true under a different head is still unconfirmed for our write.
            var body = !put ? Summary()
                : headDrifted ? MergedDetail(account, head: new string('e', 40)) : MergedDetail(account, merged: false);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        });
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .MergeAsync(scope, Revision, PullRequestMergeMethod.Merge, fixture.Token));
        Assert.Equal("source_write_unconfirmed", failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Put));
        Assert.Equal(2, log.Count(entry => entry.Method == HttpMethod.Get));
    }

    [Fact]
    public async Task FailedReconciliationReportsUnknownWithoutReputting()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var put = false;
        var probe = Probe(log, (request, _) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                put = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            return Task.FromResult(new HttpResponseMessage(
                put ? HttpStatusCode.InternalServerError : HttpStatusCode.OK)
            { Content = new StringContent(Summary()) });
        });
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .MergeAsync(scope, Revision, PullRequestMergeMethod.Merge, fixture.Token));
        Assert.Equal("source_write_unknown", failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Put));
        Assert.Equal(2, log.Count(entry => entry.Method == HttpMethod.Get));
    }

    [Fact]
    public async Task DeterministicRejectionsAreNotReconciled()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Put
            ? new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Summary()) }));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .MergeAsync(scope, Revision, PullRequestMergeMethod.Merge, fixture.Token));
        Assert.Equal("source_write_rejected", failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Put));
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Get));
    }

    [Fact]
    public async Task CallerCancellationDuringThePutIsNotReconciled()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var entered = new TaskCompletionSource();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var probe = Probe(log, async (request, token) =>
        {
            if (request.Method != HttpMethod.Put)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Summary()) };
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var work = Writer(fixture, grants, probe).MergeAsync(scope, Revision, PullRequestMergeMethod.Merge, cancel.Token);
        await entered.Task;
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Put));
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Get));
    }
}
