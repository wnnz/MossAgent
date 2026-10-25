using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class DocumentDraftAttachmentTests
{
    [Fact]
    public async Task PdfDraftReopensWithReextractedTextAndBinarySnapshot()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var pdf = AttachmentDocumentTestData.Pdf("draft pdf body");
        var original = await AttachmentAsync(fixture, pdf, "selected.pdf");
        var repository = Repository(fixture);
        Assert.True(await repository.SaveAsync([new(scope, "draft body", 0, [original])], fixture.Token));
        Assert.False(await repository.SaveAsync([new(scope, "conflict", 0)], fixture.Token));
        var snapshot = Assert.Single(Directory.GetFiles(
            Path.Combine(fixture.Project.PrimaryDirectory, "draft-attachments"),
            original.Id.ToString("N") + ".pdf", SearchOption.AllDirectories));
        Assert.Equal(pdf, await File.ReadAllBytesAsync(snapshot, fixture.Token));
        var restored = (await Repository(fixture).GetAsync(scope, fixture.Token))!;
        var item = Assert.Single(restored.AttachmentItems);
        Assert.Equal("draft body", restored.Text);
        Assert.Equal(original.Id, item.Id);
        Assert.Equal(AttachmentDocument.PdfMediaType, item.MediaType);
        Assert.True(item.IsAvailable);
        Assert.Contains("draft pdf body", item.Content);
        Assert.Equal(original.Document?.Base64Data, item.Document?.Base64Data);
        Assert.False(File.Exists(original.FullPath));
    }

    [Fact]
    public async Task UnavailablePdfDraftRetainsMetadataAndNeverBecomesEmptyText()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var original = await AttachmentAsync(fixture, AttachmentDocumentTestData.Pdf("kept"), "kept.pdf");
        var repository = Repository(fixture);
        Assert.True(await repository.SaveAsync([new(scope, "preserved", 0, [original])], fixture.Token));
        var snapshot = Assert.Single(Directory.GetFiles(
            Path.Combine(fixture.Project.PrimaryDirectory, "draft-attachments"),
            original.Id.ToString("N") + ".pdf", SearchOption.AllDirectories));
        File.Delete(snapshot);
        var restored = (await Repository(fixture).GetAsync(scope, fixture.Token))!;
        var item = Assert.Single(restored.AttachmentItems);
        Assert.False(item.IsAvailable);
        Assert.Null(item.Content);
        Assert.Null(item.Document);
        Assert.Equal(AttachmentDocument.PdfMediaType, item.MediaType);
        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync([new(scope, "edit", 1, [item])], fixture.Token));
        Assert.True(await repository.SaveAsync([new(scope, "edited", 1)], fixture.Token));
        Assert.Empty((await repository.GetAsync(scope, fixture.Token))!.AttachmentItems);
    }

    [Fact]
    public async Task EncryptedPdfDraftIsRejectedBeforePersisting()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var pdf = AttachmentDocumentTestData.EncryptedPdf("secret");
        var original = new ConversationDraftAttachment(Guid.NewGuid(),
            Path.Combine(fixture.Project.PrimaryDirectory, "locked.pdf"), "locked.pdf", string.Empty,
            pdf.LongLength, WorkspaceAttachmentAuthorization.Create(fixture.Project),
            AttachmentDocument.PdfMediaType,
            Document: AttachmentDocument.FromExtractedText(AttachmentDocument.PdfMediaType, pdf, 0, 0, 0, false));
        var failure = await Assert.ThrowsAsync<AttachmentDocumentValidationException>(() =>
            Repository(fixture).SaveAsync([new(scope, "draft", 0, [original])], fixture.Token));
        Assert.Equal(AttachmentDocumentFailure.Encrypted, failure.Failure);
        Assert.Null(await Repository(fixture).GetAsync(scope, fixture.Token));
    }

    private static SqliteConversationDraftRepository Repository(SqliteFailurePersistenceFixture fixture) =>
        new(fixture.Connections, new AppDataPaths(fixture.Project.PrimaryDirectory),
            documents: new PdfTextExtractor());

    private static async Task<ConversationDraftAttachment> AttachmentAsync(
        SqliteFailurePersistenceFixture fixture, byte[] pdf, string name)
    {
        var extraction = await new PdfTextExtractor().ExtractAsync(pdf, fixture.Token);
        return new(Guid.NewGuid(), Path.Combine(fixture.Project.PrimaryDirectory, name), name,
            extraction.Text, pdf.LongLength, WorkspaceAttachmentAuthorization.Create(fixture.Project),
            AttachmentDocument.PdfMediaType,
            Document: AttachmentDocument.FromExtractedText(AttachmentDocument.PdfMediaType, pdf,
                extraction.PageCount, extraction.TotalPages, extraction.Text.Length, extraction.Truncated));
    }
}
