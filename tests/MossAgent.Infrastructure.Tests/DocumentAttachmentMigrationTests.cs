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
    public async Task VersionThirtyTwoAcceptsPdfAndRejectsInvalidRowsWithoutChangingSavedData()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var message = await SeedAsync(fixture);
        foreach (var table in new[] { "conversation_attachments", "conversation_draft_attachments" })
        {
            await fixture.ExecuteAsync($"UPDATE {table} SET media_type='application/pdf',byte_count=10485760;");
            var rejected = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
                $"UPDATE {table} SET byte_count=10485761 WHERE media_type='application/pdf';"));
            Assert.Equal(19, rejected.SqliteErrorCode);
            var empty = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
                $"UPDATE {table} SET byte_count=0 WHERE media_type='application/pdf';"));
            Assert.Equal(19, empty.SqliteErrorCode);
            var other = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync(
                $"UPDATE {table} SET media_type='application/msword',byte_count=1024;"));
            Assert.Equal(19, other.SqliteErrorCode);
            await fixture.ExecuteAsync($"UPDATE {table} SET media_type='text/plain',byte_count=10;");
        }
        var restored = Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(message.Attachments!.ToArray(), restored.Attachments!.ToArray());
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task FreshDatabaseStoresAndReloadsPdfConversationAndDraftAttachments()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var pdf = AttachmentDocumentTestData.Pdf("persisted document");
        var extraction = await new PdfTextExtractor().ExtractAsync(pdf, fixture.Token);
        var document = AttachmentDocument.FromExtractedText(AttachmentDocument.PdfMediaType, pdf,
            extraction.PageCount, extraction.TotalPages, extraction.Text.Length, extraction.Truncated);
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Project.PrimaryDirectory));
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User, "look", DateTimeOffset.UtcNow);
        var metadata = await new ConversationAttachmentStore(paths, documents: new PdfTextExtractor())
            .SaveAsync(fixture.Task.Id, message.Id,
                [new("report.pdf", "report.pdf", extraction.Text, pdf.LongLength, Document: document)], fixture.Token);
        message = message with { Attachments = metadata };
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var draft = new ConversationDraftAttachment(Guid.NewGuid(),
            Path.Combine(fixture.Project.PrimaryDirectory, "report.pdf"), "report.pdf", extraction.Text,
            pdf.LongLength, WorkspaceAttachmentAuthorization.Create(fixture.Project),
            AttachmentDocument.PdfMediaType, Document: document);
        var drafts = new SqliteConversationDraftRepository(fixture.Connections,
            new AppDataPaths(fixture.Project.PrimaryDirectory), documents: new PdfTextExtractor());
        Assert.True(await drafts.SaveAsync([new(scope, "with pdf", 0, [draft])], fixture.Token));
        var restored = Assert.Single(await fixture.Repository.GetMessagesAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal("application/pdf", Assert.Single(restored.Attachments!).MediaType);
        var content = Assert.Single(await new ConversationAttachmentStore(paths, documents: new PdfTextExtractor())
            .ReadAsync(fixture.Task.Id, message.Id, restored.Attachments!, fixture.Token));
        Assert.Contains("persisted document", content.Content);
        var restoredDraft = (await drafts.GetAsync(scope, fixture.Token))!;
        Assert.Equal(AttachmentDocument.PdfMediaType, Assert.Single(restoredDraft.AttachmentItems).MediaType);
    }

    private static async Task<ConversationMessage> SeedAsync(SqliteFailurePersistenceFixture fixture)
    {
        var paths = new TaskArtifactPaths(new AppDataPaths(fixture.Project.PrimaryDirectory));
        var message = new ConversationMessage(Guid.NewGuid(), fixture.Task.Id, MessageRole.User, "seed", DateTimeOffset.UtcNow);
        var metadata = await new ConversationAttachmentStore(paths).SaveAsync(fixture.Task.Id, message.Id,
            [new("seed.txt", "seed.txt", "seed bytes", 10)], fixture.Token);
        message = message with { Attachments = metadata };
        await fixture.Repository.AppendMessageAsync(message, fixture.Token);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var repository = new SqliteConversationDraftRepository(fixture.Connections,
            new AppDataPaths(fixture.Project.PrimaryDirectory), documents: new PdfTextExtractor());
        Assert.True(await repository.SaveAsync(
            [new(scope, "seed draft", 0, [ConversationDraftAttachmentTestData.Attachment(fixture, "seed bytes")])], fixture.Token));
        return message;
    }
}
