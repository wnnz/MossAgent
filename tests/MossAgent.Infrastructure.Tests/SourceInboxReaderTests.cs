using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SourceInboxReaderTests
{
    private static string Page(SourceAccount account) => PullRequestTestData
        .InboxPage(items: PullRequestTestData.InboxItem())
        .Replace("https://api.github.com/", account.ApiEndpoint.AbsoluteUri, StringComparison.Ordinal);

    [Fact]
    public async Task EnabledAccountReadsInboxPage()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var probe = new SourceAccountHttpProbe { RespondAsync = (_, _) => Task.FromResult(
            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(Page(account)) }) };
        var reader = new SourceInboxReader(accounts, new GitHubPullRequestInboxClient(probe));
        var page = await reader.ReadAsync(account.Id, account.Revision, PullRequestInboxFilter.ReviewRequested, 1, fixture.Token);
        var item = Assert.Single(page.Items);
        Assert.Equal("fixture", item.Owner);
        Assert.Equal("repo", item.Repository);
        Assert.Equal(1, probe.RequestCount);
    }

    [Fact]
    public async Task MissingDisabledOrStaleRevisionAccountIsDeniedBeforeTransport()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var disabled = await accounts.SaveAsync(new SourceAccount(Guid.NewGuid(), "disabled",
            account.ApiEndpoint, "other-synthetic-credential", null, false), fixture.Token);
        var probe = new SourceAccountHttpProbe { RespondAsync = (_, _) => Task.FromResult(
            new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(Page(account)) }) };
        var reader = new SourceInboxReader(accounts, new GitHubPullRequestInboxClient(probe));
        foreach (var (id, revision) in new[] { (Guid.NewGuid(), account.Revision), (disabled.Id, disabled.Revision), (account.Id, 99L) })
            Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(
                () => reader.ReadAsync(id, revision, PullRequestInboxFilter.Involves, 1, fixture.Token))).ErrorCode);
        Assert.Equal("invalid_arguments", (await Assert.ThrowsAsync<SourceReviewException>(
            () => reader.ReadAsync(account.Id, account.Revision, PullRequestInboxFilter.Involves, 0, fixture.Token))).ErrorCode);
        Assert.Equal(0, probe.RequestCount);
    }

    [Fact]
    public async Task AccountRevisionChangeDuringReadDeniesThePage()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var probe = new SourceAccountHttpProbe { RespondAsync = async (_, token) =>
        {
            await accounts.SaveAsync(account, token);
            return new(System.Net.HttpStatusCode.OK) { Content = new StringContent(Page(account)) };
        } };
        var reader = new SourceInboxReader(accounts, new GitHubPullRequestInboxClient(probe));
        var failure = await Assert.ThrowsAsync<SourceReviewException>(() =>
            reader.ReadAsync(account.Id, account.Revision, PullRequestInboxFilter.Authored, 1, fixture.Token));
        Assert.Equal("source_scope_denied", failure.ErrorCode);
        Assert.Equal(1, probe.RequestCount);
        Assert.Equal(1, probe.DisposedHandlers);
    }
}
