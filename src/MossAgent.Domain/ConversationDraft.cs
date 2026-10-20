namespace MossAgent.Domain;

public sealed record ConversationDraft(string Text, long Revision, IReadOnlyList<ConversationDraftAttachment>? Attachments = null)
{
    public const int MaximumTextLength = 262144;
    public IReadOnlyList<ConversationDraftAttachment> AttachmentItems => Attachments ?? [];
}
