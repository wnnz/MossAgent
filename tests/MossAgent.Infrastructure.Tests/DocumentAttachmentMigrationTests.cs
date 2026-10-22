using Microsoft.Data.Sqlite;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class DocumentAttachmentMigrationTests
{
    [Fact]
    public async Task FailedVersionThirtyFourMigrationRollsBackBothTablesAndVersion()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var message = await SeedTextAsync(fixture);
        await fixture.ExecuteAsync("""
            DELETE FROM schema_versions WHERE version=34;
            CREATE TRIGGER reject_v34 BEFORE INSERT ON schema_versions
            WHEN NEW.version=34 BEGIN SELECT RAISE(FAIL,'synthetic PDF migration failure'); END;
            """);

        await Assert.ThrowsAsync<SqliteException>(() =>
            fixture.Initializer.InitializeAsync(fixture.Token));

        Assert.Equal(33, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM conversation_attachments WHERE id='" + message.Attachments![0].Id + "';"));
        Assert.Equal(1, await fixture.CountAsync(
            "SELECT COUNT(*) FROM conversation_draft_attachments WHERE attachment_id='" + message.Attachments[0].Id + "';"));
        Assert.Equal(0, await fixture.CountAsync(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name LIKE '%_v34';"));

        await fixture.ExecuteAsync("DROP TRIGGER reject_v34;");
        await fixture.Initializer.InitializeAsync(fixture.Token);
        await fixture.Initializer.InitializeAsync(fixture.Token);

        Assert.Equal(34, await fixture.CountAsync("SELECT COUNT(*) FROM schema_versions;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task BothAttachmentTablesAcceptExactPdfLimitAndRejectOneByteMore()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        await SeedTextAsync(fixture);
        foreach (var table in new[] { "conversation_attachments", "conversation_draft_attachments" })
        {
            await fixture.ExecuteAsync(
                $"UPDATE {table} SET media_type='application/pdf',byte_count={AttachmentDocument.MaximumByteCount};");
            Assert.Equal(1, await fixture.CountAsync(
                $"SELECT COUNT(*) FROM {table} WHERE media_type='application/pdf' "
                + $"AND byte_count={AttachmentDocument.MaximumByteCount};"));

            var error = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
                $"UPDATE {table} SET byte_count={AttachmentDocument.MaximumByteCount + 1};"));
            Assert.Equal(19, error.SqliteErrorCode);
            Assert.Equal(1, await fixture.CountAsync(
                $"SELECT COUNT(*) FROM {table} WHERE media_type='application/pdf' "
                + $"AND byte_count={AttachmentDocument.MaximumByteCount};"));
        }
    }

    private static async Task<ConversationMessage> SeedTextAsync(SqliteFailurePersistenceFixture fixture)
    {
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Project.PrimaryDirectory));
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User,
            "legacy", DateTimeOffset.UtcNow);
        var metadata = await new ConversationAttachmentStore(paths).SaveAsync(
            fixture.Task.Id, message.Id,
            [new("never-read.txt", "legacy.txt", "snapshot", 8)], fixture.Token);
        message = message with { Attachments = metadata };
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var attachment = ConversationDraftAttachmentTestData.Attachment(fixture, "snapshot");
        attachment = attachment with { Id = metadata[0].Id };
        Assert.True(await ConversationDraftAttachmentTestData.Repository(fixture)
            .SaveAsync([new(scope, "draft", 0, [attachment])], fixture.Token));
        return message;
    }
}
