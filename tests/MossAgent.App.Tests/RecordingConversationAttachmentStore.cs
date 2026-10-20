using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class RecordingConversationAttachmentStore : IConversationAttachmentStore
{
    public Func<CancellationToken, Task<IReadOnlyList<WorkspaceAttachmentContent>>> ReadHandler { get; set; } =
        _ => Task.FromResult<IReadOnlyList<WorkspaceAttachmentContent>>([]);
    public Func<CancellationToken, Task<IReadOnlyList<ConversationAttachment>>> SaveHandler { get; set; } =
        _ => Task.FromResult<IReadOnlyList<ConversationAttachment>>([]);
    public List<Guid> ReadMessages { get; } = [];
    public CancellationToken ReadToken { get; private set; }

    public Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadAsync(Guid taskId, Guid messageId,
        IReadOnlyList<ConversationAttachment> attachments, CancellationToken cancellationToken)
    {
        ReadMessages.Add(messageId);
        ReadToken = cancellationToken;
        return ReadHandler(cancellationToken);
    }

    public Task<IReadOnlyList<ConversationAttachment>> SaveAsync(Guid taskId, Guid messageId,
        IReadOnlyList<WorkspaceAttachmentContent> attachments, CancellationToken cancellationToken) => SaveHandler(cancellationToken);
}
