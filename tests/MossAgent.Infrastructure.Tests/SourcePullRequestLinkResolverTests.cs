using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SourcePullRequestLinkResolverTests
{
    [Fact]
    public async Task ResolvesOnlyTheExplicitAccountAndExistingRepositoryGrant()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var other = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var grants = new SqliteSourceRepositoryGrants(fixture.Connections);
        var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", false);
        await grants.AddAsync(scope, fixture.Token);
        var resolver = new SourcePullRequestLinkResolver(accounts, grants);
        const string url = "https://enterprise.example.test/Fixture/Repo/pull/42/files?access_token=synthetic-query#discussion_r7";
        Assert.Equal(new SourcePullRequestTarget(scope, 42), await resolver.ResolveAsync(account.Id, account.Revision, url, fixture.Token));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => resolver.ResolveAsync(other.Id, other.Revision, url, fixture.Token));
        Assert.Equal("source_url_repository_not_granted", failure.ErrorCode);
        Assert.DoesNotContain("synthetic-query", failure.ToString(), StringComparison.Ordinal);
        Assert.Equal(scope, Assert.Single(await grants.GetAllAsync(fixture.Token)));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM source_review_sessions;"));
    }

    [Theory]
    [InlineData("https://github.com/fixture/repo/pull/7", "source_url_account_mismatch")]
    [InlineData("https://enterprise.example.test:8443/fixture/repo/pull/7", "source_url_account_mismatch")]
    [InlineData("https://enterprise.example.test/fixture/ungranted/pull/7", "source_url_repository_not_granted")]
    [InlineData("https://enterprise.example.test/fixture/repo/issues/7?token=synthetic-query", "source_invalid_pr_url")]
    public async Task InvalidTargetsNeverCreateOrBroadenARepositoryGrant(string url, string code)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var grants = new SqliteSourceRepositoryGrants(fixture.Connections);
        var resolver = new SourcePullRequestLinkResolver(accounts, grants);
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => resolver.ResolveAsync(account.Id, account.Revision, url, fixture.Token));
        Assert.Equal(code, failure.ErrorCode);
        Assert.DoesNotContain("synthetic-query", failure.ToString(), StringComparison.Ordinal);
        Assert.Empty(await grants.GetAllAsync(fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM source_review_sessions;"));
    }

    [Theory]
    [InlineData("revoke")]
    [InlineData("edit")]
    [InlineData("disable")]
    [InlineData("delete")]
    public async Task StaleAccountOrGrantCannotBeRecoveredByPastingALink(string mutation)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var grants = new SqliteSourceRepositoryGrants(fixture.Connections);
        var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", false);
        await grants.AddAsync(scope, fixture.Token);
        if (mutation == "revoke") await grants.RevokeAsync(scope.GrantId, fixture.Token);
        else if (mutation == "delete") await accounts.DeleteAsync(account.Id, account.Revision, fixture.Token);
        else await accounts.SaveAsync(new(account.Id, account.Name, account.ApiEndpoint, account.AccessToken, null, mutation != "disable", account.Revision), fixture.Token);
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() => new SourcePullRequestLinkResolver(accounts, grants)
            .ResolveAsync(account.Id, account.Revision, "https://enterprise.example.test/fixture/repo/pull/7", fixture.Token));
        Assert.Equal(mutation == "revoke" ? "source_url_repository_not_granted" : "source_scope_denied", failure.ErrorCode);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM source_review_sessions;"));
    }

    [Fact]
    public async Task CancellationDoesNotCreateSourceSessionsOrGrants()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var resolver = new SourcePullRequestLinkResolver(new SqliteSourceAccountRepository(fixture.Connections), new SqliteSourceRepositoryGrants(fixture.Connections));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolver.ResolveAsync(Guid.NewGuid(), 1, "https://github.com/fixture/repo/pull/7", cancellation.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM source_review_sessions;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM source_repository_grants;"));
    }
}
