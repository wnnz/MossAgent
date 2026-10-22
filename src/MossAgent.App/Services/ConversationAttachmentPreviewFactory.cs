using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.App.Services;

public sealed class ConversationAttachmentPreviewFactory(
    IConversationAttachmentStore attachments, IAttachmentThumbnailRenderer renderer)
{
    private readonly SemaphoreSlim _reads = new(1, 1);

    public AttachmentPreviewSource? Create(ConversationMessage message, ConversationAttachment attachment)
    {
        if (message.Role != MessageRole.User || attachment.MediaType == "text/plain") return null;
        var size = attachment.ByteCount < 1024 ? $"{attachment.ByteCount} B"
            : $"{attachment.ByteCount / 1024d:0.#} KiB";
        return new(attachment.DisplayPath, size,
            token => LoadAsync(message.TaskId, message.Id, attachment, token), renderer);
    }

    private async Task<AttachmentImage> LoadAsync(Guid taskId, Guid messageId,
        ConversationAttachment attachment, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ConversationAttachmentValidation.Validate(taskId, messageId, [attachment]);
        await _reads.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var contents = await attachments.ReadAsync(taskId, messageId, [attachment], token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            var image = contents.Count == 1 ? contents[0].Image : null;
            if (image is null || image.MediaType != attachment.MediaType || image.ByteCount != attachment.ByteCount)
                throw new InvalidDataException("Attachment snapshot did not contain the expected image.");
            return image;
        }
        finally { _reads.Release(); }
    }
}
