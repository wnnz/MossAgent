using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ConversationDocumentAttachmentStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.AttachmentTests", Guid.NewGuid().ToString("N"));
    private readonly Guid _taskId = Guid.NewGuid();
    private readonly Guid _messageId = Guid.NewGuid();
    private readonly ConversationAttachmentStore _store;
    private readonly TaskArtifactPaths _paths;
    private CancellationToken Token => TestContext.Current.CancellationToken;

    public ConversationDocumentAttachmentStoreTests()
    {
        Directory.CreateDirectory(_root);
        _paths = new TaskArtifactPaths(new AppDataPaths(_root));
        _store = new ConversationAttachmentStore(_paths, documents: new PdfTextExtractor());
    }

    [Fact]
    public async Task PdfSnapshotStoresOriginalBytesAndReextractsTextOnRead()
    {
        var pdf = AttachmentDocumentTestData.Pdf("stored page text", "second page");
        var content = await DocumentContentAsync(pdf, "report.pdf");
        var snapshot = Assert.Single(await _store.SaveAsync(_taskId, _messageId, [content], Token));
        Assert.Equal("application/pdf", snapshot.MediaType);
        Assert.Equal(pdf.LongLength, snapshot.ByteCount);
        var path = Path.Combine(_paths.GetTaskDirectory(_taskId), "attachments",
            _messageId.ToString("N"), snapshot.Id.ToString("N") + ".pdf");
        Assert.True(File.Exists(path));
        Assert.Equal(pdf, await File.ReadAllBytesAsync(path, Token));
        var restored = Assert.Single(await _store.ReadAsync(_taskId, _messageId, [snapshot], Token));
        Assert.Equal("application/pdf", restored.MediaType);
        Assert.Contains("stored page text", restored.Content);
        Assert.Equal(2, restored.Document?.TotalPages);
        Assert.Null(restored.Image);
        Assert.False(restored.Document!.Truncated);
    }

    [Fact]
    public async Task CorruptAndEncryptedPdfSnapshotsAreRejectedWithoutPublishing()
    {
        var corrupt = await DocumentContentAsync(
            System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 broken"), "broken.pdf", corrupted: true);
        await Assert.ThrowsAsync<AttachmentDocumentValidationException>(
            () => _store.SaveAsync(_taskId, _messageId, [corrupt], Token));
        var encrypted = await DocumentContentAsync(AttachmentDocumentTestData.EncryptedPdf("hidden"), "locked.pdf", corrupted: true);
        var failure = await Assert.ThrowsAsync<AttachmentDocumentValidationException>(
            () => _store.SaveAsync(_taskId, _messageId, [encrypted], Token));
        Assert.Equal(AttachmentDocumentFailure.Encrypted, failure.Failure);
        Assert.Empty(Directory.GetFiles(_root, "*.pdf", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task TamperedSnapshotBytesFailHashValidationBeforeExtraction()
    {
        var pdf = AttachmentDocumentTestData.Pdf("original");
        var snapshot = Assert.Single(await _store.SaveAsync(_taskId, _messageId,
            [await DocumentContentAsync(pdf, "kept.pdf")], Token));
        var path = Path.Combine(_paths.GetTaskDirectory(_taskId), "attachments",
            _messageId.ToString("N"), snapshot.Id.ToString("N") + ".pdf");
        await File.WriteAllBytesAsync(path, AttachmentDocumentTestData.Pdf("replaced"), Token);
        await Assert.ThrowsAsync<InvalidDataException>(() => _store.ReadAsync(_taskId, _messageId, [snapshot], Token));
    }

    private async Task<WorkspaceAttachmentContent> DocumentContentAsync(byte[] pdf, string name, bool corrupted = false)
    {
        if (corrupted)
            return new(Path.Combine(_root, name), name, string.Empty, pdf.LongLength,
                Document: AttachmentDocument.FromExtractedText(AttachmentDocument.PdfMediaType, pdf, 0, 0, 0, false));
        var extraction = await new PdfTextExtractor().ExtractAsync(pdf, Token);
        return new(Path.Combine(_root, name), name, extraction.Text, pdf.LongLength,
            Document: AttachmentDocument.FromExtractedText(AttachmentDocument.PdfMediaType, pdf,
                extraction.PageCount, extraction.TotalPages, extraction.Text.Length, extraction.Truncated));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
