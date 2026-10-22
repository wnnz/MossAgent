using MossAgent.Domain;

namespace MossAgent.Application.Models;

public struct ModelFileRequestValidation(bool supportsFiles)
{
    public const int MaximumFiles = AttachmentDocument.MaximumCount;
    public const int MaximumTotalByteCount = AttachmentDocument.MaximumTotalByteCount;
    public const int MaximumJsonByteCount = 48 * 1024 * 1024;
    private int _count;
    private long _bytes;

    public readonly bool ContainsFiles => _count > 0;

    public static void Validate(ModelRequest request)
    {
        var validation = new ModelFileRequestValidation(request.Model.SupportsFiles);
        foreach (var message in request.Messages)
            validation.Include(message);
    }

    public void Include(ModelMessage message)
    {
        if (message.Files.IsDefaultOrEmpty) return;
        if (!supportsFiles)
            throw new ArgumentException("The selected model does not support file inputs.", nameof(message));
        if (message.Role != ModelRole.User || message.Continuation is not null
            || message.ToolCallId is not null || message.ToolName is not null
            || message.ToolCalls is { Count: > 0 })
            throw new ArgumentException("Only plain user messages may contain file inputs.", nameof(message));
        foreach (var file in message.Files)
        {
            if (file is null || string.IsNullOrWhiteSpace(file.FileName) || file.FileName.Length > 255
                || file.FileName.Contains('/') || file.FileName.Contains('\\') || file.FileName.Contains('\0')
                || !AttachmentDocument.IsSupportedMediaType(file.MediaType)
                || file.ByteCount is < 1 or > AttachmentDocument.MaximumByteCount
                || string.IsNullOrWhiteSpace(file.Base64Data))
                throw new ArgumentException("A file input is invalid.", nameof(message));
            _bytes += file.ByteCount;
            if (++_count > MaximumFiles || _bytes > MaximumTotalByteCount)
                throw new ArgumentException("File inputs exceed the request limit.", nameof(message));
        }
    }
}
