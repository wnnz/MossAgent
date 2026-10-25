using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class WorkspaceDocumentAttachmentReaderTests
{
    [Fact]
    public async Task PdfBytesAreDetectedByMagicAndReturnExtractedTextSnapshot()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "notes.data");
        await File.WriteAllBytesAsync(path, AttachmentDocumentTestData.Pdf("hello pdf"), fixture.Token);
        var selected = await new WorkspaceAttachmentReader(documents: new PdfTextExtractor())
            .ReadAsync(fixture.Project, [path], fixture.Token);
        var content = Assert.Single(selected);
        Assert.Equal("application/pdf", content.MediaType);
        Assert.NotNull(content.Document);
        Assert.Contains("hello pdf", content.Content);
        Assert.Equal(content.Document!.PageCount, content.Document.TotalPages);
        Assert.False(content.Document.Truncated);
        Assert.Null(content.Image);
    }

    [Fact]
    public async Task FakePdfExtensionAndMagicAreRejected()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var fake = Path.Combine(fixture.Project.PrimaryDirectory, "fake.pdf");
        await File.WriteAllTextAsync(fake, "not actually a pdf", fixture.Token);
        var reader = new WorkspaceAttachmentReader(documents: new PdfTextExtractor());
        var extension = await Assert.ThrowsAsync<AttachmentDocumentValidationException>(
            () => reader.ReadAsync(fixture.Project, [fake], fixture.Token));
        Assert.Equal(AttachmentDocumentFailure.Invalid, extension.Failure);
        var magic = Path.Combine(fixture.Project.PrimaryDirectory, "magic.pdf");
        await File.WriteAllBytesAsync(magic,
            System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 followed by garbage"), fixture.Token);
        var exception = await Assert.ThrowsAsync<AttachmentDocumentValidationException>(
            () => reader.ReadAsync(fixture.Project, [magic], fixture.Token));
        Assert.Equal(AttachmentDocumentFailure.Invalid, exception.Failure);
    }

    [Fact]
    public async Task EncryptedAndOversizedPdfsAreRejected()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var reader = new WorkspaceAttachmentReader(documents: new PdfTextExtractor());
        var encrypted = Path.Combine(fixture.Project.PrimaryDirectory, "locked.pdf");
        await File.WriteAllBytesAsync(encrypted, AttachmentDocumentTestData.EncryptedPdf("secret"), fixture.Token);
        var encryptedFailure = await Assert.ThrowsAsync<AttachmentDocumentValidationException>(
            () => reader.ReadAsync(fixture.Project, [encrypted], fixture.Token));
        Assert.Equal(AttachmentDocumentFailure.Encrypted, encryptedFailure.Failure);
        var oversized = Path.Combine(fixture.Project.PrimaryDirectory, "huge.pdf");
        await File.WriteAllBytesAsync(oversized,
            AttachmentDocumentTestData.PaddedPdf(AttachmentDocument.MaximumByteCount + 64), fixture.Token);
        var oversizedFailure = await Assert.ThrowsAsync<AttachmentDocumentValidationException>(
            () => reader.ReadAsync(fixture.Project, [oversized], fixture.Token));
        Assert.Equal(AttachmentDocumentFailure.SizeLimit, oversizedFailure.Failure);
    }

    [Fact]
    public async Task AggregateDocumentBudgetIsEnforcedSeparatelyFromImages()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var paths = Enumerable.Range(0, 3).Select(index =>
            Path.Combine(fixture.Project.PrimaryDirectory, $"large-{index}.pdf")).ToArray();
        foreach (var path in paths)
            await File.WriteAllBytesAsync(path, AttachmentDocumentTestData.PaddedPdf(9 * 1024 * 1024), fixture.Token);
        var reader = new WorkspaceAttachmentReader(documents: new PdfTextExtractor());
        var exception = await Assert.ThrowsAsync<AttachmentDocumentValidationException>(
            () => reader.ReadAsync(fixture.Project, paths, fixture.Token));
        Assert.Equal(AttachmentDocumentFailure.SizeLimit, exception.Failure);
        var image = Path.Combine(fixture.Project.PrimaryDirectory, "image.png");
        var single = Path.Combine(fixture.Project.PrimaryDirectory, "single.pdf");
        await File.WriteAllBytesAsync(image, AttachmentImageTestData.Static("image/png"), fixture.Token);
        await File.WriteAllBytesAsync(single, AttachmentDocumentTestData.Pdf("mixed"), fixture.Token);
        var mixed = await new WorkspaceAttachmentReader(new SkiaAttachmentImageDecoder(), documents: new PdfTextExtractor())
            .ReadAsync(fixture.Project, [image, single], fixture.Token);
        Assert.Equal(2, mixed.Count);
    }

    [Fact]
    public async Task MissingExtractorAndCancellationReturnNoResults()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var single = Path.Combine(fixture.Project.PrimaryDirectory, "single.pdf");
        await File.WriteAllBytesAsync(single, AttachmentDocumentTestData.Pdf("body"), fixture.Token);
        var exception = await Assert.ThrowsAsync<AttachmentDocumentValidationException>(() =>
            new WorkspaceAttachmentReader().ReadAsync(fixture.Project, [single], fixture.Token));
        Assert.Equal(AttachmentDocumentFailure.ExtractorUnavailable, exception.Failure);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new WorkspaceAttachmentReader(documents: new PdfTextExtractor())
                .ReadAsync(fixture.Project, [single], cancellation.Token));
    }
}
