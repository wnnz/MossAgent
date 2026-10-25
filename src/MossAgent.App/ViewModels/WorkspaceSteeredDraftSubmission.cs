using MossAgent.Application.Conversations;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceSteeredDraftSubmission(
    IConversationDraftRepository drafts, ConversationDraftScope scope, string text,
    Func<CancellationToken, Task<WorkspaceSteeringDraftLease>> capture,
    Action<Guid, IReadOnlyCollection<Guid>, ConversationDraftUpdate[]> accept, Action conflict)
{
    public async Task<ConversationSteeringAcceptance> SaveAsync(ConversationSteeringSession session,
        Guid expectedRunId, Guid inputId, CancellationToken cancellationToken)
    {
        if (scope.TaskId != session.TaskId) throw new ConversationSteeringException("SteeringRunUnavailable");
        using var lease = await capture(cancellationToken);
        var update = lease.Update;
        if (update.Scope != scope) throw new ConversationSteeringException("SteeringRunUnavailable");
        var submitted = update.AttachmentItems.Select(item => item.Id).ToArray();
        var committed = new[] { update with { Attachments = null } };
        ConversationSteeringAcceptance receipt;
        try { receipt = await session.AcceptAsync(expectedRunId, inputId, text, update, cancellationToken); }
        catch (ConversationDraftConflictException) { conflict(); throw; }
        if (!receipt.AlreadyAccepted) accept(session.TaskId, submitted, committed);
        else
        {
            var saved = await drafts.GetAsync(scope, cancellationToken);
            if (saved is not null && saved.Revision == committed[0].ExpectedRevision + 1
                && saved.Text == committed[0].Text && saved.AttachmentItems.SequenceEqual(committed[0].AttachmentItems))
                accept(session.TaskId, submitted, committed);
        }
        return receipt;
    }
}
