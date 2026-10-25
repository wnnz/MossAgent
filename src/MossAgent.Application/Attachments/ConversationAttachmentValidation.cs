using MossAgent.Domain;

namespace MossAgent.Application.Attachments;

public static class ConversationAttachmentValidation
{
    public static void ValidateModel(ModelProfile model, IReadOnlyList<WorkspaceAttachmentContent> attachments)
    {
        if (attachments.Any(item => item.Image is not null) && !model.SupportsImages)
            throw new ArgumentException("The selected model does not support image inputs.", nameof(model));
    }

    public static void Validate(Guid taskId, Guid messageId, IReadOnlyList<ConversationAttachment> attachments)
    {
        if (taskId == Guid.Empty || messageId == Guid.Empty || attachments.Count > WorkspaceAttachmentLimits.MaximumFiles
            || attachments.Select(attachment => attachment.Id).Distinct().Count() != attachments.Count)
            throw new InvalidDataException("Invalid conversation attachment identity or count.");
        foreach (var attachment in attachments)
        {
            if (attachment.Id == Guid.Empty || attachment.TaskId != taskId || attachment.MessageId != messageId)
                throw new UnauthorizedAccessException("Conversation attachment belongs to another message.");
            if (string.IsNullOrWhiteSpace(attachment.DisplayPath) || attachment.DisplayPath.Length > 1024
                || attachment.DisplayPath.Contains('\0') || !IsBoundedByteCount(attachment.MediaType, attachment.ByteCount)
                || string.IsNullOrEmpty(attachment.Sha256) || attachment.Sha256.Length != 64 || !attachment.Sha256.All(char.IsAsciiHexDigit))
                throw new InvalidDataException("Invalid conversation attachment metadata.");
        }
        if (attachments.Where(item => AttachmentImage.IsSupportedMediaType(item.MediaType)).Sum(item => item.ByteCount) > AttachmentImage.MaximumTotalByteCount)
            throw new InvalidDataException("Image attachments exceed their aggregate byte limit.");
        if (attachments.Where(item => AttachmentDocument.IsSupportedMediaType(item.MediaType)).Sum(item => item.ByteCount) > AttachmentDocument.MaximumTotalByteCount)
            throw new InvalidDataException("Document attachments exceed their aggregate byte limit.");
    }

    internal static bool IsBoundedByteCount(string mediaType, long byteCount) => mediaType switch
    {
        "text/plain" => byteCount is >= 0 and <= WorkspaceAttachmentLimits.MaximumBytesPerFile,
        AttachmentDocument.PdfMediaType => byteCount is >= 1 and <= AttachmentDocument.MaximumByteCount,
        _ => AttachmentImage.IsSupportedMediaType(mediaType) && byteCount is >= 1 and <= AttachmentImage.MaximumByteCount
    };
}
