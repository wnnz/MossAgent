using System.Text;

namespace MossAgent.Domain;

public sealed record ConversationSteeringInput(
    Guid Id,
    Guid TaskId,
    Guid RunId,
    long Sequence,
    string Text,
    ConversationSteeringInputState State,
    DateTimeOffset AcceptedAt,
    DateTimeOffset? AppliedAt = null,
    DateTimeOffset? InterruptedAt = null,
    IReadOnlyList<ConversationAttachment>? Attachments = null)
{
    public const int MaximumTextLength = 32_768;
    public const int MaximumPendingItems = 16;
    public const int MaximumPendingBytes = 262_144;
    public const int MaximumPageSize = 64;

    public IReadOnlyList<ConversationAttachment> AttachmentItems => Attachments ?? [];

    public static void ValidateText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaximumTextLength || text.Contains('\0'))
            throw new ArgumentException("Invalid steering text.");
        try { _ = new UTF8Encoding(false, true).GetByteCount(text); }
        catch (EncoderFallbackException) { throw new ArgumentException("Invalid steering text encoding."); }
    }
}
