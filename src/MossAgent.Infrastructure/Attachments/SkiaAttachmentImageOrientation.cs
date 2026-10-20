using MossAgent.Application.Attachments;
using SkiaSharp;

namespace MossAgent.Infrastructure.Attachments;

internal static class SkiaAttachmentImageOrientation
{
    internal static SKImage CreateImage(SKBitmap bitmap, SKEncodedOrigin origin, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (origin == SKEncodedOrigin.TopLeft) return SKImage.FromBitmap(bitmap);
        var transposed = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = transposed ? bitmap.Height : bitmap.Width;
        var height = transposed ? bitmap.Width : bitmap.Height;
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new AttachmentImageValidationException(AttachmentImageFailure.Invalid);
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.SetMatrix(CreateMatrix(origin, bitmap.Width, bitmap.Height));
        surface.Canvas.DrawBitmap(bitmap, 0, 0);
        surface.Canvas.Flush();
        token.ThrowIfCancellationRequested();
        return surface.Snapshot();
    }

    private static SKMatrix CreateMatrix(SKEncodedOrigin origin, int width, int height) => origin switch
    {
        SKEncodedOrigin.TopRight => new(-1, 0, width, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new(-1, 0, width, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new(1, 0, 0, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new(0, -1, height, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new(0, -1, height, -1, 0, width, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new(0, 1, 0, -1, 0, width, 0, 0, 1),
        _ => throw new AttachmentImageValidationException(AttachmentImageFailure.Invalid)
    };
}
