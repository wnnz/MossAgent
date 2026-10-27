using Microsoft.Data.Sqlite;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SourceRepositoryGrantTests
{
    [Fact]
    public async Task VersionEighteenMigrationRollsBackAndPreservesAccountsAndTasks()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        await fixture.ExecuteAsync("""
            DROP TABLE source_review_sessions; DROP TABLE source_repository_grants; DROP TABLE source_repository_revocations;
            DROP TABLE source_review_chats; DROP TRIGGER source_review_chat_environment;
            DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=19;
            CREATE TRIGGER reject_nineteen BEFORE INSERT ON schema_versions WHEN NEW.version=19
            BEGIN SELECT RAISE(FAIL,'fixture failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(18, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='source_repository_grants';"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_nineteen;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(47, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(account.AccessToken, (await accounts.GetAsync(account.Id, fixture.Token))!.AccessToken);
        Assert.Equal(fixture.Task.Id, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)).Id);
    }

    [Fact]
    public async Task ReopenRevocationAndCredentialChangeNeverReuseOldScopeOrGrantLocalAccess()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var grants = new SqliteSourceRepositoryGrants(fixture.Connections);
        var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", false);
        await grants.AddAsync(scope, fixture.Token);
        var reopened = new SqliteSourceRepositoryGrants(fixture.Connections);
        Assert.Equal(scope, Assert.Single(await reopened.GetAllAsync(fixture.Token)));
        var sessions = new SqliteSourceReviewSessions(fixture.Connections);
        var id = await sessions.GetOrCreateAsync(scope, fixture.Token);
        Assert.Equal(id, await sessions.GetOrCreateAsync(scope, fixture.Token));
        Assert.Empty(await fixture.Repository.GetTasksAsync(null, fixture.Token));
        Assert.Empty(await fixture.Repository.GetPinnedTasksAsync(fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM agent_tasks WHERE project_id IS NULL AND worktree_path IS NOT NULL;"));
        var reader = new SourceReviewReader(grants, accounts, new GitHubPullRequestClient(new SourceAccountHttpProbe()));
        await reader.ValidateAsync(scope, fixture.Token);
        await Assert.ThrowsAsync<SourceReviewException>(() => reader.ValidateAsync(scope with { Repository = "other" }, fixture.Token));
        var updated = await accounts.SaveAsync(account, fixture.Token);
        await Assert.ThrowsAsync<SourceReviewException>(() => reader.ValidateAsync(scope, fixture.Token));
        await grants.RevokeAsync(scope.GrantId, fixture.Token);
        await Assert.ThrowsAsync<SourceReviewException>(() => reader.ValidateAsync(scope, fixture.Token));
        await Assert.ThrowsAsync<SourceReviewException>(() => grants.AddAsync(scope, fixture.Token));
        await Assert.ThrowsAsync<SourceReviewException>(() => grants.AddAsync(scope with { AccountRevision = updated.Revision }, fixture.Token));
        Assert.Empty(await reopened.GetAllAsync(fixture.Token));
        await grants.AddAsync(scope with { GrantId = Guid.NewGuid(), AccountRevision = updated.Revision }, fixture.Token);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task WriteGrantRoundTripsCanWriteAndRevocationStillBlocksReadd()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var grants = new SqliteSourceRepositoryGrants(fixture.Connections);
        var write = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", true);
        await grants.AddAsync(write, fixture.Token);
        var reopened = new SqliteSourceRepositoryGrants(fixture.Connections);
        Assert.Equal(write, await reopened.GetAsync(write.GrantId, fixture.Token));
        Assert.True(Assert.Single(await reopened.GetAllAsync(fixture.Token)).CanWrite);
        await grants.RevokeAsync(write.GrantId, fixture.Token);
        await Assert.ThrowsAsync<SourceReviewException>(() => grants.AddAsync(write, fixture.Token));
        var read = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", false);
        await grants.AddAsync(read, fixture.Token);
        Assert.False(Assert.Single(await reopened.GetAllAsync(fixture.Token)).CanWrite);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task WriteResolveRequiresExplicitWriteGrantAndLiveAccountRevision()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var grants = new SqliteSourceRepositoryGrants(fixture.Connections);
        var write = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", true);
        var read = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "other", false);
        await grants.AddAsync(write, fixture.Token);
        await grants.AddAsync(read, fixture.Token);
        var access = new SourceRepositoryAccess(grants, accounts);
        // Read resolution is unchanged: both grant kinds pass it.
        Assert.Equal(account.Id, (await access.ResolveAsync(read, fixture.Token)).Id);
        Assert.Equal(account.Id, (await access.ResolveAsync(write, fixture.Token)).Id);
        Assert.Equal(account.Id, (await access.ResolveWriteAsync(write, fixture.Token)).Id);
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(
            () => access.ResolveWriteAsync(read, fixture.Token))).ErrorCode);
        // A forged write flag or a forged downgrade never satisfies either resolution.
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(
            () => access.ResolveWriteAsync(read with { CanWrite = true }, fixture.Token))).ErrorCode);
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(
            () => access.ResolveWriteAsync(write with { CanWrite = false }, fixture.Token))).ErrorCode);
        // Stale account revision still denies write resolution on a write grant.
        var updated = await accounts.SaveAsync(account, fixture.Token);
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(
            () => access.ResolveWriteAsync(write, fixture.Token))).ErrorCode);
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(
            () => access.ResolveWriteAsync(write with { AccountRevision = updated.Revision }, fixture.Token))).ErrorCode);
        await grants.RevokeAsync(write.GrantId, fixture.Token);
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(
            () => access.ResolveWriteAsync(write with { AccountRevision = updated.Revision }, fixture.Token))).ErrorCode);
    }

    [Theory]
    [InlineData("../owner")]
    [InlineData("a%2fb")]
    [InlineData("a/b")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a\\b")]
    [InlineData("owner?secret=1")]
    [InlineData("owner name")]
    public void RepositoryNamesCannotCreatePathsOrQueries(string value) =>
        Assert.Throws<ArgumentException>(() => SourceRepositoryScope.ValidateSegment(value));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevokedScopeCannotPublishAnInflightRead(bool changeAccount)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var accounts = new SqliteSourceAccountRepository(fixture.Connections);
        var account = await accounts.SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var grants = new SqliteSourceRepositoryGrants(fixture.Connections);
        var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo", false);
        await grants.AddAsync(scope, fixture.Token);
        var probe = new SourceAccountHttpProbe { RespondAsync = async (_, token) =>
        {
            if (changeAccount) await accounts.SaveAsync(account, token);
            else await grants.RevokeAsync(scope.GrantId, token);
            return new(System.Net.HttpStatusCode.OK) { Content = new StringContent($"[{PullRequestTestData.Item()}]") };
        } };
        var reader = new SourceReviewReader(grants, accounts, new GitHubPullRequestClient(probe));
        Assert.Equal("source_scope_denied", (await Assert.ThrowsAsync<SourceReviewException>(() => reader.ListAsync(scope, 1, fixture.Token))).ErrorCode);
        Assert.Equal(1, probe.DisposedHandlers);
    }
}
