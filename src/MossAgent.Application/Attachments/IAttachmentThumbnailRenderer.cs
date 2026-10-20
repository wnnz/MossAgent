using MossAgent.Domain;

namespace MossAgent.Application.Attachments;

public interface IAttachmentThumbnailRenderer
{
    const int MaximumDimension = 96;
    const int MaximumByteCount = 64 * 1024;

    Task<AttachmentImage> RenderAsync(AttachmentImage image, CancellationToken cancellationToken);
}
