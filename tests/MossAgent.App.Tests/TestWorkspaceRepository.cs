using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class TestWorkspaceRepository : IWorkspaceRepository
{
    public ProjectProfile? SavedProject { get; private set; }
    public AgentTask? SavedTask { get; private set; }
    public List<(Guid TaskId, bool IsArchived)> ArchiveOperations { get; } = [];
    public IReadOnlyList<AgentTask> ArchivedTasks { get; set; } = [];

    public Task<IReadOnlyList<ProjectProfile>> GetProjectsAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectProfile>>([]);

    public Task SaveProjectAsync(ProjectProfile project, CancellationToken cancellationToken)
    {
        SavedProject = project;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AgentTask>> GetTasksAsync(
        Guid projectId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AgentTask>>([]);

    public Task<IReadOnlyList<AgentTask>> GetArchivedTasksAsync(
        Guid projectId,
        CancellationToken cancellationToken) =>
        Task.FromResult(ArchivedTasks);

    public Task SaveTaskAsync(AgentTask task, CancellationToken cancellationToken)
    {
        SavedTask = task;
        return Task.CompletedTask;
    }

    public Task SetTaskArchivedAsync(
        Guid taskId,
        bool isArchived,
        CancellationToken cancellationToken)
    {
        ArchiveOperations.Add((taskId, isArchived));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        Guid taskId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ConversationMessage>>([]);

    public Task AppendMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
