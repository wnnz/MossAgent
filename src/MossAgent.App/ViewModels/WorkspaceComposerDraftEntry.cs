using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceComposerDraftEntry(ConversationDraftScope scope)
{
    public ConversationDraftScope Scope { get; } = scope;
    public string Text { get; set; } = string.Empty;
    public string SavedText { get; set; } = string.Empty;
    public ConversationDraftAttachment[] Attachments { get; set; } = [];
    public ConversationDraftAttachment[] SavedAttachments { get; set; } = [];
    public bool AttachmentsEdited { get; set; }
    public bool AttachmentsAuthorized { get; set; } = true;
    public long Revision { get; set; }
    public bool Edited { get; set; }
    public bool Loaded { get; set; }
    public bool IsSteeringSubmission { get; set; }
    public string? ErrorKey { get; set; }
    public Task? ReadTask { get; set; }
    public ConversationDraftUpdate? Attempt { get; set; }
    public bool CanSave => Text.Length <= ConversationDraft.MaximumTextLength && Attachments.All(attachment => attachment.IsAvailable);
    public bool NeedsSaving => (Edited || AttachmentsEdited)
        && (!Loaded || Text != SavedText || !Attachments.SequenceEqual(SavedAttachments));
}
