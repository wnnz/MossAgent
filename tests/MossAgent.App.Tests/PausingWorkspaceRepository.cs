using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class PausingWorkspaceRepository(IWorkspaceRepository inner) : IWorkspaceRepository
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool PauseAfterCommit { get; init; }
    public bool IgnoreCancellationWhilePaused { get; init; }

    public async Task SaveTaskWithMessagesAsync(AgentTask task, IReadOnlyList<ConversationMessage> messages, CancellationToken cancellationToken)
    {
        if (PauseAfterCommit) await inner.SaveTaskWithMessagesAsync(task, messages, cancellationToken);
        Entered.TrySetResult();
        if (IgnoreCancellationWhilePaused) await Release.Task;
        else await Release.Task.WaitAsync(cancellationToken);
        if (!PauseAfterCommit) await inner.SaveTaskWithMessagesAsync(task, messages, cancellationToken);
    }

    public Task<IReadOnlyList<ProjectProfile>> GetProjectsAsync(CancellationToken cancellationToken) => inner.GetProjectsAsync(cancellationToken);
    public Task SaveProjectAsync(ProjectProfile project, CancellationToken cancellationToken) => inner.SaveProjectAsync(project, cancellationToken);
    public Task<IReadOnlyList<AgentTask>> GetTasksAsync(Guid? projectId, CancellationToken cancellationToken) => inner.GetTasksAsync(projectId, cancellationToken);
    public Task<IReadOnlyList<AgentTask>> GetArchivedTasksAsync(Guid? projectId, CancellationToken cancellationToken) => inner.GetArchivedTasksAsync(projectId, cancellationToken);
    public Task<IReadOnlyList<AgentTask>> GetPinnedTasksAsync(CancellationToken cancellationToken) => inner.GetPinnedTasksAsync(cancellationToken);
    public Task SaveTaskAsync(AgentTask task, CancellationToken cancellationToken) => inner.SaveTaskAsync(task, cancellationToken);
    public Task<AgentTask?> RenameTaskAsync(AgentTask expectedTask, string title, CancellationToken cancellationToken) => inner.RenameTaskAsync(expectedTask, title, cancellationToken);
    public Task<bool> SetTaskPinnedAsync(Guid taskId, bool isPinned, CancellationToken cancellationToken) => inner.SetTaskPinnedAsync(taskId, isPinned, cancellationToken);
    public Task SetTaskArchivedAsync(Guid taskId, bool isArchived, CancellationToken cancellationToken) => inner.SetTaskArchivedAsync(taskId, isArchived, cancellationToken);
    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(Guid taskId, CancellationToken cancellationToken) => inner.GetMessagesAsync(taskId, cancellationToken);
    public Task AppendMessageAsync(ConversationMessage message, CancellationToken cancellationToken) => inner.AppendMessageAsync(message, cancellationToken);
}
