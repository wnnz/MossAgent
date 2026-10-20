using System.Text;
using MossAgent.Domain;

namespace MossAgent.Application.Attachments;

public static class ConversationDraftAttachmentValidation
{
    public static void Validate(ConversationDraftScope scope, IReadOnlyList<ConversationDraftAttachment> attachments, bool requireContent = true)
    {
        scope.Validate();
        if (attachments.Count > WorkspaceAttachmentLimits.MaximumFiles
            || attachments.Select(attachment => attachment.Id).Distinct().Count() != attachments.Count)
            throw new ArgumentException("Invalid draft attachment scope or count.");
        foreach (var attachment in attachments)
        {
            if (attachment.Id == Guid.Empty || string.IsNullOrWhiteSpace(attachment.FullPath)
                || attachment.FullPath.Length > 32768 || attachment.FullPath.Contains('\0') || !Path.IsPathFullyQualified(attachment.FullPath)
                || string.IsNullOrWhiteSpace(attachment.DisplayPath) || attachment.DisplayPath.Length > 1024
                || attachment.DisplayPath.Contains('\0') || attachment.ByteCount < 0
                || (attachment.MediaType == "text/plain" ? attachment.ByteCount > WorkspaceAttachmentLimits.MaximumBytesPerFile
                    : !AttachmentImage.IsSupportedMediaType(attachment.MediaType) || attachment.ByteCount is < 1 or > AttachmentImage.MaximumByteCount)
                || string.IsNullOrEmpty(attachment.AuthorizationStamp) || attachment.AuthorizationStamp.Length != 64
                || !attachment.AuthorizationStamp.All(char.IsAsciiHexDigit))
                throw new ArgumentException("Invalid draft attachment metadata.");
            if (scope.ProjectId is null && attachment.AuthorizationStamp != WorkspaceAttachmentAuthorization.CreateSelection(attachment.FullPath))
                throw new UnauthorizedAccessException("Projectless attachments require an explicit file-selection stamp.");
            if (!attachment.IsAvailable)
            {
                if (requireContent) throw new InvalidDataException("Draft attachment content is unavailable.");
                continue;
            }
            if (attachment.MediaType != "text/plain")
            {
                if (!string.IsNullOrEmpty(attachment.Content) || attachment.Image!.MediaType != attachment.MediaType
                    || attachment.Image.ByteCount != attachment.ByteCount)
                    throw new InvalidDataException("Invalid draft image content.");
                continue;
            }
            if (attachment.Image is not null) throw new InvalidDataException("Text attachment cannot contain image bytes.");
            if (attachment.Content!.Length > WorkspaceAttachmentLimits.MaximumBytesPerFile || attachment.Content.Contains('\0')
                || new UTF8Encoding(false, true).GetByteCount(attachment.Content) != attachment.ByteCount)
                throw new InvalidDataException("Draft attachment must be bounded UTF-8 text.");
        }
        if (attachments.Where(item => item.MediaType != "text/plain").Sum(item => item.ByteCount) > AttachmentImage.MaximumTotalByteCount)
            throw new InvalidDataException("Draft images exceed their aggregate byte limit.");
    }
}
