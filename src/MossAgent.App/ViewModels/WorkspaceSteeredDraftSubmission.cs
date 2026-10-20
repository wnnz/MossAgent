using MossAgent.Application.Conversations;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceSteeredDraftSubmission(
    IConversationDraftRepository drafts, ConversationDraftScope scope, string text,
    Func<CancellationToken, Task<WorkspaceSteeringDraftLease>> capture, Action<Guid, ConversationDraftUpdate[]> accept, Action conflict)
{
    public async Task<ConversationSteeringAcceptance> SaveAsync(ConversationSteeringSession session,
        Guid expectedRunId, Guid inputId, CancellationToken cancellationToken)
    {
        if (scope.TaskId != session.TaskId) throw new ConversationSteeringException("SteeringRunUnavailable");
        using var lease = await capture(cancellationToken);
        var updates = new[] { lease.Update };
        if (updates[0].Scope != scope || updates[0].AttachmentItems.Count != 0)
            throw new ConversationSteeringException("SteeringTextOnly");
        ConversationSteeringAcceptance receipt;
        try { receipt = await session.AcceptAsync(expectedRunId, inputId, text, updates[0], cancellationToken); }
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
