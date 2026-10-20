using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Attachments;

public sealed class SkiaAttachmentImageDecoder : IAttachmentImageDecoder
{
    public Task<AttachmentImage> DecodeAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (content.IsEmpty || content.Length > AttachmentImage.MaximumByteCount)
            throw new AttachmentImageValidationException(AttachmentImageFailure.SizeLimit);
        var snapshot = content.ToArray();
        return Task.Run(() => Decode(snapshot, cancellationToken), cancellationToken).WaitAsync(cancellationToken);
    }

    private static AttachmentImage Decode(byte[] bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var source = SkiaAttachmentImageSource.Open(bytes, cancellationToken);
            return AttachmentImage.FromDecodedBytes(source.MediaType, bytes, source.Bitmap.Width, source.Bitmap.Height);
        }
        catch (AttachmentImageValidationException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new AttachmentImageValidationException(AttachmentImageFailure.Invalid);
        }
    }

}
