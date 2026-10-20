using MossAgent.Domain;

namespace MossAgent.Application.Attachments;

public interface IAttachmentImageDecoder
{
    Task<AttachmentImage> DecodeAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
}
