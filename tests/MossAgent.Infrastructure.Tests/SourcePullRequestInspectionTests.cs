using System.Net;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SourcePullRequestInspectionTests
{
    private static PullRequestRevision Revision => new(7, new string('a', 40), new string('b', 40));

    [Theory]
    [InlineData("stable", null, 3)]
    [InlineData("before", "source_revision_changed", 1)]
    [InlineData("after", "source_revision_changed", 3)]
    [InlineData("base-before", "source_revision_changed", 1)]
    [InlineData("base-after", "source_revision_changed", 3)]
    [InlineData("revoke", "source_scope_denied", 2)]
    [InlineData("account", "source_scope_denied", 2)]
    public async Task EveryReadChecksCurrentRevisionAndLiveGrantBeforePublishing(string change, string? error, int expectedRequests)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var grants = new SqliteSourceRepositoryGrants(fixture.Connections);
        var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo");
        await grants.AddAsync(scope, fixture.Token);
        var count = 0;
        var probe = new SourceAccountHttpProbe { RespondAsync = async (request, token) =>
        {
            count++;
            if (request.RequestUri!.AbsolutePath.EndsWith("/files", StringComparison.Ordinal))
            {
                if (change == "revoke") await grants.RevokeAsync(scope.GrantId, token);
                if (change == "account") await accounts.SaveAsync(account, token);
                return new(HttpStatusCode.OK) { Content = new StringContent($"[{PullRequestTestData.File()}]") };
            }
            var body = PullRequestTestData.Item();
            if (change == "before" || change == "after" && count == 3) body = body.Replace(new string('b', 40), new string('c', 40), StringComparison.Ordinal);
            if (change == "base-before" || change == "base-after" && count == 3) body = body.Replace(new string('a', 40), new string('c', 40), StringComparison.Ordinal);
            return new(HttpStatusCode.OK) { Content = new StringContent(body) };
        } };
        var inspection = new SourcePullRequestInspection(new(grants, accounts), new GitHubPullRequestClient(probe), new GitHubPullRequestInspectionClient(probe));
        await inspection.ValidateAsync(scope, Revision, fixture.Token);
        Assert.Equal(0, count);
        if (error is null) Assert.Single((await inspection.FilesAsync(scope, Revision, 1, fixture.Token)).Items);
        else Assert.Equal(error, (await Assert.ThrowsAsync<SourceReviewException>(() => inspection.FilesAsync(scope, Revision, 1, fixture.Token))).ErrorCode);
        Assert.Equal(expectedRequests, count);
        Assert.Equal(expectedRequests, probe.DisposedHandlers);
    }

    [Fact]
    public async Task CrossScopeAndAlreadyRevokedReadsNeverSendHttp()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var grants = new SqliteSourceRepositoryGrants(fixture.Connections);
        var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo");
        await grants.AddAsync(scope, fixture.Token);
        var probe = new SourceAccountHttpProbe();
        var inspection = new SourcePullRequestInspection(new(grants, accounts), new GitHubPullRequestClient(probe), new GitHubPullRequestInspectionClient(probe));
        foreach (var other in new[] { scope with { Repository = "other" }, scope with { AccountId = Guid.NewGuid() }, scope with { AccountRevision = account.Revision + 1 } })
            Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() => inspection.FilesAsync(other, Revision, 1, fixture.Token))).ErrorCode);
        await grants.RevokeAsync(scope.GrantId, fixture.Token);
        await Assert.ThrowsAsync<SourceReviewException>(() => inspection.CommentsAsync(scope, Revision, "inline", 1, fixture.Token));
        await Assert.ThrowsAsync<SourceReviewException>(() => inspection.ChecksAsync(scope, Revision, "runs", 1, fixture.Token));
        Assert.Equal(0, probe.RequestCount);
    }

    [Fact]
    public async Task CancellationWaitsForActualHttpCleanupAndSkipsPostreadRequest()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var grants = new SqliteSourceRepositoryGrants(fixture.Connections);
        var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo");
        await grants.AddAsync(scope, fixture.Token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaned = false;
        var probe = new SourceAccountHttpProbe { RespondAsync = async (request, token) =>
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/files", StringComparison.Ordinal))
                return new(HttpStatusCode.OK) { Content = new StringContent(PullRequestTestData.Item()) };
            entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cleaned = true; }
            return new(HttpStatusCode.OK);
        } };
        var inspection = new SourcePullRequestInspection(new(grants, accounts), new GitHubPullRequestClient(probe), new GitHubPullRequestInspectionClient(probe));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var work = inspection.FilesAsync(scope, Revision, 1, cancel.Token);
        await entered.Task.WaitAsync(fixture.Token); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        Assert.True(cleaned);
        Assert.Equal(2, probe.DisposedHandlers);
    }
}
