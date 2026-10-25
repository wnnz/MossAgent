namespace MossAgent.Application.Models;

public struct ModelImageRequestValidation(bool supportsImages)
{
    public const int MaximumImages = 10;
    public const int MaximumTotalByteCount = 10 * 1024 * 1024;
    public const int MaximumJsonByteCount = 20 * 1024 * 1024;
    private int _count;
    private long _bytes;

    public readonly bool ContainsImages => _count > 0;

    /// <summary>
    /// Joint model x protocol image capability: the protocol must be able to
    /// carry image blocks and the model profile must declare image support.
    /// Unknown models stay fail-closed because their profiles default the flag
    /// to false until the user explicitly opts in.
    /// </summary>
    public static ModelImageRequestValidation For(ModelRequest request) =>
        new(request.Model.SupportsImages
            && ModelProtocolCapabilities.AcceptsImages(request.Provider.Protocol));

    public static void Validate(ModelRequest request)
    {
        var validation = For(request);
        foreach (var message in request.Messages)
            validation.Include(message);
    }

    public void Include(ModelMessage message)
    {
        if (message.Images.IsDefaultOrEmpty) return;
        if (!supportsImages)
            throw new ModelCapabilityException(ModelCapabilityException.UnsupportedImageInput,
                "The selected model does not support image inputs. Remove the image attachments "
                + "or switch to a model that supports images.");
        if (message.Role != ModelRole.User || message.Continuation is not null
            || message.ToolCallId is not null || message.ToolName is not null
            || message.ToolCalls is { Count: > 0 })
            throw new ArgumentException("Only plain user messages may contain image inputs.", nameof(message));
        foreach (var image in message.Images)
        {
            if (image is null)
                throw new ArgumentException("An image input is unavailable.", nameof(message));
            _bytes += image.ByteCount;
            if (++_count > MaximumImages || _bytes > MaximumTotalByteCount)
                throw new ArgumentException("Image inputs exceed the request limit.", nameof(message));
        }
    }
}
