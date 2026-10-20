using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceDraftSubmission(
    IConversationSubmissionRepository repository,
    SemaphoreSlim writeGate,
    Func<Guid, ConversationDraftUpdate[]> capture,
    Action<Guid, ConversationDraftUpdate[]> accept,
    Action conflict)
{
    public async Task SaveAsync(AgentTask task, ConversationMessage message, CancellationToken cancellationToken)
    {
        using var lease = await WorkspaceDraftWriteLease.AcquireAsync(writeGate, cancellationToken);
        var updates = capture(task.Id);
        try { await repository.SaveSubmissionAsync(task, message, updates, cancellationToken); }
        catch (ConversationDraftConflictException) { conflict(); throw; }
        accept(task.Id, updates);
    }
}
