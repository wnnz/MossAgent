using System.Text.Json.Serialization;

namespace MossAgent.Domain;

public sealed class AttachmentImage
{
    public const int MaximumByteCount = 5 * 1024 * 1024;
    public const int MaximumTotalByteCount = 10 * 1024 * 1024;
    public const int MaximumDimension = 8192;
    public const long MaximumPixels = 16 * 1024 * 1024;

    private AttachmentImage(string mediaType, string data, int byteCount, int width, int height)
    {
        MediaType = mediaType;
        Base64Data = data;
        ByteCount = byteCount;
        Width = width;
        Height = height;
    }

    public string MediaType { get; }
    [JsonIgnore] public string Base64Data { get; }
    public int ByteCount { get; }
    public int Width { get; }
    public int Height { get; }

    public static AttachmentImage FromDecodedBytes(string mediaType, ReadOnlySpan<byte> bytes, int width, int height)
    {
        if (!IsSupportedMediaType(mediaType) || bytes.IsEmpty || bytes.Length > MaximumByteCount
            || width is < 1 or > MaximumDimension || height is < 1 or > MaximumDimension
            || (long)width * height > MaximumPixels)
            throw new ArgumentException("Invalid image snapshot metadata.");
        return new(mediaType, Convert.ToBase64String(bytes), bytes.Length, width, height);
    }

    public static bool IsSupportedMediaType(string mediaType) =>
        mediaType is "image/png" or "image/jpeg" or "image/gif" or "image/webp";

    public static string? DetectMediaType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.StartsWith(new byte[] { 255, 216, 255 })) return "image/jpeg";
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8)) return "image/gif";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }

    public override string ToString() => $"AttachmentImage({MediaType}, {ByteCount} bytes, {Width}x{Height})";
}
