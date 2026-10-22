using System.Text.Json.Serialization;

namespace MossAgent.Domain;

public sealed class SharedFileContent
{
    private SharedFileContent(string fileName, string mediaType, int byteCount, string base64Data)
    {
        FileName = fileName;
        MediaType = mediaType;
        ByteCount = byteCount;
        Base64Data = base64Data;
    }

    public string FileName { get; }
    public string MediaType { get; }
    public int ByteCount { get; }
    [JsonIgnore] public string Base64Data { get; }

    public static SharedFileContent FromEncoded(
        string fileName, string mediaType, int byteCount, string base64Data)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255
            || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains('\0')
            || !AttachmentDocument.IsSupportedMediaType(mediaType)
            || byteCount is < 1 or > AttachmentDocument.MaximumByteCount
            || string.IsNullOrWhiteSpace(base64Data))
            throw new ArgumentException("Invalid shared file content.");
        return new(fileName, mediaType, byteCount, base64Data);
    }
}
