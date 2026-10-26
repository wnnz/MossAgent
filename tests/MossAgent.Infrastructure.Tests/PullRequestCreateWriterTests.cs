using System.Net;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PullRequestCreateWriterTests
{
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

    private static string Created(SourceAccount account, string title = "Add feature") =>
        PullRequestTestData.CreatedItem(title: title)
            .Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal);

    [Fact]
    public async Task ReadOnlyGrantNeverSendsHttp()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, false);
        var writer = Writer(fixture, grants, Probe(log, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.CreateAsync(scope, "T", null, "h", "b", false, fixture.Token))).ErrorCode);
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.CreateAsync(scope with { CanWrite = true }, "T", null, "h", "b", false, fixture.Token))).ErrorCode);
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.ValidateWriteAsync(scope, fixture.Token))).ErrorCode);
        Assert.Empty(log);
    }

    [Fact]
    public async Task InvalidArgumentsAreRejectedBeforeResolvingOrSending()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var writer = Writer(fixture, grants, Probe(log, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var token = fixture.Token;

        foreach (var call in new Func<Task>[]
        {
            () => writer.CreateAsync(scope, "", null, "h", "b", false, token),
            () => writer.CreateAsync(scope, new string('t', 257), null, "h", "b", false, token),
            () => writer.CreateAsync(scope, "T", new string('b', 65537), "h", "b", false, token),
            () => writer.CreateAsync(scope, "T", null, "has..dots", "b", false, token),
            () => writer.CreateAsync(scope, "T", null, "Main", "main", false, token),
            () => writer.CreateAsync(scope, "T", null, "", "b", false, token)
        })
            Assert.Equal("invalid_arguments", (await Assert.ThrowsAsync<SourceReviewException>(call)).ErrorCode);
        Assert.Empty(log);
    }

    [Fact]
    public async Task AcceptedCreatePostsOnceAndReturnsTheReceipt()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(Created(account)) }));

        var receipt = await Writer(fixture, grants, probe)
            .CreateAsync(scope, "Add feature", null, "feature", "main", false, fixture.Token);

        Assert.Equal(new PullRequestCreateReceipt(9, new string('b', 40), new string('a', 40), false), receipt);
        Assert.Equal([(HttpMethod.Post, "/api/v3/repos/fixture/repo/pulls")], log);
    }

    [Fact]
    public async Task UnknownOutcomeReconcilesByHeadBaseAndTitleWithoutReposting()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"[{Created(account)}]") }));

        var receipt = await Writer(fixture, grants, probe)
            .CreateAsync(scope, "Add feature", null, "feature", "main", false, fixture.Token);

        Assert.Equal(new PullRequestCreateReceipt(9, new string('b', 40), new string('a', 40), true), receipt);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Post));
        Assert.Single(log, entry => entry.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task UnmatchedReconciliationReportsUnconfirmedWithoutReposting()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(request.Method == HttpMethod.Post
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent($"[{Created(account, title: "Different title")}]") }));

        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .CreateAsync(scope, "Add feature", null, "feature", "main", false, fixture.Token));

        Assert.Equal("source_write_unconfirmed", failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Post));
        Assert.Single(log, entry => entry.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task FailedReconciliationReportsUnknownWithoutReposting()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (request, _) => Task.FromResult(new HttpResponseMessage(
            request.Method == HttpMethod.Post ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.InternalServerError)));

        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .CreateAsync(scope, "Add feature", null, "feature", "main", false, fixture.Token));

        Assert.Equal("source_write_unknown", failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Post));
        Assert.Single(log, entry => entry.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task RevocationDuringReconciliationReportsUnknown()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, async (request, token) =>
        {
            await grants.RevokeAsync(scope.GrantId, token);
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });

        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .CreateAsync(scope, "Add feature", null, "feature", "main", false, fixture.Token));

        Assert.Equal("source_write_unknown", failure.ErrorCode);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Post));
        Assert.DoesNotContain(log, entry => entry.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task DeterministicRejectionsAreNotReconciled()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, _, log) = await SetupAsync(fixture, true);
        var probe = Probe(log, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)));

        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => Writer(fixture, grants, probe)
            .CreateAsync(scope, "Add feature", null, "feature", "main", false, fixture.Token));

        Assert.Equal("source_write_rejected", failure.ErrorCode);
        Assert.Equal([(HttpMethod.Post, "/api/v3/repos/fixture/repo/pulls")], log);
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
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var work = Writer(fixture, grants, probe)
            .CreateAsync(scope, "Add feature", null, "feature", "main", false, cancel.Token);
        await entered.Task;
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        Assert.Equal(1, log.Count(entry => entry.Method == HttpMethod.Post));
        Assert.DoesNotContain(log, entry => entry.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task TitleNeverEchoesTheCredentialIntoArgumentsOrResults()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var (grants, scope, account, log) = await SetupAsync(fixture, true);
        var writer = Writer(fixture, grants, Probe(log, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        var failure = await Assert.ThrowsAsync<SourceReviewException>(() =>
            writer.CreateAsync(scope, $"Fix {account.AccessToken} leak", null, "h", "b", false, fixture.Token));
        Assert.Equal("invalid_arguments", failure.ErrorCode);
        Assert.DoesNotContain(account.AccessToken, failure.ToString(), StringComparison.Ordinal);
        Assert.Empty(log);
    }
}
