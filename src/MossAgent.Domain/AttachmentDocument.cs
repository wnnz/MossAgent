using System.Text.Json.Serialization;

namespace MossAgent.Domain;

public sealed class AttachmentDocument
{
    public const int MaximumByteCount = 16 * 1024 * 1024;
    public const int MaximumTotalByteCount = 32 * 1024 * 1024;
    public const int MaximumCount = 4;
    public const string PdfMediaType = "application/pdf";

    private AttachmentDocument(string mediaType, int byteCount, string base64Data)
    {
        MediaType = mediaType;
        ByteCount = byteCount;
        Base64Data = base64Data;
    }

    public string MediaType { get; }
    public int ByteCount { get; }
    [JsonIgnore] public string Base64Data { get; }

    public static AttachmentDocument FromDecodedBytes(string mediaType, ReadOnlySpan<byte> bytes)
    {
        if (!IsSupportedMediaType(mediaType) || bytes.Length < 8
            || bytes.Length > MaximumByteCount
            || !bytes.StartsWith("%PDF-"u8)
            || bytes[(bytes.Length - Math.Min(bytes.Length, 2048))..].IndexOf("%%EOF"u8) < 0)
            throw new InvalidDataException("Invalid PDF attachment content.");
        return new(mediaType, bytes.Length, Convert.ToBase64String(bytes));
    }

    public static bool IsSupportedMediaType(string mediaType) =>
        mediaType == PdfMediaType;

    public static string? DetectMediaType(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith("%PDF-"u8) ? PdfMediaType : null;

    public override string ToString() =>
        $"AttachmentDocument({MediaType}, {ByteCount} bytes)";
}
