using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PdfTextExtractorTests
{
    [Fact]
    public async Task ValidPdfExtractsBoundedTextWithPageCounts()
    {
        var extraction = await new PdfTextExtractor().ExtractAsync(
            AttachmentDocumentTestData.Pdf("Hello", "World page two"), TestContext.Current.CancellationToken);
        Assert.Contains("Hello", extraction.Text);
        Assert.Contains("World page two", extraction.Text);
        Assert.Equal(2, extraction.PageCount);
        Assert.Equal(2, extraction.TotalPages);
        Assert.False(extraction.Truncated);
    }

    [Fact]
    public async Task PageCapTruncatesAndReportsTotalPages()
    {
        var texts = Enumerable.Range(0, AttachmentDocument.MaximumPageCount + 1)
            .Select(index => $"page {index}").ToArray();
        var extraction = await new PdfTextExtractor().ExtractAsync(
            AttachmentDocumentTestData.Pdf(texts), TestContext.Current.CancellationToken);
        Assert.Equal(AttachmentDocument.MaximumPageCount, extraction.PageCount);
        Assert.Equal(AttachmentDocument.MaximumPageCount + 1, extraction.TotalPages);
        Assert.True(extraction.Truncated);
        Assert.DoesNotContain($"page {AttachmentDocument.MaximumPageCount}", extraction.Text);
    }

    [Fact]
    public async Task CharacterCapTruncatesLongPages()
    {
        var extraction = await new PdfTextExtractor().ExtractAsync(
            AttachmentDocumentTestData.Pdf(new string('x', AttachmentDocument.MaximumTextCharacters + 1000)),
            TestContext.Current.CancellationToken);
        Assert.True(extraction.Truncated);
        Assert.True(extraction.Text.Length <= AttachmentDocument.MaximumTextCharacters);
        Assert.Equal(1, extraction.TotalPages);
    }

    [Fact]
    public async Task CorruptAndFakeMagicBytesAreRejected()
    {
        var extractor = new PdfTextExtractor();
        var corrupt = await Assert.ThrowsAsync<AttachmentDocumentValidationException>(() =>
            extractor.ExtractAsync(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 not a real document"),
                TestContext.Current.CancellationToken));
        Assert.Equal(AttachmentDocumentFailure.Invalid, corrupt.Failure);
        var garbage = await Assert.ThrowsAsync<AttachmentDocumentValidationException>(() =>
            extractor.ExtractAsync(new byte[128], TestContext.Current.CancellationToken));
        Assert.Equal(AttachmentDocumentFailure.Invalid, garbage.Failure);
    }

    [Fact]
    public async Task EncryptedPdfIsRejectedWithEncryptedFailure()
    {
        var exception = await Assert.ThrowsAsync<AttachmentDocumentValidationException>(() =>
            new PdfTextExtractor().ExtractAsync(AttachmentDocumentTestData.EncryptedPdf("secret"),
                TestContext.Current.CancellationToken));
        Assert.Equal(AttachmentDocumentFailure.Encrypted, exception.Failure);
    }

    [Fact]
    public async Task OversizeAndEmptyInputAreRejectedBeforeParsing()
    {
        var extractor = new PdfTextExtractor();
        var oversized = await Assert.ThrowsAsync<AttachmentDocumentValidationException>(() =>
            extractor.ExtractAsync(new byte[AttachmentDocument.MaximumByteCount + 1],
                TestContext.Current.CancellationToken));
        Assert.Equal(AttachmentDocumentFailure.SizeLimit, oversized.Failure);
        await Assert.ThrowsAsync<AttachmentDocumentValidationException>(() =>
            extractor.ExtractAsync(ReadOnlyMemory<byte>.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancellationStopsExtraction()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new PdfTextExtractor().ExtractAsync(AttachmentDocumentTestData.Pdf("hello"), cancellation.Token));
    }
}
