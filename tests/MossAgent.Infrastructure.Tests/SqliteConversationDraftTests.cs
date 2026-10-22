using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationDraftTests
{
    [Fact]
    public async Task DraftsRoundTripAcrossRepositoryInstancesWithoutChangingTasksOrMessages()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationDraftRepository(fixture.Connections);
        var chat = fixture.Task with { Id = Guid.NewGuid(), ProjectId = null };
        await fixture.Repository.SaveTaskAsync(chat, fixture.Token);
        ConversationDraftScope[] scopes = [new(null, null), new(fixture.Project.Id, null),
            new(fixture.Project.Id, fixture.Task.Id), new(null, chat.Id)];
        var updates = scopes.Select((scope, index) => new ConversationDraftUpdate(scope, $"  草稿 {index}\r\n", 0)).ToArray();

        Assert.True(await repository.SaveAsync(updates, fixture.Token));

        var reopened = new SqliteConversationDraftRepository(fixture.Connections);
        foreach (var update in updates)
            Assert.Equal(new(update.Text, 1), await reopened.GetAsync(update.Scope, fixture.Token));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
        Assert.Empty(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM tool_executions;"));
    }

    [Fact]
    public async Task ConcurrentWritersAndClearedDraftsCannotBypassTheExpectedRevision()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var first = new SqliteConversationDraftRepository(fixture.Connections);
        var second = new SqliteConversationDraftRepository(fixture.Connections);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        Assert.True(await first.SaveAsync([new(scope, "first", 0)], fixture.Token));
        var results = await Task.WhenAll(first.SaveAsync([new(scope, "writer one", 1)], fixture.Token),
            second.SaveAsync([new(scope, "writer two", 1)], fixture.Token));
        Assert.Single(results, result => result);
        Assert.Equal(2, (await first.GetAsync(scope, fixture.Token))!.Revision);
        Assert.True(await first.SaveAsync([new(scope, string.Empty, 2)], fixture.Token));
        Assert.False(await second.SaveAsync([new(scope, "stale resurrection", 0)], fixture.Token));
        Assert.Equal(new(string.Empty, 3), await first.GetAsync(scope, fixture.Token));
    }

    [Fact]
    public async Task ConflictingPromotionRollsBackAllScopesAndCanBeRetriedAtomically()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationDraftRepository(fixture.Connections);
        var source = new ConversationDraftScope(fixture.Project.Id, null);
        var target = source with { TaskId = fixture.Task.Id };
        Assert.True(await repository.SaveAsync([new(source, "sent", 0), new(target, "other window", 0)], fixture.Token));

        Assert.False(await repository.SaveAsync([new(source, string.Empty, 1), new(target, "next", 0)], fixture.Token));

        Assert.Equal(new("sent", 1), await repository.GetAsync(source, fixture.Token));
        Assert.Equal(new("other window", 1), await repository.GetAsync(target, fixture.Token));
        Assert.True(await repository.SaveAsync([new(source, string.Empty, 1), new(target, "next", 1)], fixture.Token));
        Assert.Equal(new(string.Empty, 2), await repository.GetAsync(source, fixture.Token));
        Assert.Equal(new("next", 2), await repository.GetAsync(target, fixture.Token));
    }

    [Fact]
    public async Task MissingArchivedAndForeignScopesCannotReadOrOverwriteDrafts()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationDraftRepository(fixture.Connections);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        Assert.True(await repository.SaveAsync([new(scope, "owned", 0)], fixture.Token));
        ConversationDraftScope[] invalid = [new(Guid.NewGuid(), null), new(fixture.Project.Id, Guid.NewGuid()),
            new(null, fixture.Task.Id), new(Guid.NewGuid(), fixture.Task.Id)];
        foreach (var candidate in invalid)
        {
            Assert.Null(await repository.GetAsync(candidate, fixture.Token));
            Assert.False(await repository.SaveAsync([new(candidate, "foreign", 1)], fixture.Token));
        }
        await fixture.Repository.SetTaskArchivedAsync(fixture.Task.Id, true, fixture.Token);
        Assert.Null(await repository.GetAsync(scope, fixture.Token));
        Assert.False(await repository.SaveAsync([new(scope, "archived", 1)], fixture.Token));
        await fixture.Repository.SetTaskArchivedAsync(fixture.Task.Id, false, fixture.Token);
        Assert.Equal(new("owned", 1), await repository.GetAsync(scope, fixture.Token));
    }

    [Fact]
    public async Task InvalidUpdatesAndCancellationLeaveTheWholeBatchUnchanged()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = new SqliteConversationDraftRepository(fixture.Connections);
        var scope = new ConversationDraftScope(null, null);
        ConversationDraftUpdate[] invalid = [new(scope, new('x', ConversationDraft.MaximumTextLength + 1), 0),
            new(scope, "invalid revision", -1), new(scope, "overflow", long.MaxValue), new(new(Guid.Empty, null), "invalid scope", 0)];
        foreach (var update in invalid)
            await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync([update], fixture.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(
            [new(scope, "first", 0), new(scope, "duplicate", 0)], fixture.Token));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.SaveAsync([new(scope, "cancelled", 0)], cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetAsync(scope, cancellation.Token));
        Assert.Null(await repository.GetAsync(scope, fixture.Token));
    }

    [Fact]
    public async Task VersionTwelveUpgradePreservesHistoryIsIdempotentAndMaintainsCascades()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User, "preserved", DateTimeOffset.UtcNow);
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        await fixture.ExecuteAsync("DROP TABLE conversation_draft_attachments; DROP TABLE conversation_attachments; DROP INDEX ix_messages_identity; DROP TABLE conversation_drafts; DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=13;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(29, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token)));
        var repository = new SqliteConversationDraftRepository(fixture.Connections);
        Assert.True(await repository.SaveAsync([new(new(fixture.Project.Id, fixture.Task.Id), "task draft", 0),
            new(new(fixture.Project.Id, null), "project draft", 0), new(new(null, null), "plain", 0)], fixture.Token));
        await fixture.ExecuteAsync($"DELETE FROM projects WHERE id='{fixture.Project.Id}';");
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_drafts;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FailedMigrationRollsBackWithoutDeletingHistoryAndSupportsRetry()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            DROP TABLE conversation_draft_attachments;
            DROP TABLE conversation_attachments;
            DROP INDEX ix_messages_identity;
            DROP TABLE conversation_drafts;
            DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=13;
            CREATE TRIGGER reject_version_thirteen BEFORE INSERT ON schema_versions
            WHEN NEW.version=13 BEGIN SELECT RAISE(FAIL, 'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name='conversation_drafts';"));
        Assert.Equal(12, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_thirteen;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(29, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
    }
}
