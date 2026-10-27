using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ConversationFollowupMigrationTests
{
    [Fact]
    public async Task ForwardMigrationIsAtomicRetryableAndPreservesExistingConversationData()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User, "existing message", DateTimeOffset.UtcNow);
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        var scope = new ConversationDraftScope(fixture.Task.ProjectId, fixture.Task.Id);
        var drafts = new SqliteConversationDraftRepository(fixture.Connections);
        Assert.True(await drafts.SaveAsync([new(scope, "existing draft", 0)], fixture.Token));
        await fixture.ExecuteAsync("""
            DROP TABLE conversation_followups;
            DROP TABLE conversation_followup_queues;
            DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=23;
            CREATE TRIGGER reject_followup_version BEFORE INSERT ON schema_versions
            WHEN NEW.version=23 BEGIN SELECT RAISE(ABORT,'synthetic follow-up migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(22, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name IN ('conversation_followups','conversation_followup_queues');"));
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token)));
        Assert.Equal(new("existing draft", 1), await drafts.GetAsync(scope, fixture.Token));
        await fixture.ExecuteAsync("DROP TRIGGER reject_followup_version;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(46, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(2, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name IN ('conversation_followups','conversation_followup_queues');"));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Task.ProjectId, fixture.Token)));
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token)));
        Assert.Equal(new("existing draft", 1), await drafts.GetAsync(scope, fixture.Token));
    }
}
