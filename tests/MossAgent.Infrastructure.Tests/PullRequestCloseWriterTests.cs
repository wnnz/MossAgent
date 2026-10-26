using System.Net;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PullRequestCloseWriterTests
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

    private static string ClosedResponse(SourceAccount account, string state = "closed") => PullRequestTestData
        .Item(state: state).Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal);

    [Fact]
    public async Task ReadOnlyGrantNeverSendsHttp()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, false);
        var writer = Writer(fixture, grants, Probe(log, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.ValidateWriteAsync(scope, Revision, fixture.Token))).ErrorCode);
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.CloseAsync(scope, Revision, fixture.Token))).ErrorCode);
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.CloseAsync(scope with { CanWrite = true }, Revision, fixture.Token))).ErrorCode);
        Assert.Empty(log);
    }

    [Fact]
    public async Task StaleRevisionFailsBeforePatching()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(Summary(head: new string('c', 40))) }));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .CloseAsync(scope, Revision, fixture.Token));
        Assert.Equal("source_revision_changed", failure.ErrorCode);
        Assert.Single(log);
        Assert.DoesNotContain(log, entry => entry.Method == HttpMethod.Patch);
    }

    [Fact]
    public async Task RevocationBetweenRevisionCheckAndPatchFailsClosed()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, async (request, token) =>
        {
            await grants.RevokeAsync(scope.GrantId, token);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Summary()) };
        });
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .CloseAsync(scope, Revision, fixture.Token));
        Assert.Equal("source_scope_denied", failure.ErrorCode);
        Assert.DoesNotContain(log, entry => entry.Method == HttpMethod.Patch);
    }

    [Fact]
    public async Task AcceptedClosePatchesOnceAndReturnsReceipt()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Patch
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ClosedResponse(account)) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Summary()) }));
        var receipt = await Writer(fixture, grants, probe).CloseAsync(scope, Revision, fixture.Token);
        Assert.Equal(new PullRequestCloseReceipt("closed", false), receipt);
        Assert.Equal([(HttpMethod.Get, "/api/v3/repos/fixture/repo/pulls/7"),
            (HttpMethod.Patch, "/api/v3/repos/fixture/repo/pulls/7")], log);
    }

    [Fact]
    public async Task NonClosedPatchResponseIsRejectedWithoutReconciling()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Patch
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ClosedResponse(account, "open")) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Summary()) }));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .CloseAsync(scope, Revision, fixture.Token));
        Assert.Equal("source_invalid_response", failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Patch));
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Get));
    }

    [Fact]
    public async Task UnknownOutcomeReconcilesByClosedSummaryWithoutRepatching()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var patched = false;
        var probe = Probe(log, (request, _) =>
        {
            if (request.Method == HttpMethod.Patch)
            {
                patched = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(Summary(patched ? "closed" : "open")) });
        });
        var receipt = await Writer(fixture, grants, probe).CloseAsync(scope, Revision, fixture.Token);
        Assert.Equal(new PullRequestCloseReceipt("closed", true), receipt);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Patch));
        Assert.Equal(2, log.Count(entry => entry.Method == HttpMethod.Get));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnmatchedReconciliationReportsUnconfirmed(bool revisionDrifted)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var patched = false;
        var probe = Probe(log, (request, _) =>
        {
            if (request.Method == HttpMethod.Patch)
            {
                patched = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            var body = !patched ? Summary("open")
                : revisionDrifted ? Summary("closed", head: new string('c', 40)) : Summary("open");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        });
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .CloseAsync(scope, Revision, fixture.Token));
        Assert.Equal("source_write_unconfirmed", failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Patch));
        Assert.Equal(2, log.Count(entry => entry.Method == HttpMethod.Get));
    }

    [Fact]
    public async Task FailedReconciliationReportsUnknownWithoutRepatching()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var patched = false;
        var probe = Probe(log, (request, _) =>
        {
            if (request.Method == HttpMethod.Patch)
            {
                patched = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            return Task.FromResult(new HttpResponseMessage(
                patched ? HttpStatusCode.InternalServerError : HttpStatusCode.OK)
            { Content = new StringContent(Summary()) });
        });
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .CloseAsync(scope, Revision, fixture.Token));
        Assert.Equal("source_write_unknown", failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Patch));
        Assert.Equal(2, log.Count(entry => entry.Method == HttpMethod.Get));
    }

    [Fact]
    public async Task DeterministicRejectionsAreNotReconciled()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Patch
            ? new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Summary()) }));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .CloseAsync(scope, Revision, fixture.Token));
        Assert.Equal("source_write_rejected", failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Patch));
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Get));
    }

    [Fact]
    public async Task CallerCancellationDuringThePatchIsNotReconciled()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var entered = new TaskCompletionSource();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var probe = Probe(log, async (request, token) =>
        {
            if (request.Method != HttpMethod.Patch)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Summary()) };
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var work = Writer(fixture, grants, probe).CloseAsync(scope, Revision, cancel.Token);
        await entered.Task;
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Patch));
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Get));
    }
}
