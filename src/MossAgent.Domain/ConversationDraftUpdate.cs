namespace MossAgent.Domain;

public sealed record ConversationDraftUpdate(ConversationDraftScope Scope, string Text, long ExpectedRevision,
    IReadOnlyList<ConversationDraftAttachment>? Attachments = null)
{
    public IReadOnlyList<ConversationDraftAttachment> AttachmentItems => Attachments ?? [];
}
