using MossAgent.Domain;

namespace MossAgent.Application.Attachments;

public static class ConversationAttachmentValidation
{
    public static void ValidateModel(ModelProfile model, IReadOnlyList<WorkspaceAttachmentContent> attachments)
    {
        if (attachments.Any(item => item.Image is not null) && !model.SupportsImages)
            throw new ArgumentException("The selected model does not support image inputs.", nameof(model));
        if (attachments.Any(item => item.Document is not null) && !model.SupportsFiles)
            throw new ArgumentException("The selected model does not support file inputs.", nameof(model));
    }

    public static void Validate(Guid taskId, Guid messageId, IReadOnlyList<ConversationAttachment> attachments)    {
        if (taskId == Guid.Empty || messageId == Guid.Empty || attachments.Count > WorkspaceAttachmentLimits.MaximumFiles
            || attachments.Select(attachment => attachment.Id).Distinct().Count() != attachments.Count)
            throw new InvalidDataException("Invalid conversation attachment identity or count.");
        foreach (var attachment in attachments)
        {
            if (attachment.Id == Guid.Empty || attachment.TaskId != taskId || attachment.MessageId != messageId)
                throw new UnauthorizedAccessException("Conversation attachment belongs to another message.");
            if (string.IsNullOrWhiteSpace(attachment.DisplayPath) || attachment.DisplayPath.Length > 1024
                || attachment.DisplayPath.Contains('\0') || attachment.ByteCount < 0
                || attachment.ByteCount > Limit(attachment.MediaType)
                || attachment.MediaType != "text/plain"
                    && attachment.ByteCount < 1
                || attachment.MediaType is not "text/plain"
                    && !AttachmentImage.IsSupportedMediaType(attachment.MediaType)
                    && !AttachmentDocument.IsSupportedMediaType(attachment.MediaType)
                || string.IsNullOrEmpty(attachment.Sha256) || attachment.Sha256.Length != 64 || !attachment.Sha256.All(char.IsAsciiHexDigit))
                throw new InvalidDataException("Invalid conversation attachment metadata.");
        }
        if (attachments.Where(item => AttachmentImage.IsSupportedMediaType(item.MediaType))
            .Sum(item => item.ByteCount) > AttachmentImage.MaximumTotalByteCount)
            throw new InvalidDataException("Image attachments exceed their aggregate byte limit.");
        var documents = attachments.Where(item => AttachmentDocument.IsSupportedMediaType(item.MediaType)).ToArray();
        if (documents.Length > AttachmentDocument.MaximumCount
            || documents.Sum(item => item.ByteCount) > AttachmentDocument.MaximumTotalByteCount)
            throw new InvalidDataException("PDF attachments exceed their aggregate limit.");
    }

    /// <summary>
    /// Checks whether the selected model accepts the supplied attachment contents without throwing,
    /// so composer state can show the limitation before any snapshot is written.
    /// </summary>
    /// <param name="model">Model that will receive the attachments.</param>
    /// <param name="attachments">Currently selected attachment contents.</param>
    public static bool Supports(ModelProfile model, IEnumerable<WorkspaceAttachmentContent> attachments) =>
        (!attachments.Any(item => item.Image is not null) || model.SupportsImages)
        && (!attachments.Any(item => item.Document is not null) || model.SupportsFiles);

    private static long Limit(string mediaType) => mediaType == "text/plain"
        ? WorkspaceAttachmentLimits.MaximumBytesPerFile
        : AttachmentImage.IsSupportedMediaType(mediaType)
            ? AttachmentImage.MaximumByteCount
            : AttachmentDocument.IsSupportedMediaType(mediaType)
                ? AttachmentDocument.MaximumByteCount : 0;
}
