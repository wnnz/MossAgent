using System.Text;
using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Attachments;

internal sealed class AttachmentContentCodec(IAttachmentImageDecoder? images)
{
    private readonly UTF8Encoding _encoding = new(false, true);

    public static bool IsImagePath(string path) => Path.GetExtension(path).ToLowerInvariant()
        is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp";

    public byte[] Encode(WorkspaceAttachmentContent content)
    {
        if (content.Image is { } image)
        {
            if (content.Content.Length != 0 || content.ByteCount != image.ByteCount || content.MediaType != image.MediaType)
                throw new InvalidDataException("Invalid image attachment content.");
            return Convert.FromBase64String(image.Base64Data);
        }
        if (content.MediaType != "text/plain" || content.Content.Length > WorkspaceAttachmentLimits.MaximumBytesPerFile || content.Content.Contains('\0'))
            throw new InvalidDataException("Attachment must be bounded UTF-8 text.");
        var bytes = _encoding.GetBytes(content.Content);
        if (bytes.Length > WorkspaceAttachmentLimits.MaximumBytesPerFile)
            throw new InvalidDataException("Attachment text exceeds its byte limit.");
        return bytes;
    }

    public async Task<WorkspaceAttachmentContent> DecodeAsync(string path, string displayPath, byte[] bytes,
        string? mediaType, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        mediaType ??= AttachmentImage.DetectMediaType(bytes) ?? (IsImagePath(path)
            ? throw new AttachmentImageValidationException(AttachmentImageFailure.Invalid) : "text/plain");
        if (mediaType == "text/plain")
        {
            if (bytes.Length > WorkspaceAttachmentLimits.MaximumBytesPerFile || bytes.Contains((byte)0))
                throw new InvalidDataException("Attachment must be bounded UTF-8 text.");
            return new(path, displayPath, _encoding.GetString(bytes), bytes.LongLength);
        }
        if (!AttachmentImage.IsSupportedMediaType(mediaType))
            throw new AttachmentImageValidationException(AttachmentImageFailure.Unsupported);
        var decoder = images ?? throw new AttachmentImageValidationException(AttachmentImageFailure.DecoderUnavailable);
        var image = await decoder.DecodeAsync(bytes, cancellationToken);
        if (image.MediaType != mediaType || image.ByteCount != bytes.Length)
            throw new AttachmentImageValidationException(AttachmentImageFailure.Invalid);
        return new(path, displayPath, string.Empty, image.ByteCount, image);
    }
}
