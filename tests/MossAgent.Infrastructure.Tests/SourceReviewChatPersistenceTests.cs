using Microsoft.Data.Sqlite;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SourceReviewChatPersistenceTests
{
    [Fact]
    public async Task ReopenPinsTheFirstSnapshotAndSeparatesRevisionsWithoutLocalEnvironments()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var binding = await BindingAsync(fixture);
        var chats = new SqliteSourceReviewChats(fixture.Connections);
        var task = await chats.OpenAsync(binding, "PR review", fixture.Token);
        Assert.Null(task.ProjectId); Assert.Null(task.WorktreePath); Assert.Equal(ApprovalPolicy.ReadOnly, task.ApprovalPolicy);
        var message = new ConversationMessage(Guid.NewGuid(), task.Id, MessageRole.User, "saved question", DateTimeOffset.UtcNow);
        await fixture.Repository.SaveTaskWithMessagesAsync(task, [message], fixture.Token);
        var reopened = new SqliteSourceReviewChats(fixture.Connections);
        var same = await reopened.OpenAsync(binding with { TaskId = Guid.NewGuid(), Context = "different snapshot",
            Revision = binding.Revision with { HeadRevision = binding.Revision.HeadRevision.ToUpperInvariant() } }, "other title", fixture.Token);
        Assert.Equal(task.Id, same.Id); Assert.Equal(binding.Context, (await reopened.GetAsync(task.Id, fixture.Token))!.Context);
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token)));
        var changed = await chats.OpenAsync(binding with { TaskId = Guid.NewGuid(), Revision = binding.Revision with { HeadRevision = new string('c', 40) } }, "new head", fixture.Token);
        Assert.NotEqual(task.Id, changed.Id); Assert.Equal(2, (await fixture.Repository.GetTasksAsync(null, fixture.Token)).Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Repository.SaveTaskAsync(task with { ProjectId = fixture.Project.Id }, fixture.Token));
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Repository.SaveTaskAsync(task with { ApprovalPolicy = ApprovalPolicy.FullAccess }, fixture.Token));
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Repository.SaveTaskAsync(task with { WorktreePath = fixture.Project.PrimaryDirectory }, fixture.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevocationAndCredentialChangesBlockOpeningAndRuntimeResolution(bool credentials)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var binding = await BindingAsync(fixture);
        var chats = new SqliteSourceReviewChats(fixture.Connections);
        var task = await chats.OpenAsync(binding, "PR review", fixture.Token);
        var accounts = new SqliteSourceAccountRepository(fixture.Connections); var grants = new SqliteSourceRepositoryGrants(fixture.Connections);
        var access = new SourceReviewChatAccess(chats, new(grants, accounts));
        Assert.NotNull(await access.ResolveAsync(task, fixture.Token));
        if (credentials) await accounts.SaveAsync((await accounts.GetAsync(binding.Scope.AccountId, fixture.Token))!, fixture.Token);
        else await grants.RevokeAsync(binding.Scope.GrantId, fixture.Token);
        await Assert.ThrowsAsync<SourceReviewException>(() => chats.OpenAsync(binding, "PR review", fixture.Token));
        await Assert.ThrowsAsync<SourceReviewException>(() => access.ResolveAsync(task, fixture.Token));
        Assert.Single(await fixture.Repository.GetTasksAsync(null, fixture.Token));
    }

    [Fact]
    public async Task DraftPreparationDoesNotSendOrOverwriteExistingTextOrOtherScopes()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var binding = await BindingAsync(fixture);
        var chats = new SqliteSourceReviewChats(fixture.Connections);
        var drafts = ConversationDraftAttachmentTestData.Repository(fixture);
        var creator = new SourceReviewChatCreator(chats, new(new SqliteSourceRepositoryGrants(fixture.Connections), new SqliteSourceAccountRepository(fixture.Connections)), drafts);
        var task = await creator.OpenAsync(binding, "PR review", fixture.Token);
        var unrelated = new ConversationDraftScope(null, null);
        Assert.True(await drafts.SaveAsync([new(unrelated, "ordinary draft", 0)], fixture.Token));
        Assert.True(await creator.PrepareDraftAsync(task, binding.Scope, "investigate this failure", fixture.Token));
        Assert.False(await creator.PrepareDraftAsync(task, binding.Scope, "replacement", fixture.Token));
        Assert.Equal("ordinary draft", (await drafts.GetAsync(unrelated, fixture.Token))!.Text);
        Assert.Equal("investigate this failure", (await drafts.GetAsync(new(null, task.Id), fixture.Token))!.Text);
        Assert.Empty(await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token));
        await Assert.ThrowsAsync<SourceReviewException>(() => creator.PrepareDraftAsync(task, binding.Scope with { Repository = "other" }, "replacement", fixture.Token));
    }

    [Fact]
    public async Task VersionNineteenMigrationRollsBackAndRetainsExistingUserData()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE source_review_chats; DROP TRIGGER source_review_chat_environment;
            DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=20;
            CREATE TRIGGER reject_twenty BEFORE INSERT ON schema_versions WHEN NEW.version=20
            BEGIN SELECT RAISE(FAIL,'synthetic migration refusal'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(19, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='source_review_chats';"));
        Assert.Equal(fixture.Task.Id, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)).Id);
        await fixture.ExecuteAsync("DROP TRIGGER reject_twenty;");
        await fixture.Initializer.InitializeAsync(fixture.Token); await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(43, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
    }

    private static async Task<SourceReviewChatBinding> BindingAsync(SqliteFailurePersistenceFixture fixture)
    {
        var account = await new SqliteSourceAccountRepository(fixture.Connections).SaveAsync(SourceAccountValidationTests.Create(), fixture.Token);
        var scope = new SourceRepositoryScope(Guid.NewGuid(), account.Id, account.Revision, "fixture", "repo");
        await new SqliteSourceRepositoryGrants(fixture.Connections).AddAsync(scope, fixture.Token);
        return new(Guid.NewGuid(), scope, new(7, new string('a', 40), new string('b', 40)), "immutable untrusted snapshot");
    }
}
