using MossAgent.Application.Conversations;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

/// <summary>
/// Commits one steering input together with the composer draft and attachment snapshots.
/// The draft consumption and the receipt are committed by the same steering transaction, so a
/// refused input keeps both the draft text and the composer attachments.
/// </summary>
internal sealed class WorkspaceSteeredDraftSubmission(
    IConversationDraftRepository drafts, ConversationDraftScope scope, string text,
    IReadOnlyCollection<Guid> attachmentIds, IReadOnlyList<ConversationAttachment> attachments,
    Func<CancellationToken, Task<WorkspaceSteeringDraftLease>> capture, Action<Guid, ConversationDraftUpdate[]> accept, Action conflict)
{
    public async Task<ConversationSteeringAcceptance> SaveAsync(ConversationSteeringSession session,
        Guid expectedRunId, Guid inputId, CancellationToken cancellationToken)
    {
        if (scope.TaskId != session.TaskId) throw new ConversationSteeringException("SteeringRunUnavailable");
        using var lease = await capture(cancellationToken);
        var updates = new[] { lease.Update };
        if (updates[0].Scope != scope || updates[0].AttachmentItems.Any(item => attachmentIds.Contains(item.Id)))
            throw new ConversationSteeringException("SteeringInvalid");
        ConversationSteeringAcceptance receipt;
        try
        {
            receipt = await session.AcceptAsync(expectedRunId, inputId, text, updates[0], cancellationToken,
                attachments, attachmentIds);
        }
        catch (ConversationDraftConflictException) { conflict(); throw; }
        if (!receipt.AlreadyAccepted) accept(session.TaskId, updates);
        else
        {
            var saved = await drafts.GetAsync(scope, cancellationToken);
            if (saved is not null && saved.Revision == updates[0].ExpectedRevision + 1
                && saved.Text == updates[0].Text && saved.AttachmentItems.SequenceEqual(updates[0].AttachmentItems))
                accept(session.TaskId, updates);
        }
        return receipt;
    }
}
