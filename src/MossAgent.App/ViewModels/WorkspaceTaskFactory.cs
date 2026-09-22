using MossAgent.Application.Git;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceTaskFactory(
    IWorkspaceRepository workspaces,
    IGitWorktreeService worktrees)
{
    public async Task<AgentTask> CreateAsync(
        ProjectProfile project,
        string prompt,
        ApprovalPolicy approvalPolicy,
        bool useWorktree,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var taskId = Guid.NewGuid();
        var title = prompt.Length <= 48 ? prompt : prompt[..48];
        var worktreePath = useWorktree
            ? await worktrees.CreateAsync(project, taskId, cancellationToken)
            : null;
        var task = new AgentTask(
            taskId, project.Id, title, approvalPolicy,
            AgentTaskStatus.Running, now, now, worktreePath);
        await workspaces.SaveTaskAsync(task, cancellationToken);
        await workspaces.AppendMessageAsync(
            new ConversationMessage(Guid.NewGuid(), task.Id, MessageRole.User, prompt, now),
            cancellationToken);
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
            ApprovalPolicy = approvalPolicy,
            Status = AgentTaskStatus.Running,
            UpdatedAt = now
        };
        await workspaces.SaveTaskAsync(updated, cancellationToken);
        await workspaces.AppendMessageAsync(
            new ConversationMessage(Guid.NewGuid(), task.Id, MessageRole.User, prompt, now),
            cancellationToken);
        return updated;
    }

    public async Task<AgentTask> CompleteAsync(
        AgentTask task,
        string content,
        AgentTaskStatus status,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            await workspaces.AppendMessageAsync(
                new ConversationMessage(
                    Guid.NewGuid(), task.Id, MessageRole.Assistant,
                    content, DateTimeOffset.UtcNow),
                cancellationToken);
        }

        return await SetStatusAsync(task, status, cancellationToken);
    }

    public async Task<AgentTask> SetStatusAsync(
        AgentTask task,
        AgentTaskStatus status,
        CancellationToken cancellationToken)
    {
        var updated = task with { Status = status, UpdatedAt = DateTimeOffset.UtcNow };
        await workspaces.SaveTaskAsync(updated, cancellationToken);
        return updated;
    }
}
