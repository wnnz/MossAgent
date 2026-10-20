using MossAgent.Domain;

namespace MossAgent.Application.SourceControl;

public interface ISourceReviewChats
{
    Task<AgentTask> OpenAsync(SourceReviewChatBinding binding, string title, CancellationToken cancellationToken);
    Task<SourceReviewChatBinding?> GetAsync(Guid taskId, CancellationToken cancellationToken);
}
