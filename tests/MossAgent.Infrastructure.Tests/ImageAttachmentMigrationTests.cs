using Microsoft.Data.Sqlite;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ImageAttachmentMigrationTests
{
    [Fact]
    public async Task VersionFifteenUpgradePreservesTextSnapshotsAndRollsBackBothTablesBeforeRetry()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var message = await SeedTextAsync(fixture);
        await RestoreVersionFifteenAsync(fixture);
        await fixture.ExecuteAsync("""
            CREATE TRIGGER reject_version_sixteen BEFORE INSERT ON schema_versions
            WHEN NEW.version=16 BEGIN SELECT RAISE(FAIL,'synthetic migration failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Initializer.InitializeAsync(fixture.Token));
        Assert.Equal(15, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM sqlite_master WHERE name LIKE '%_v16';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_table_info('conversation_attachments') WHERE name='media_type';"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_table_info('conversation_draft_attachments') WHERE name='media_type';"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_attachments WHERE byte_count=15;"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_draft_attachments WHERE byte_count=15;"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_drafts WHERE text='legacy draft' AND revision=1;"));
        await fixture.ExecuteAsync("DROP TRIGGER reject_version_sixteen;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);
        Assert.Equal(41, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        var restored = Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(message with { Attachments = null }, restored with { Attachments = null });
        Assert.Equal(message.Attachments!.ToArray(), restored.Attachments!.ToArray());
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Project.PrimaryDirectory));
        var content = Assert.Single(await new ConversationAttachmentStore(paths).ReadAsync(fixture.Task.Id, message.Id, restored.Attachments!, fixture.Token));
        Assert.Equal("legacy snapshot", content.Content);
        Assert.Equal("text/plain", content.MediaType);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var draft = (await ConversationDraftAttachmentTestData.Repository(fixture).GetAsync(scope, fixture.Token))!;
        Assert.Equal("legacy draft", draft.Text);
        Assert.Equal("legacy snapshot", Assert.Single(draft.AttachmentItems).Content);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
        await fixture.ExecuteAsync($"DELETE FROM projects WHERE id='{fixture.Project.Id}';");
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_attachments;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_draft_attachments;"));
    }

    [Theory]
    [InlineData("image/png", 0)]
    [InlineData("image/png", 5242881)]
    [InlineData("image/svg+xml", 1)]
    [InlineData("text/plain", 131073)]
    [InlineData("text/plain", -1)]
    public async Task BothMetadataTablesRejectInvalidMediaAndByteCountsWithoutChangingSavedRows(string mediaType, long byteCount)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await SeedTextAsync(fixture);
        foreach (var table in new[] { "conversation_attachments", "conversation_draft_attachments" })
        {
            var exception = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
                $"UPDATE {table} SET media_type='{mediaType}',byte_count={byteCount};"));
            Assert.Equal(19, exception.SqliteErrorCode);
            Assert.Equal(1, await fixture.CountAsync($"SELECT COUNT(*) FROM {table} WHERE media_type='text/plain' AND byte_count=15;"));
        }
    }

    [Fact]
    public async Task CancelledUpgradeLeavesVersionFifteenAndItsDataUnchanged()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await SeedTextAsync(fixture);
        await RestoreVersionFifteenAsync(fixture);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Initializer.InitializeAsync(cancellation.Token));
        Assert.Equal(15, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_attachments;"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_draft_attachments;"));
    }

    private static async Task<ConversationMessage> SeedTextAsync(SqliteFailurePersistenceFixture fixture)
    {
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Project.PrimaryDirectory));
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User, "legacy message", DateTimeOffset.UtcNow);
        var metadata = await new ConversationAttachmentStore(paths).SaveAsync(fixture.Task.Id, message.Id,
            [new("never-read.txt", "legacy.txt", "legacy snapshot", 15)], fixture.Token);
        message = message with { Attachments = metadata };
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var attachment = ConversationDraftAttachmentTestData.Attachment(fixture, "legacy snapshot");
        Assert.True(await ConversationDraftAttachmentTestData.Repository(fixture).SaveAsync([new(scope, "legacy draft", 0, [attachment])], fixture.Token));
        return message;
    }

    private static Task RestoreVersionFifteenAsync(SqliteFailurePersistenceFixture fixture) => fixture.ExecuteAsync("""
        CREATE TABLE legacy_attachments (
            id TEXT PRIMARY KEY, task_id TEXT NOT NULL, message_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL CHECK(typeof(ordinal)='integer' AND ordinal BETWEEN 0 AND 9),
            display_path TEXT NOT NULL CHECK(length(display_path) BETWEEN 1 AND 1024),
            byte_count INTEGER NOT NULL CHECK(typeof(byte_count)='integer' AND byte_count BETWEEN 0 AND 131072),
            sha256 TEXT NOT NULL CHECK(length(sha256)=64 AND sha256 NOT GLOB '*[^0-9A-Fa-f]*'),
            UNIQUE(message_id,ordinal),
            FOREIGN KEY(message_id,task_id) REFERENCES messages(id,task_id) ON DELETE CASCADE
        );
        INSERT INTO legacy_attachments SELECT id,task_id,message_id,ordinal,display_path,byte_count,sha256 FROM conversation_attachments;
        DROP TABLE conversation_attachments;
        ALTER TABLE legacy_attachments RENAME TO conversation_attachments;
        CREATE INDEX ix_conversation_attachments_task ON conversation_attachments(task_id,message_id,ordinal);
        CREATE TABLE legacy_draft_attachments (
            scope_key TEXT NOT NULL REFERENCES conversation_drafts(scope_key) ON DELETE CASCADE,
            attachment_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL CHECK(typeof(ordinal)='integer' AND ordinal BETWEEN 0 AND 9),
            source_path TEXT NOT NULL CHECK(length(source_path) BETWEEN 1 AND 32768),
            display_path TEXT NOT NULL CHECK(length(display_path) BETWEEN 1 AND 1024),
            byte_count INTEGER NOT NULL CHECK(typeof(byte_count)='integer' AND byte_count BETWEEN 0 AND 131072),
            sha256 TEXT NOT NULL CHECK(length(sha256)=64 AND sha256 NOT GLOB '*[^0-9A-Fa-f]*'),
            authorization_stamp TEXT NOT NULL CHECK(length(authorization_stamp)=64 AND authorization_stamp NOT GLOB '*[^0-9A-Fa-f]*'),
            PRIMARY KEY(scope_key,attachment_id), UNIQUE(scope_key,ordinal)
        );
        INSERT INTO legacy_draft_attachments SELECT scope_key,attachment_id,ordinal,source_path,display_path,byte_count,sha256,authorization_stamp FROM conversation_draft_attachments;
        DROP TABLE conversation_draft_attachments;
        ALTER TABLE legacy_draft_attachments RENAME TO conversation_draft_attachments;
        CREATE TRIGGER validate_draft_attachment_scope BEFORE INSERT ON conversation_draft_attachments
        WHEN NOT EXISTS(SELECT 1 FROM conversation_drafts WHERE scope_key=NEW.scope_key AND project_id IS NOT NULL)
        BEGIN SELECT RAISE(ABORT,'Invalid draft attachment scope.'); END;
        DROP TRIGGER project_authorization_changed; ALTER TABLE projects DROP COLUMN authorization_revision; DELETE FROM schema_versions WHERE version>=16;
        """);
}
