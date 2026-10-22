using MossAgent.Domain;

namespace MossAgent.Application.Persistence;

public interface IConversationSteeringRepository
{
    bool SupportsDrafts(IConversationDraftRepository drafts);
    Task<ConversationSteeringRun?> OpenAsync(AgentTask expectedTask, Guid runId, CancellationToken cancellationToken,
        ConversationSteeringAuthorization? authorization = null);
    Task<ConversationSteeringRun?> GetRunAsync(Guid taskId, Guid runId, CancellationToken cancellationToken);
    Task<ConversationSteeringPending?> GetPendingAsync(Guid taskId, Guid runId, CancellationToken cancellationToken);
    Task<ConversationSteeringPage> GetInputsAsync(Guid taskId, long beforeSequence, int pageSize, CancellationToken cancellationToken);
    Task<ConversationSteeringAcceptance?> AcceptAsync(Guid taskId, Guid runId, Guid inputId, string text,
        ConversationDraftUpdate? draft, CancellationToken cancellationToken, ConversationSteeringAuthorization? authorization = null,
        IReadOnlyList<ConversationAttachment>? attachments = null, IReadOnlyCollection<Guid>? draftAttachmentIds = null);
    Task<ConversationSteeringApplication?> ApplyAsync(ConversationSteeringRun expected, IReadOnlyList<Guid> inputIds,
        ConversationMessage? precedingAssistant, CancellationToken cancellationToken, ConversationSteeringAuthorization? authorization = null);
    Task<bool> CloseAsync(ConversationSteeringRun expected, bool interruptPending, CancellationToken cancellationToken);
}
