using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.App.Services;

public sealed class ConversationAttachmentThumbnailFactory(
    IConversationAttachmentStore attachments, IAttachmentThumbnailRenderer renderer)
{
    private readonly SemaphoreSlim _reads = new(1, 1);

    public AttachmentThumbnailSource? Create(ConversationMessage message, ConversationAttachment attachment)
    {
        if (message.Role != MessageRole.User || !AttachmentImage.IsSupportedMediaType(attachment.MediaType)) return null;
        return new(token => LoadAsync(message.TaskId, message.Id, attachment, token));
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
            return await renderer.RenderAsync(image, token).ConfigureAwait(false);
        }
        finally { _reads.Release(); }
    }
}
