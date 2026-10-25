namespace MossAgent.Application.Attachments;

public interface IAttachmentDocumentTextExtractor
{
    Task<DocumentTextExtraction> ExtractAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
}
