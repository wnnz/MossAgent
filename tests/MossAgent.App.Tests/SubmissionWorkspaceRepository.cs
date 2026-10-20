using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class SubmissionWorkspaceRepository(IWorkspaceRepository inner) : IWorkspaceRepository, IConversationSubmissionRepository
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool PauseAfterCommit { get; init; }
    public bool SupportsDrafts(IConversationDraftRepository drafts) => ((IConversationSubmissionRepository)inner).SupportsDrafts(drafts);

    public async Task SaveSubmissionAsync(AgentTask task, ConversationMessage message,
        IReadOnlyList<ConversationDraftUpdate> drafts, CancellationToken cancellationToken)
    {
        var submissions = (IConversationSubmissionRepository)inner;
        if (PauseAfterCommit) await submissions.SaveSubmissionAsync(task, message, drafts, cancellationToken);
        Entered.TrySetResult();
        if (PauseAfterCommit) await Release.Task;
        else await Release.Task.WaitAsync(cancellationToken);
        if (!PauseAfterCommit) await submissions.SaveSubmissionAsync(task, message, drafts, cancellationToken);
    }

    public Task<IReadOnlyList<ProjectProfile>> GetProjectsAsync(CancellationToken token) => inner.GetProjectsAsync(token);
    public Task SaveProjectAsync(ProjectProfile project, CancellationToken token) => inner.SaveProjectAsync(project, token);
    public Task<IReadOnlyList<AgentTask>> GetTasksAsync(Guid? projectId, CancellationToken token) => inner.GetTasksAsync(projectId, token);
    public Task<IReadOnlyList<AgentTask>> GetArchivedTasksAsync(Guid? projectId, CancellationToken token) => inner.GetArchivedTasksAsync(projectId, token);
    public Task<IReadOnlyList<AgentTask>> GetPinnedTasksAsync(CancellationToken token) => inner.GetPinnedTasksAsync(token);
    public Task SaveTaskAsync(AgentTask task, CancellationToken token) => inner.SaveTaskAsync(task, token);
    public Task SaveTaskWithMessagesAsync(AgentTask task, IReadOnlyList<ConversationMessage> messages, CancellationToken token) => inner.SaveTaskWithMessagesAsync(task, messages, token);
    public Task<AgentTask?> RenameTaskAsync(AgentTask task, string title, CancellationToken token) => inner.RenameTaskAsync(task, title, token);
    public Task<bool> SetTaskPinnedAsync(Guid taskId, bool value, CancellationToken token) => inner.SetTaskPinnedAsync(taskId, value, token);
    public Task SetTaskArchivedAsync(Guid taskId, bool value, CancellationToken token) => inner.SetTaskArchivedAsync(taskId, value, token);
    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(Guid taskId, CancellationToken token) => inner.GetMessagesAsync(taskId, token);
    public Task AppendMessageAsync(ConversationMessage message, CancellationToken token) => inner.AppendMessageAsync(message, token);
}
