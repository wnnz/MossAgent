using System.Text;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;

namespace MossAgent.Infrastructure.Attachments;

public sealed class PdfTextExtractor : IAttachmentDocumentTextExtractor
{
    public Task<DocumentTextExtraction> ExtractAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (content.IsEmpty || content.Length > AttachmentDocument.MaximumByteCount)
            throw new AttachmentDocumentValidationException(AttachmentDocumentFailure.SizeLimit);
        var snapshot = content.ToArray();
        return Task.Run(() => Extract(snapshot, cancellationToken), cancellationToken).WaitAsync(cancellationToken);
    }

    private static DocumentTextExtraction Extract(byte[] bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var document = PdfDocument.Open(bytes);
            if (document.IsEncrypted)
                throw new AttachmentDocumentValidationException(AttachmentDocumentFailure.Encrypted);
            var builder = new StringBuilder();
            var processed = 0;
            var truncated = false;
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (processed >= AttachmentDocument.MaximumPageCount
                    || builder.Length >= AttachmentDocument.MaximumTextCharacters)
                { truncated = true; break; }
                var text = page.Text ?? string.Empty;
                var remaining = AttachmentDocument.MaximumTextCharacters - builder.Length;
                if (text.Length > remaining)
                {
                    text = text[..remaining];
                    truncated = true;
                }
                if (builder.Length > 0 && text.Length > 0) builder.Append('\n');
                builder.Append(text);
                processed++;
            }
            return new(builder.ToString(), processed, document.NumberOfPages, truncated || processed < document.NumberOfPages);
        }
        catch (AttachmentDocumentValidationException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (PdfDocumentEncryptedException)
        { throw new AttachmentDocumentValidationException(AttachmentDocumentFailure.Encrypted); }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        { throw new AttachmentDocumentValidationException(AttachmentDocumentFailure.Invalid); }
    }
}
