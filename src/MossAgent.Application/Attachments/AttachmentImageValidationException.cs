namespace MossAgent.Application.Attachments;

public sealed class AttachmentImageValidationException(AttachmentImageFailure failure)
    : IOException("Image attachment validation failed.")
{
    public AttachmentImageFailure Failure { get; } = failure;
}
