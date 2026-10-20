using MossAgent.Application.Attachments;
using MossAgent.Domain;
using SkiaSharp;

namespace MossAgent.Infrastructure.Attachments;

public sealed class SkiaAttachmentThumbnailRenderer : IAttachmentThumbnailRenderer
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public Task<AttachmentImage> RenderAsync(AttachmentImage image, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();
        return RenderGuardedAsync(image, cancellationToken).WaitAsync(cancellationToken);
    }

    private async Task<AttachmentImage> RenderGuardedAsync(AttachmentImage image, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Render(image, token), token).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private static AttachmentImage Render(AttachmentImage image, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var source = SkiaAttachmentImageSource.Open(Convert.FromBase64String(image.Base64Data), token);
        var bitmap = source.Bitmap;
        if (source.MediaType != image.MediaType || bitmap.Width != image.Width || bitmap.Height != image.Height)
            throw new AttachmentImageValidationException(AttachmentImageFailure.Invalid);
        var scale = Math.Min(1d, (double)IAttachmentThumbnailRenderer.MaximumDimension / Math.Max(bitmap.Width, bitmap.Height));
        var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
        using var resized = bitmap.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul),
            new SKSamplingOptions(SKFilterMode.Linear));
        if (resized is null) throw new AttachmentImageValidationException(AttachmentImageFailure.Invalid);
        token.ThrowIfCancellationRequested();
        using var thumbnail = SkiaAttachmentImageOrientation.CreateImage(resized, source.Origin, token);
        using var encoded = thumbnail.Encode(SKEncodedImageFormat.Png, 100);
        if (encoded is null || encoded.Size > IAttachmentThumbnailRenderer.MaximumByteCount)
            throw new AttachmentImageValidationException(AttachmentImageFailure.SizeLimit);
        token.ThrowIfCancellationRequested();
        return AttachmentImage.FromDecodedBytes("image/png", encoded.ToArray(), thumbnail.Width, thumbnail.Height);
    }
}
