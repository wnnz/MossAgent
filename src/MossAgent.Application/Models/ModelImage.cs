using System.Text.Json.Serialization;
using MossAgent.Domain;

namespace MossAgent.Application.Models;

public sealed class ModelImage
{
    public const int MaximumByteCount = AttachmentImage.MaximumByteCount;

    private ModelImage(string mediaType, int byteCount, string base64Data)
    {
        MediaType = mediaType;
        ByteCount = byteCount;
        Base64Data = base64Data;
    }

    public static ModelImage FromBytes(string mediaType, ReadOnlySpan<byte> content)
    {
        if (mediaType is not ("image/png" or "image/jpeg" or "image/gif" or "image/webp"))
            throw new ArgumentException("Unsupported image media type.", nameof(mediaType));
        if (content.IsEmpty || content.Length > MaximumByteCount)
            throw new ArgumentException("Image content is empty or exceeds the size limit.", nameof(content));

        return new ModelImage(mediaType, content.Length, Convert.ToBase64String(content));
    }

    public string MediaType { get; }
    public int ByteCount { get; }
    [JsonIgnore] public string Base64Data { get; }
    [JsonIgnore] public int ContextCharacterCost => Base64Data.Length + 128;

    public static ModelImage FromAttachment(AttachmentImage image) =>
        new(image.MediaType, image.ByteCount, image.Base64Data);

    public override string ToString() => $"ModelImage(MediaType={MediaType}, ByteCount={ByteCount})";
}
