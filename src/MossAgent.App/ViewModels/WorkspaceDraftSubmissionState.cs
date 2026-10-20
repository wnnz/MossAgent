using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceDraftSubmissionState
{
    public static ConversationDraftUpdate[] Capture(WorkspaceComposerDraftEntry source, WorkspaceComposerDraftEntry target,
        string sentText, IReadOnlyCollection<Guid> attachmentIds)
    {
        if (!source.Loaded || !source.CanSave || source.ErrorKey is not null || !source.AttachmentsAuthorized)
            throw new InvalidOperationException("The conversation draft is unavailable.");
        var remaining = source.Attachments.Where(attachment => !attachmentIds.Contains(attachment.Id)).ToArray();
        var update = new ConversationDraftUpdate(target.Scope, source.Text == sentText ? string.Empty : source.Text,
            target.Revision, remaining);
        return ReferenceEquals(source, target) ? [update]
            : [new(source.Scope, string.Empty, source.Revision), update];
    }

    public static void Accept(WorkspaceComposerDraftEntry source, WorkspaceComposerDraftEntry target, string sentText,
        IReadOnlyCollection<Guid>? attachmentIds, IReadOnlyList<ConversationDraftUpdate>? committed)
    {
        var nextText = source.Text == sentText ? string.Empty : source.Text;
        var remaining = source.Attachments.Where(attachment => attachmentIds?.Contains(attachment.Id) != true).ToArray();
        var authorized = source.AttachmentsAuthorized;
        if (!ReferenceEquals(source, target))
        {
            source.Text = string.Empty;
            source.Edited = source.AttachmentsEdited = true;
            source.Attachments = [];
            target.Loaded = true;
        }
        target.Text = nextText;
        target.Edited = target.AttachmentsEdited = true;
        target.Attachments = remaining;
        target.AttachmentsAuthorized = remaining.Length == 0 || authorized;
        foreach (var update in committed ?? [])
        {
            var entry = update.Scope == source.Scope ? source : target;
            entry.SavedText = update.Text;
            entry.SavedAttachments = update.AttachmentItems.ToArray();
            entry.Revision = update.ExpectedRevision + 1;
            entry.Attempt = null;
        }
    }
}
