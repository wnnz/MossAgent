using System.Buffers.Binary;
using SkiaSharp;

namespace MossAgent.Infrastructure.Tests;

internal static class AttachmentOrientationTestData
{
    internal static SKColor[] Colors => [SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Yellow, SKColors.Magenta, SKColors.Cyan];

    internal static byte[] Jpeg(SKEncodedOrigin origin, bool large, bool littleEndian)
    {
        var cell = large ? 100 : 20;
        using var bitmap = new SKBitmap(cell * 3, cell * 2);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint();
        var colors = Colors;
        for (var index = 0; index < colors.Length; index++)
        {
            paint.Color = colors[index];
            canvas.DrawRect(SKRect.Create(index % 3 * cell, index / 3 * cell, cell, cell), paint);
        }
        canvas.Flush();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        var jpeg = data.ToArray();
        var segment = Exif(origin, littleEndian);
        var result = new byte[jpeg.Length + segment.Length];
        jpeg.AsSpan(0, 2).CopyTo(result);
        segment.CopyTo(result, 2);
        jpeg.AsSpan(2).CopyTo(result.AsSpan(2 + segment.Length));
        return result;
    }

    internal static int[] ExpectedCells(SKEncodedOrigin origin) => origin switch
    {
        SKEncodedOrigin.TopLeft => [0, 1, 2, 3, 4, 5],
        SKEncodedOrigin.TopRight => [2, 1, 0, 5, 4, 3],
        SKEncodedOrigin.BottomRight => [5, 4, 3, 2, 1, 0],
        SKEncodedOrigin.BottomLeft => [3, 4, 5, 0, 1, 2],
        SKEncodedOrigin.LeftTop => [0, 3, 1, 4, 2, 5],
        SKEncodedOrigin.RightTop => [3, 0, 4, 1, 5, 2],
        SKEncodedOrigin.RightBottom => [5, 2, 4, 1, 3, 0],
        SKEncodedOrigin.LeftBottom => [2, 5, 1, 4, 0, 3],
        _ => throw new ArgumentOutOfRangeException(nameof(origin))
    };

    private static byte[] Exif(SKEncodedOrigin origin, bool littleEndian)
    {
        var segment = new byte[36];
        segment[0] = 0xff;
        segment[1] = 0xe1;
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2), 34);
        "Exif\0\0"u8.CopyTo(segment.AsSpan(4));
        segment[10] = segment[11] = littleEndian ? (byte)'I' : (byte)'M';
        Write16(segment.AsSpan(12), 42, littleEndian);
        Write32(segment.AsSpan(14), 8, littleEndian);
        Write16(segment.AsSpan(18), 1, littleEndian);
        Write16(segment.AsSpan(20), 0x112, littleEndian);
        Write16(segment.AsSpan(22), 3, littleEndian);
        Write32(segment.AsSpan(24), 1, littleEndian);
        Write16(segment.AsSpan(28), (ushort)origin, littleEndian);
        return segment;
    }

    private static void Write16(Span<byte> target, ushort value, bool littleEndian)
    {
        if (littleEndian) BinaryPrimitives.WriteUInt16LittleEndian(target, value);
        else BinaryPrimitives.WriteUInt16BigEndian(target, value);
    }

    private static void Write32(Span<byte> target, uint value, bool littleEndian)
    {
        if (littleEndian) BinaryPrimitives.WriteUInt32LittleEndian(target, value);
        else BinaryPrimitives.WriteUInt32BigEndian(target, value);
    }
}
