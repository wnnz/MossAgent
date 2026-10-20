using MossAgent.Application.Conversations;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceQueuedDraftSubmission(
    IConversationFollowupRepository repository,
    SemaphoreSlim writeGate,
    Func<Guid, ConversationDraftUpdate[]> capture,
    Action<Guid, ConversationDraftUpdate[]> accept,
    Action conflict)
{
    public async Task SaveAsync(ConversationFollowupQueue queue, CancellationToken cancellationToken)
    {
        using var lease = await WorkspaceDraftWriteLease.AcquireAsync(writeGate, cancellationToken);
        var updates = capture(queue.TaskId);
        if (!await repository.SaveAsync(queue, updates, cancellationToken))
        {
            if ((await repository.GetAsync(queue.TaskId, cancellationToken)).Revision == queue.Revision) conflict();
            throw new ConversationFollowupException("FollowupConflict");
        }
        accept(queue.TaskId, updates);
    }
}
