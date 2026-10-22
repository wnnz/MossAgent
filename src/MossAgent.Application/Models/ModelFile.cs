using System.Text.Json.Serialization;
using MossAgent.Domain;

namespace MossAgent.Application.Models;

public sealed class ModelFile
{
    private ModelFile(string fileName, string mediaType, int byteCount, string base64Data)
    {
        FileName = fileName;
        MediaType = mediaType;
        ByteCount = byteCount;
        Base64Data = base64Data;
    }

    public string FileName { get; }
    public string MediaType { get; }
    public int ByteCount { get; }
    [JsonIgnore]
    public string Base64Data { get; }
    [JsonIgnore]
    public int ContextCharacterCost => Base64Data.Length + FileName.Length + 160;

    public static ModelFile FromAttachment(string fileName, AttachmentDocument document)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255
            || fileName.Contains('/') || fileName.Contains('\\')
            || fileName.Contains('\0')
            || !AttachmentDocument.IsSupportedMediaType(document.MediaType))
            throw new ArgumentException("Invalid model file input.");
        return new(fileName, document.MediaType, document.ByteCount, document.Base64Data);
    }

    public static ModelFile FromShared(SharedFileContent file) =>
        new(file.FileName, file.MediaType, file.ByteCount, file.Base64Data);

    public override string ToString() =>
        $"ModelFile(FileName={FileName}, MediaType={MediaType}, ByteCount={ByteCount})";
}
