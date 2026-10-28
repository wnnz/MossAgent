using Microsoft.Data.Sqlite;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationAttachmentTests
{
    [Fact]
    public async Task OrderedMetadataRoundTripsWithoutChangingPromptAndCascadesWithProject()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var message = CreateMessage(fixture.Task.Id);
        var first = CreateAttachment(message, "first.txt");
        var second = CreateAttachment(message, "second.txt");
        await fixture.Repository.SaveTaskWithMessagesAsync(fixture.Task, [message with { Attachments = [first, second] }], fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        var restored = Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(message.Content, restored.Content);
        Assert.Equal([first, second], restored.Attachments);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_table_info('conversation_attachments') WHERE name='content';"));
        await fixture.ExecuteAsync($"DELETE FROM projects WHERE id='{fixture.Project.Id}';");
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_attachments;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Theory]
    [InlineData("task")]
    [InlineData("message")]
    [InlineData("role")]
    [InlineData("duplicate")]
    [InlineData("projectless-role")]
    public async Task InvalidOwnerRollsBackTaskMessageAndAllReferences(string kind)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var task = kind == "projectless-role" ? fixture.Task with { Id = Guid.NewGuid(), ProjectId = null } : fixture.Task;
        var message = CreateMessage(task.Id);
        var attachment = CreateAttachment(message, "selected.txt");
        message = kind switch
        {
            "task" => message with { Attachments = [attachment with { TaskId = Guid.NewGuid() }] },
            "message" => message with { Attachments = [attachment with { MessageId = Guid.NewGuid() }] },
            "role" or "projectless-role" => message with { Role = MessageRole.Assistant, Attachments = [attachment] },
            "duplicate" => message with { Attachments = [attachment, attachment] },
            _ => message with { Attachments = [attachment] }
        };
        var failure = await Record.ExceptionAsync(() => fixture.Repository.SaveTaskWithMessagesAsync(
            task with { Title = "must roll back" }, [message], fixture.Token));
        Assert.True(failure is UnauthorizedAccessException or InvalidDataException);
        Assert.Empty(await fixture.Repository.GetMessagesAsync(task.Id, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_attachments;"));
        Assert.Empty(await fixture.Repository.GetTasksAsync(null, fixture.Token));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MetadataInsertionFailureRollsBackAppendAndTaskBatch(bool append)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var message = CreateMessage(fixture.Task.Id);
        message = message with { Attachments = [CreateAttachment(message, "selected.txt")] };
        await fixture.ExecuteAsync("""
            CREATE TRIGGER reject_attachment BEFORE INSERT ON conversation_attachments
            BEGIN SELECT RAISE(ABORT,'synthetic refusal'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => append
            ? fixture.Repository.AppendMessageAsync(message, fixture.Token)
            : fixture.Repository.SaveTaskWithMessagesAsync(fixture.Task with { Title = "must roll back" }, [message], fixture.Token));
        Assert.Empty(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(fixture.Task, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_attachments;"));
    }

    [Fact]
    public async Task DatabaseCompositeForeignKeyRejectsCrossTaskReferences()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var message = CreateMessage(fixture.Task.Id);
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        var other = fixture.Task with { Id = Guid.NewGuid() };
        await fixture.Repository.SaveTaskAsync(other, fixture.Token);
        var exception = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync($"""
            INSERT INTO conversation_attachments(id,task_id,message_id,ordinal,display_path,byte_count,sha256)
            VALUES('{Guid.NewGuid()}','{other.Id}','{message.Id}',0,
                'foreign.txt',1,'{new string('A', 64)}');
            """));
        Assert.Equal(787, exception.SqliteExtendedErrorCode);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_attachments;"));
    }

    [Fact]
    public async Task VersionThirteenUpgradeRollsBackOnFailureAndRetriesWithoutLosingData()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var message = CreateMessage(fixture.Task.Id);
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        await fixture.Repository.SaveTaskAsync(fixture.Task with { FailureReason = TaskFailureReason.ProxyAuthentication }, fixture.Token);
        await fixture.ExecuteAsync("""
            DROP TABLE conversation_draft_attachments;
            DROP TABLE conversation_attachments;
            DROP INDEX ix_messages_identity;
            DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=14;
            CREATE TRIGGER reject_version_fourteen BEFORE INSERT ON schema_versions
            WHEN NEW.version=14 BEGIN SELECT RAISE(FAIL,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(13, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name IN ('conversation_attachments','agent_task_failures_v14','ix_messages_identity');"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM messages;"));
        Assert.Equal((int)TaskFailureReason.ProxyAuthentication, await fixture.CountAsync("SELECT reason FROM agent_task_failures;"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_fourteen;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(51, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(message, Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token)));
        await fixture.Repository.SaveTaskAsync(fixture.Task with { FailureReason = TaskFailureReason.AttachmentHistory }, fixture.Token);
        Assert.Equal(15, await fixture.CountAsync("SELECT reason FROM agent_task_failures;"));
    }

    private static ConversationMessage CreateMessage(Guid taskId) =>
        new(Guid.NewGuid(), taskId, MessageRole.User, "original prompt", DateTimeOffset.UtcNow);

    private static ConversationAttachment CreateAttachment(ConversationMessage message, string displayPath) =>
        new(Guid.NewGuid(), message.TaskId, message.Id, displayPath, 10, new string('A', 64));
}
