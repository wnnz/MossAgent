namespace MossAgent.Application.Attachments;

public enum AttachmentDocumentFailure
{
    Invalid,
    Encrypted,
    Unsupported,
    SizeLimit,
    ExtractorUnavailable
}
