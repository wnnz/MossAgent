using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class FollowupPreparationAttachmentGate(IConversationAttachmentStore inner) : IConversationAttachmentStore
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => _entered.Task;
    public void Release() => _release.TrySetResult();

    public Task<IReadOnlyList<ConversationAttachment>> SaveAsync(Guid taskId, Guid messageId,
        IReadOnlyList<WorkspaceAttachmentContent> attachments, CancellationToken cancellationToken) =>
        inner.SaveAsync(taskId, messageId, attachments, cancellationToken);

    public async Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadAsync(Guid taskId, Guid messageId,
        IReadOnlyList<ConversationAttachment> attachments, CancellationToken cancellationToken)
    {
        var contents = await inner.ReadAsync(taskId, messageId, attachments, cancellationToken);
        _entered.TrySetResult();
        await _release.Task.WaitAsync(cancellationToken);
        return contents;
    }
}
