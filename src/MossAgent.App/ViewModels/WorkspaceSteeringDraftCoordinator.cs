using MossAgent.Application.Conversations;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceSteeringDraftCoordinator(object gate, SemaphoreSlim writes,
    Func<ConversationDraftScope, WorkspaceComposerDraftEntry> getEntry, Func<bool> unavailable, Action changed)
{
    /// <summary>
    /// Captures the composer text and the attachments that this steering input takes over.
    /// </summary>
    /// <param name="scope">Task-scoped draft being steered.</param>
    /// <param name="text">Steering text that must leave the draft when accepted.</param>
    /// <param name="attachmentIds">Composer attachment ids snapshotted into the steering input.</param>
    /// <param name="token">Cancellation token for the draft write gate.</param>
    public async Task<WorkspaceSteeringDraftLease> AcquireAsync(ConversationDraftScope scope, string text,
        IReadOnlyCollection<Guid> attachmentIds, CancellationToken token)
    {
        using var writing = await WorkspaceDraftWriteLease.AcquireAsync(writes, token);
        lock (gate)
        {
            token.ThrowIfCancellationRequested();
            if (scope.TaskId is null || unavailable()) throw new ConversationSteeringException("SteeringStopped");
            var entry = getEntry(scope);
            if (entry.IsSteeringSubmission) throw new ConversationSteeringException("SteeringBusy");
            var update = WorkspaceDraftSubmissionState.Capture(entry, entry, text, attachmentIds).Single();
            entry.IsSteeringSubmission = true;
            return new(update, () =>
            {
                lock (gate) entry.IsSteeringSubmission = false;
                changed();
            });
        }
    }
}
