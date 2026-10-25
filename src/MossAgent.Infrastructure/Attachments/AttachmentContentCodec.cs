using System.Text;
using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Attachments;

internal sealed class AttachmentContentCodec(IAttachmentImageDecoder? images, IAttachmentDocumentTextExtractor? documents = null)
{
    private readonly UTF8Encoding _encoding = new(false, true);

    public static bool IsImagePath(string path) => Path.GetExtension(path).ToLowerInvariant()
        is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp";

    public static bool IsDocumentPath(string path) =>
        Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public byte[] Encode(WorkspaceAttachmentContent content)
    {
        if (content.Image is { } image)
        {
            if (content.Document is not null || content.Content.Length != 0
                || content.ByteCount != image.ByteCount || content.MediaType != image.MediaType)
                throw new InvalidDataException("Invalid image attachment content.");
            return Convert.FromBase64String(image.Base64Data);
        }
        if (content.Document is { } document)
        {
            if (content.MediaType != document.MediaType || content.ByteCount != document.ByteCount
                || content.Content.Length != document.TextCharacters || content.Content.Contains('\0'))
                throw new InvalidDataException("Invalid document attachment content.");
            return Convert.FromBase64String(document.Base64Data);
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
        mediaType ??= Detect(path, bytes);
        if (mediaType == "text/plain")
        {
            if (bytes.Length > WorkspaceAttachmentLimits.MaximumBytesPerFile || bytes.Contains((byte)0))
                throw new InvalidDataException("Attachment must be bounded UTF-8 text.");
            return new(path, displayPath, _encoding.GetString(bytes), bytes.LongLength);
        }
        if (mediaType == AttachmentDocument.PdfMediaType)
        {
            if (bytes.Length is < 1 or > AttachmentDocument.MaximumByteCount)
                throw new AttachmentDocumentValidationException(AttachmentDocumentFailure.SizeLimit);
            var extractor = documents ?? throw new AttachmentDocumentValidationException(AttachmentDocumentFailure.ExtractorUnavailable);
            var extraction = await extractor.ExtractAsync(bytes, cancellationToken);
            var document = AttachmentDocument.FromExtractedText(mediaType, bytes, extraction.PageCount,
                extraction.TotalPages, extraction.Text.Length, extraction.Truncated);
            return new(path, displayPath, extraction.Text, bytes.LongLength, Document: document);
        }
        if (!AttachmentImage.IsSupportedMediaType(mediaType))
            throw new AttachmentImageValidationException(AttachmentImageFailure.Unsupported);
        var decoder = images ?? throw new AttachmentImageValidationException(AttachmentImageFailure.DecoderUnavailable);
        var image = await decoder.DecodeAsync(bytes, cancellationToken);
        if (image.MediaType != mediaType || image.ByteCount != bytes.Length)
            throw new AttachmentImageValidationException(AttachmentImageFailure.Invalid);
        return new(path, displayPath, string.Empty, image.ByteCount, image);
    }

    private static string Detect(string path, byte[] bytes)
    {
        var detected = (string?)AttachmentImage.DetectMediaType(bytes) ?? AttachmentDocument.DetectMediaType(bytes);
        if (IsDocumentPath(path))
            return detected == AttachmentDocument.PdfMediaType ? detected
                : throw new AttachmentDocumentValidationException(AttachmentDocumentFailure.Invalid);
        if (IsImagePath(path))
            return AttachmentImage.IsSupportedMediaType(detected ?? string.Empty) ? detected!
                : throw new AttachmentImageValidationException(AttachmentImageFailure.Invalid);
        return detected ?? "text/plain";
    }
}
