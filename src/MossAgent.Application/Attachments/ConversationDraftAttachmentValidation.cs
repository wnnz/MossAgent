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
                || attachment.ByteCount > Limit(attachment.MediaType)
                || attachment.MediaType != "text/plain"
                    && attachment.ByteCount < 1
                || attachment.MediaType is not "text/plain"
                    && !AttachmentImage.IsSupportedMediaType(attachment.MediaType)
                    && !AttachmentDocument.IsSupportedMediaType(attachment.MediaType)
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
                var payloadMatches = attachment.Image is { } image
                    ? image.MediaType == attachment.MediaType
                        && image.ByteCount == attachment.ByteCount
                        && attachment.Document is null
                    : attachment.Document is { } document
                        && document.MediaType == attachment.MediaType
                        && document.ByteCount == attachment.ByteCount
                        && attachment.Image is null;
                if (!string.IsNullOrEmpty(attachment.Content) || !payloadMatches)
                    throw new InvalidDataException("Invalid binary draft attachment content.");
                continue;
            }
            if (attachment.Image is not null || attachment.Document is not null)
                throw new InvalidDataException("Text attachment cannot contain binary bytes.");
            if (attachment.Content!.Length > WorkspaceAttachmentLimits.MaximumBytesPerFile || attachment.Content.Contains('\0')
                || new UTF8Encoding(false, true).GetByteCount(attachment.Content) != attachment.ByteCount)
                throw new InvalidDataException("Draft attachment must be bounded UTF-8 text.");
        }
        if (attachments.Where(item => AttachmentImage.IsSupportedMediaType(item.MediaType))
            .Sum(item => item.ByteCount) > AttachmentImage.MaximumTotalByteCount)
            throw new InvalidDataException("Draft images exceed their aggregate byte limit.");
        var documents = attachments.Where(item =>
            AttachmentDocument.IsSupportedMediaType(item.MediaType)).ToArray();
        if (documents.Length > AttachmentDocument.MaximumCount
            || documents.Sum(item => item.ByteCount) > AttachmentDocument.MaximumTotalByteCount)
            throw new InvalidDataException("Draft PDFs exceed their aggregate limit.");
    }

    private static long Limit(string mediaType) => mediaType == "text/plain"
        ? WorkspaceAttachmentLimits.MaximumBytesPerFile
        : AttachmentImage.IsSupportedMediaType(mediaType)
            ? AttachmentImage.MaximumByteCount
            : AttachmentDocument.IsSupportedMediaType(mediaType)
                ? AttachmentDocument.MaximumByteCount : 0;
}
