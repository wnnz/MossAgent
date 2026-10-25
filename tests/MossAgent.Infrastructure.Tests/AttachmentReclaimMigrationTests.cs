using Microsoft.Data.Sqlite;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AttachmentReclaimMigrationTests
{
    [Fact]
    public async Task FreshDatabaseCreatesReclaimJournalAtVersionThirtyThree()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();

        Assert.Equal(39, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='attachment_reclaim_intents';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM attachment_reclaim_intents;"));
        var invalid = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync("""
            INSERT INTO attachment_reclaim_intents(path,byte_count,stage,error_code,created_at,updated_at)
            VALUES('x',-1,9,NULL,'now','now');
            """));
        Assert.Equal(19, invalid.SqliteErrorCode);
        var oversized = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
            "INSERT INTO attachment_reclaim_intents(path,byte_count,stage,error_code,created_at,updated_at) "
            + "VALUES('x',1,2,'" + new string('e', 65) + "','now','now');"));
        Assert.Equal(19, oversized.SqliteErrorCode);
    }

    [Fact]
    public async Task ThirtyThreeMigrationIsForwardOnlyAndPreservesExistingRows()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var paths = new AppDataPaths(fixture.Project.PrimaryDirectory);
        var messageId = Guid.NewGuid();
        var snapshots = await new ConversationAttachmentStore(new TaskArtifactPaths(paths))
            .SaveAsync(fixture.Task.Id, messageId, [new("note.txt", "note.txt", "payload", 7)], fixture.Token);
        var message = new ConversationMessage(messageId, fixture.Task.Id, MessageRole.User, "seed",
            DateTimeOffset.UtcNow) { Attachments = snapshots };
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);

        await fixture.ExecuteAsync("""
            DROP TABLE attachment_reclaim_intents;
            DELETE FROM schema_versions WHERE version>=33;
            CREATE TRIGGER reject_version_thirty_three BEFORE INSERT ON schema_versions
            WHEN NEW.version=33 BEGIN SELECT RAISE(ABORT,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(32, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE name='attachment_reclaim_intents';"));

        await fixture.ExecuteAsync("DROP TRIGGER reject_version_thirty_three;");
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(39, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM conversation_attachments WHERE message_id='" + messageId.ToString("D") + "';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }
}
