using MossAgent.Application.Conversations;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceSteeringDraftCoordinator(object gate, SemaphoreSlim writes,
    Func<ConversationDraftScope, WorkspaceComposerDraftEntry> getEntry, Func<bool> unavailable, Action changed)
{
    public async Task<WorkspaceSteeringDraftLease> AcquireAsync(ConversationDraftScope scope, string text, CancellationToken token)
    {
        using var writing = await WorkspaceDraftWriteLease.AcquireAsync(writes, token);
        lock (gate)
        {
            token.ThrowIfCancellationRequested();
            if (scope.TaskId is null || unavailable()) throw new ConversationSteeringException("SteeringStopped");
            var entry = getEntry(scope);
            if (entry.IsSteeringSubmission) throw new ConversationSteeringException("SteeringBusy");
            var update = WorkspaceDraftSubmissionState.Capture(entry, entry, text, []).Single();
            entry.IsSteeringSubmission = true;
            return new(update, () =>
            {
                lock (gate) entry.IsSteeringSubmission = false;
                changed();
            });
        }
    }
}
