using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class TestWorkspaceRepository : IWorkspaceRepository
{
    public ProjectProfile? SavedProject { get; private set; }

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

    public Task SaveTaskAsync(AgentTask task, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        Guid taskId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ConversationMessage>>([]);

    public Task AppendMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
