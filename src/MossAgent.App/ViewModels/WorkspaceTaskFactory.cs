using MossAgent.Application.Git;
using MossAgent.Application.Agent;
using MossAgent.Application.Persistence;
using MossAgent.Application.Attachments;
using MossAgent.Application.SourceControl;
using MossAgent.Application.Authorization;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceTaskFactory(
    IWorkspaceRepository workspaces,
    IGitWorktreeService worktrees,
    IConversationAttachmentStore? attachments = null,
    TimeProvider? timeProvider = null,
    SourceReviewChatAccess? reviewChats = null, ProjectAuthorizationMonitor? authorizations = null)
{
    public IConversationSubmissionRepository? Submissions => workspaces as IConversationSubmissionRepository;

    internal Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(Guid taskId, CancellationToken cancellationToken) =>
        workspaces.GetMessagesAsync(taskId, cancellationToken);

    internal Task<IReadOnlyList<ConversationAttachment>> SnapshotAttachmentsAsync(Guid taskId, Guid messageId,
        IReadOnlyList<WorkspaceAttachmentContent> selected, CancellationToken cancellationToken) =>
        selected.Count == 0 ? Task.FromResult<IReadOnlyList<ConversationAttachment>>([])
            : (attachments ?? throw new InvalidOperationException("Attachment persistence is unavailable."))
                .SaveAsync(taskId, messageId, selected, cancellationToken);

    public Task<AgentTask> PrepareAsync(ProjectProfile? project, AgentTask? task, string prompt,
        ApprovalPolicy approvalPolicy, IReadOnlyList<WorkspaceAttachmentContent> selectedAttachments,
        CancellationToken cancellationToken, bool useWorktree = false, WorkspaceDraftSubmission? submission = null,
        string? baseReference = null)
    {
        if (useWorktree && task is not null) throw new InvalidOperationException("An existing conversation cannot change its environment.");
        if (baseReference is not null && (!useWorktree || task is not null))
            throw new InvalidOperationException("A base reference requires a new worktree conversation.");
        return task is null
            ? CreateAsync(project, prompt, approvalPolicy, useWorktree, cancellationToken,
                attachments: selectedAttachments, submission: submission, baseReference: baseReference)
            : ResumeAsync(task, prompt, approvalPolicy, cancellationToken, selectedAttachments, submission);
    }

    public async Task<AgentTask> CreateAsync(
        ProjectProfile? project,
        string prompt,
        ApprovalPolicy approvalPolicy,
        bool useWorktree,
        CancellationToken cancellationToken,
        Guid? reservedTaskId = null,
        IReadOnlyList<WorkspaceAttachmentContent>? attachments = null, WorkspaceDraftSubmission? submission = null,
        string? baseReference = null)
    {
        if (project is null && useWorktree) throw new InvalidOperationException("A worktree requires a project.");
        if (!useWorktree && baseReference is not null) throw new InvalidOperationException("A base reference requires a worktree.");
        var now = DateTimeOffset.UtcNow;
        var taskId = reservedTaskId ?? Guid.NewGuid();
        var title = string.IsNullOrWhiteSpace(prompt) && attachments?.Any(item => item.Image is not null) == true
            ? WorkspaceText.Get("ImageConversationTitle") : prompt.Length <= 48 ? prompt : prompt[..48];
        var worktreePath = useWorktree
            ? await CreateWorktreeAsync(project!, taskId, baseReference, cancellationToken)
            : null;
        var task = new AgentTask(
            taskId, project?.Id, title, project is null ? ApprovalPolicy.ReadOnly : approvalPolicy,
            AgentTaskStatus.Running, now, now, worktreePath);
        try
        {
            var message = await CreateUserMessageAsync(task, prompt, attachments, cancellationToken);
            await SaveSubmissionAsync(task, message, submission, worktreePath, cancellationToken);
        }
        catch (Exception failure)
        {
            if (worktreePath is not null)
                await RecoverWorktreeAsync(project!, task, failure);
            throw;
        }
        return task;
    }

    private async Task<string> CreateWorktreeAsync(ProjectProfile project, Guid taskId, string? baseReference, CancellationToken cancellationToken)
    {
        try { return await worktrees.CreateAsync(project, taskId, cancellationToken, baseReference); }
        catch (GitWorktreeCreationException exception)
        {
            throw new WorkspaceWorktreePreparationException(exception.WorktreePath, exception);
        }
    }

    private async Task RecoverWorktreeAsync(ProjectProfile project, AgentTask task, Exception failure)
    {
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
            var active = await workspaces.GetTasksAsync(project.Id, deadline.Token).WaitAsync(deadline.Token);
            var archived = await workspaces.GetArchivedTasksAsync(project.Id, deadline.Token).WaitAsync(deadline.Token);
            if (active.Any(item => item.Id == task.Id) || archived.Any(item => item.Id == task.Id))
                throw new InvalidOperationException("A persisted task owns this worktree; refresh before recovering it.");
            await worktrees.RemoveAsync(project, task.WorktreePath!, deadline.Token);
            if (worktrees is IWorktreeCreationIntentService intents)
                await intents.MarkFailedAsync(
                    task.Id, task.WorktreePath!, "task_submission_failed", deadline.Token);
        }
        catch (Exception cleanupFailure)
        {
            throw new WorkspaceWorktreePreparationException(task.WorktreePath!, new AggregateException(failure, cleanupFailure));
        }
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

    public ITaskWorkspaceGuard? CreateWorkspaceGuard(ProjectProfile project, AgentTask task, IReadOnlyList<string> roots)
    {
        var environment = task.WorktreePath is null ? null
            : new ManagedWorktreeWorkspaceGuard(worktrees, project, task.Id, task.WorktreePath, roots);
        return authorizations is null ? environment
            : new ProjectWorkspaceGuard(authorizations, project, task.Id, task.WorktreePath ?? project.PrimaryDirectory, roots, environment);
    }

    public async Task<AgentTask> ResumeAsync(
        AgentTask task,
        string prompt,
        ApprovalPolicy approvalPolicy,
        CancellationToken cancellationToken,
        IReadOnlyList<WorkspaceAttachmentContent>? attachments = null, WorkspaceDraftSubmission? submission = null)
    {
        if (reviewChats is not null) await reviewChats.ResolveAsync(task, cancellationToken);
        if (task.WorktreePath is not null)
        {
            var project = (await workspaces.GetProjectsAsync(cancellationToken)).SingleOrDefault(item => item.Id == task.ProjectId)
                ?? throw new UnauthorizedAccessException("The worktree's project is unavailable.");
            await worktrees.ValidateAsync(project, task.Id, task.WorktreePath, cancellationToken);
        }
        var now = DateTimeOffset.UtcNow;
        var updated = task with
        {
            ApprovalPolicy = task.ProjectId is null ? ApprovalPolicy.ReadOnly : approvalPolicy,
            Status = AgentTaskStatus.Running,
            FailureReason = null,
            UpdatedAt = now
        };
        var message = await CreateUserMessageAsync(updated, prompt, attachments, cancellationToken);
        await SaveSubmissionAsync(updated, message, submission, null, cancellationToken);
        return updated;
    }

    private async Task SaveSubmissionAsync(AgentTask task, ConversationMessage message,
        WorkspaceDraftSubmission? submission, string? worktreePath, CancellationToken cancellationToken)
    {
        if (worktreePath is not null && worktrees is IWorktreeCreationIntentService { SupportsCreationIntents: true }
            && workspaces is IWorktreeCreationIntentSubmissionRepository intents)
        {
            await SaveAtomicWorktreeSubmissionAsync(task, message, submission, intents, cancellationToken);
            return;
        }
        if (submission is null) await workspaces.SaveTaskWithMessagesAsync(task, [message], cancellationToken);
        else await submission.SaveAsync(task, message, cancellationToken);
        if (worktreePath is not null && worktrees is IWorktreeCreationIntentService intentService)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5), timeProvider ?? TimeProvider.System);
            try { await intentService.MarkConsumedAsync(task.Id, worktreePath, deadline.Token); }
            catch { /* Startup reconciliation uses the persisted task if this bookkeeping update is unavailable. */ }
        }
    }

    private static Task SaveAtomicWorktreeSubmissionAsync(AgentTask task, ConversationMessage message,
        WorkspaceDraftSubmission? submission, IWorktreeCreationIntentSubmissionRepository repository,
        CancellationToken cancellationToken) => submission is null
        ? repository.SaveTaskWithMessagesAndWorktreeIntentAsync(task, [message], cancellationToken)
        : submission.SaveAsync(task, message, cancellationToken, repository.SaveSubmissionAndWorktreeIntentAsync);

    private async Task<ConversationMessage> CreateUserMessageAsync(AgentTask task, string prompt,
        IReadOnlyList<WorkspaceAttachmentContent>? selectedAttachments, CancellationToken cancellationToken)
    {
        var message = new ConversationMessage(Guid.NewGuid(), task.Id, MessageRole.User, prompt, task.UpdatedAt);
        if (selectedAttachments is not { Count: > 0 }) return message;
        var store = attachments ?? throw new InvalidOperationException("Attachment persistence is unavailable.");
        var snapshots = await store.SaveAsync(task.Id, message.Id, selectedAttachments, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ConversationAttachmentValidation.Validate(task.Id, message.Id, snapshots);
        if (snapshots.Count != selectedAttachments.Count) throw new InvalidDataException("Attachment snapshots are incomplete.");
        return message with { Attachments = snapshots.ToArray() };
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
