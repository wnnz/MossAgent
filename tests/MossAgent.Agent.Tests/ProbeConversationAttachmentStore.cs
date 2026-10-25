using MossAgent.Application.Attachments;
using MossAgent.Domain;

namespace MossAgent.Agent.Tests;

internal sealed class ProbeConversationAttachmentStore : IConversationAttachmentStore
{
    public List<(Guid TaskId, Guid MessageId)> Reads { get; } = [];
    public Exception? ReadFailure { get; set; }
    public Func<IReadOnlyList<ConversationAttachment>, IReadOnlyList<WorkspaceAttachmentContent>> Resolve { get; set; } =
        attachments => attachments.Select(item => new WorkspaceAttachmentContent("source.txt", item.DisplayPath,
            "stored " + item.DisplayPath, item.ByteCount)).ToArray();

    public Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadAsync(Guid taskId, Guid messageId,
        IReadOnlyList<ConversationAttachment> attachments, CancellationToken cancellationToken)
    {
        Reads.Add((taskId, messageId));
        if (ReadFailure is not null) return Task.FromException<IReadOnlyList<WorkspaceAttachmentContent>>(ReadFailure);
        return Task.FromResult(Resolve(attachments));
    }

    public Task<IReadOnlyList<ConversationAttachment>> SaveAsync(Guid taskId, Guid messageId,
        IReadOnlyList<WorkspaceAttachmentContent> attachments, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
