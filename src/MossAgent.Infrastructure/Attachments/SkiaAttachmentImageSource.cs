using System.Buffers.Binary;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using SkiaSharp;

namespace MossAgent.Infrastructure.Attachments;

internal sealed class SkiaAttachmentImageSource(string mediaType, SKBitmap bitmap, SKEncodedOrigin origin) : IDisposable
{
    internal string MediaType { get; } = mediaType;
    internal SKBitmap Bitmap { get; } = bitmap;
    internal SKEncodedOrigin Origin { get; } = origin;

    internal static SkiaAttachmentImageSource Open(byte[] bytes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (bytes.Length is 0 or > AttachmentImage.MaximumByteCount)
            throw new AttachmentImageValidationException(AttachmentImageFailure.SizeLimit);
        var expected = AttachmentImage.DetectMediaType(bytes)
            ?? throw new AttachmentImageValidationException(AttachmentImageFailure.Unsupported);
        RejectAnimatedPng(bytes, expected);
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data)
            ?? throw new AttachmentImageValidationException(AttachmentImageFailure.Invalid);
        var mediaType = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Gif => "image/gif",
            SKEncodedImageFormat.Webp => "image/webp",
            _ => throw new AttachmentImageValidationException(AttachmentImageFailure.Unsupported)
        };
        if (expected != mediaType) throw new AttachmentImageValidationException(AttachmentImageFailure.Invalid);
        var info = codec.Info;
        if (info.Width is < 1 or > AttachmentImage.MaximumDimension || info.Height is < 1 or > AttachmentImage.MaximumDimension
            || (long)info.Width * info.Height > AttachmentImage.MaximumPixels)
            throw new AttachmentImageValidationException(AttachmentImageFailure.SizeLimit);
        if (codec.FrameCount > 1) throw new AttachmentImageValidationException(AttachmentImageFailure.Animated);
        token.ThrowIfCancellationRequested();
        var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        try
        {
            if (bitmap.GetPixels() == IntPtr.Zero || codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
                throw new AttachmentImageValidationException(AttachmentImageFailure.Invalid);
            token.ThrowIfCancellationRequested();
            return new(mediaType, bitmap, codec.EncodedOrigin);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static void RejectAnimatedPng(ReadOnlySpan<byte> bytes, string mediaType)
    {
        if (mediaType != "image/png") return;
        var offset = 8;
        while (offset <= bytes.Length - 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            if (length > bytes.Length - offset - 12)
                throw new AttachmentImageValidationException(AttachmentImageFailure.Invalid);
            if (bytes.Slice(offset + 4, 4).SequenceEqual("acTL"u8))
                throw new AttachmentImageValidationException(AttachmentImageFailure.Animated);
            offset += checked((int)length + 12);
        }
        if (offset != bytes.Length) throw new AttachmentImageValidationException(AttachmentImageFailure.Invalid);
    }

    public void Dispose() => Bitmap.Dispose();
}
