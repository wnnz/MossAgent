using MossAgent.Domain;

namespace MossAgent.Application.Persistence;

public interface IWorkspaceRepository
{
    Task<IReadOnlyList<ProjectProfile>> GetProjectsAsync(CancellationToken cancellationToken);
    Task SaveProjectAsync(ProjectProfile project, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentTask>> GetTasksAsync(Guid projectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentTask>> GetArchivedTasksAsync(
        Guid projectId,
        CancellationToken cancellationToken);
    Task SaveTaskAsync(AgentTask task, CancellationToken cancellationToken);
    Task SetTaskArchivedAsync(
        Guid taskId,
        bool isArchived,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(Guid taskId, CancellationToken cancellationToken);
    Task AppendMessageAsync(ConversationMessage message, CancellationToken cancellationToken);
}
