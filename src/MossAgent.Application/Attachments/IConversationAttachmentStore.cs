using MossAgent.Domain;

namespace MossAgent.Application.Attachments;

public interface IConversationAttachmentStore
{
    Task<IReadOnlyList<ConversationAttachment>> SaveAsync(Guid taskId, Guid messageId,
        IReadOnlyList<WorkspaceAttachmentContent> attachments, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceAttachmentContent>> ReadAsync(Guid taskId, Guid messageId,
        IReadOnlyList<ConversationAttachment> attachments, CancellationToken cancellationToken);
}
