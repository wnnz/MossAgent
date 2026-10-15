using MossAgent.Application.Git;
using MossAgent.Application.Agent;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceTaskFactory(
    IWorkspaceRepository workspaces,
    IGitWorktreeService worktrees)
{
    public async Task<AgentTask> CreateAsync(
        ProjectProfile? project,
        string prompt,
        ApprovalPolicy approvalPolicy,
        bool useWorktree,
        CancellationToken cancellationToken,
        Guid? reservedTaskId = null)
    {
        if (project is null && useWorktree) throw new InvalidOperationException("A worktree requires a project.");
        var now = DateTimeOffset.UtcNow;
        var taskId = reservedTaskId ?? Guid.NewGuid();
        var title = prompt.Length <= 48 ? prompt : prompt[..48];
        var worktreePath = useWorktree
            ? await worktrees.CreateAsync(project!, taskId, cancellationToken)
            : null;
        var task = new AgentTask(
            taskId, project?.Id, title, project is null ? ApprovalPolicy.ReadOnly : approvalPolicy,
            AgentTaskStatus.Running, now, now, worktreePath);
        await workspaces.SaveTaskWithMessagesAsync(task,
            [new ConversationMessage(Guid.NewGuid(), task.Id, MessageRole.User, prompt, now)], cancellationToken);
        return task;
    }

    public IReadOnlyList<string> GetAuthorizedRoots(
        ProjectProfile project,
        AgentTask task)
    {
        if (task.WorktreePath is null)
        {
            return project.AuthorizedDirectories;
        }

        return project.AuthorizedDirectories
            .Where(path => !path.Equals(project.PrimaryDirectory, StringComparison.OrdinalIgnoreCase))
            .Prepend(task.WorktreePath)
            .ToArray();
    }

    public async Task<AgentTask> ResumeAsync(
        AgentTask task,
        string prompt,
        ApprovalPolicy approvalPolicy,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var updated = task with
        {
            ApprovalPolicy = task.ProjectId is null ? ApprovalPolicy.ReadOnly : approvalPolicy,
            Status = AgentTaskStatus.Running,
            FailureReason = null,
            UpdatedAt = now
        };
        await workspaces.SaveTaskWithMessagesAsync(updated,
            [new ConversationMessage(Guid.NewGuid(), task.Id, MessageRole.User, prompt, now)], cancellationToken);
        return updated;
    }

    public async Task<AgentTask> CompleteAsync(
        AgentTask task,
        string content,
        AgentTaskStatus status,
        CancellationToken cancellationToken,
        TaskFailureReason? failureReason = null)
    {
        var updated = task with
        {
            Status = status,
            UpdatedAt = DateTimeOffset.UtcNow,
            FailureReason = TaskFailureClassifier.Normalize(status, failureReason)
        };
        ConversationMessage[] messages = string.IsNullOrWhiteSpace(content) ? [] :
            [new ConversationMessage(Guid.NewGuid(), task.Id, MessageRole.Assistant, content, updated.UpdatedAt)];
        await workspaces.SaveTaskWithMessagesAsync(updated, messages, cancellationToken);
        return updated;
    }

    public async Task<AgentTask> SetStatusAsync(
        AgentTask task,
        AgentTaskStatus status,
        CancellationToken cancellationToken)
    {
        var updated = task with
        {
            Status = status,
            UpdatedAt = DateTimeOffset.UtcNow,
            FailureReason = TaskFailureClassifier.Normalize(status, task.FailureReason)
        };
        await workspaces.SaveTaskAsync(updated, cancellationToken);
        return updated;
    }
}
