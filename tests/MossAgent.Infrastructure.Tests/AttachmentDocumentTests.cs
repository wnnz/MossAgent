using System.Text;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AttachmentDocumentTests
{
    [Fact]
    public void ValidPdfSignatureAndEndMarkerAreAcceptedWithoutExecutingContent()
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF\n");

        var document = AttachmentDocument.FromDecodedBytes(AttachmentDocument.PdfMediaType, bytes);

        Assert.Equal(AttachmentDocument.PdfMediaType, document.MediaType);
        Assert.Equal(bytes.Length, document.ByteCount);
        Assert.Equal(Convert.ToBase64String(bytes), document.Base64Data);
        Assert.Equal(AttachmentDocument.PdfMediaType, AttachmentDocument.DetectMediaType(bytes));
    }

    [Theory]
    [InlineData("not-a-pdf")]
    [InlineData("%PDF-no-end-marker")]
    [InlineData("%PDG-1.7\n%%EOF")]
    public void InvalidPdfStructureIsRejected(string value)
    {
        Assert.Throws<InvalidDataException>(() =>
            AttachmentDocument.FromDecodedBytes(AttachmentDocument.PdfMediaType, Encoding.ASCII.GetBytes(value)));
    }

    [Fact]
    public void EndMarkerMustBeInTheFinalTwoKilobytes()
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF\n" + new string('x', 4_096));

        Assert.Throws<InvalidDataException>(() =>
            AttachmentDocument.FromDecodedBytes(AttachmentDocument.PdfMediaType, bytes));
    }

    [Fact]
    public void OversizedPdfIsRejectedBeforeCallersAllocateProviderPayloads()
    {
        var bytes = new byte[AttachmentDocument.MaximumByteCount + 1];
        "%PDF-"u8.CopyTo(bytes);
        "%%EOF"u8.CopyTo(bytes.AsSpan(bytes.Length - 5));

        Assert.Throws<InvalidDataException>(() =>
            AttachmentDocument.FromDecodedBytes(AttachmentDocument.PdfMediaType, bytes));
    }
}
