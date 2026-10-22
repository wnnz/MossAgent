namespace MossAgent.Domain;

public sealed record SharedImageContent(
    string MediaType,
    int ByteCount,
    string Base64Data)
{
    public static SharedImageContent FromEncoded(
        string mediaType, int byteCount, string base64Data)
    {
        if (!AttachmentImage.IsSupportedMediaType(mediaType)
            || byteCount is < 1 or > AttachmentImage.MaximumByteCount
            || string.IsNullOrWhiteSpace(base64Data)
            || base64Data.Length != checked((byteCount + 2) / 3 * 4)
            || base64Data.Any(static character =>
                character is not (>= 'A' and <= 'Z')
                and not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9')
                and not '+' and not '/' and not '='))
            throw new ArgumentException("Invalid shared image input.");
        return new(mediaType, byteCount, base64Data);
    }
}
