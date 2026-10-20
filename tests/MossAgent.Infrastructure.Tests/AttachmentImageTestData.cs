using System.Buffers.Binary;
using SkiaSharp;

namespace MossAgent.Infrastructure.Tests;

internal static class AttachmentImageTestData
{
    public const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC";
    public const string Gif = "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";
    public const string AnimatedGif = "R0lGODlhAQABAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQABQAAACwAAAAAAQABAAAIBAABBAQAIfkEAQUAAQAsAAAAAAEAAQCBAAD/AAAAAAAAAAAACAQAAQQEADs=";
    public const string AnimatedPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAACGFjVEwAAAACAAAAAPONk3AAAAAaZmNUTAAAAAAAAAABAAAAAQAAAAAAAAAAAAEAFAAATMe+qgAAAAxJREFUeJxj+M/AAAADAQEAyf6S7wAAABpmY1RMAAAAAQAAAAEAAAABAAAAAAAAAAAAAQAUAADXtFR+AAAAEGZkQVQAAAACeJxjYGD4DwABAwEAaL/t9AAAAABJRU5ErkJggg==";
    public const string AnimatedWebp = "UklGRsQAAABXRUJQVlA4WAoAAAACAAAAAAAAAAAAQU5JTQYAAAAAAAAAAABBTk1GSgAAAAAAAAAAAAAAAAAAADIAAAJWUDggMgAAADABAJ0BKgEAAQABQCYloAADcAD+8ut///mwP/bz/wR6Af//0uD//pcH//S4P/SkAAAAQU5NRkYAAAAAAAAAAAAAAAAAAAAyAAAAVlA4IC4AAAA0AQCdASoBAAEAAAAmJaAAA3AA/vtV4///S4P/+lwf/9Lg/9Lg//rV5Vesq6AA";

    public static byte[] Static(string mediaType)
    {
        if (mediaType == "image/png") return Convert.FromBase64String(Png);
        if (mediaType == "image/gif") return Convert.FromBase64String(Gif);
        using var bitmap = new SKBitmap(1, 1);
        bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(mediaType == "image/jpeg" ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Webp, 100);
        return data.ToArray();
    }

    public static byte[] Dimensions(int width, int height)
    {
        var bytes = Convert.FromBase64String(Png);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        var checksum = uint.MaxValue;
        foreach (var value in bytes.AsSpan(12, 17))
        {
            checksum ^= value;
            for (var bit = 0; bit < 8; bit++)
                checksum = (checksum >> 1) ^ ((checksum & 1) == 0 ? 0 : 0xEDB88320u);
        }
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(29, 4), ~checksum);
        return bytes;
    }
}
