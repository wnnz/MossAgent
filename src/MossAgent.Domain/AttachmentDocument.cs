using System.Text.Json.Serialization;

namespace MossAgent.Domain;

public sealed class AttachmentDocument
{
    public const string PdfMediaType = "application/pdf";
    public const int MaximumByteCount = 10 * 1024 * 1024;
    public const int MaximumTotalByteCount = 25 * 1024 * 1024;
    public const int MaximumPageCount = 200;
    public const int MaximumTextCharacters = 200 * 1024;

    private AttachmentDocument(string mediaType, string data, int byteCount,
        int pageCount, int totalPages, int textCharacters, bool truncated)
    {
        MediaType = mediaType;
        Base64Data = data;
        ByteCount = byteCount;
        PageCount = pageCount;
        TotalPages = totalPages;
        TextCharacters = textCharacters;
        Truncated = truncated;
    }

    public string MediaType { get; }
    [JsonIgnore] public string Base64Data { get; }
    public int ByteCount { get; }
    public int PageCount { get; }
    public int TotalPages { get; }
    public int TextCharacters { get; }
    public bool Truncated { get; }

    public static AttachmentDocument FromExtractedText(string mediaType, ReadOnlySpan<byte> bytes,
        int pageCount, int totalPages, int textCharacters, bool truncated)
    {
        if (!IsSupportedMediaType(mediaType) || bytes.IsEmpty || bytes.Length > MaximumByteCount
            || pageCount is < 0 or > MaximumPageCount || totalPages < pageCount
            || textCharacters is < 0 or > MaximumTextCharacters || (!truncated && pageCount != totalPages))
            throw new ArgumentException("Invalid document snapshot metadata.");
        return new(mediaType, Convert.ToBase64String(bytes), bytes.Length,
            pageCount, totalPages, textCharacters, truncated);
    }

    public static bool IsSupportedMediaType(string mediaType) => mediaType is PdfMediaType;

    public static string? DetectMediaType(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith("%PDF-"u8) ? PdfMediaType : null;

    public override string ToString() =>
        $"AttachmentDocument({MediaType}, {ByteCount} bytes, {PageCount}/{TotalPages} pages{(Truncated ? ", truncated" : "")})";
}
