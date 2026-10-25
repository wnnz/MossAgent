namespace MossAgent.Application.Attachments;

public sealed class AttachmentDocumentValidationException(AttachmentDocumentFailure failure)
    : IOException("Document attachment validation failed.")
{
    public AttachmentDocumentFailure Failure { get; } = failure;
}
