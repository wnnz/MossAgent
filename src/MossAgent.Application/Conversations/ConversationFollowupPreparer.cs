using MossAgent.Application.Attachments;
using MossAgent.Application.Git;
using MossAgent.Application.Models;
using MossAgent.Application.Persistence;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Application.Conversations;

public sealed class ConversationFollowupPreparer(
    IConfigurationRepository configurations,
    IAgentConfigurationRepository agents,
    IWorkspaceRepository workspaces,
    IGitWorktreeService worktrees,
    IAuthorizedPathResolver paths,
    IConversationAttachmentStore attachments,
    SourceReviewChatAccess? reviews = null)
{
    public Task<PreparedConversationFollowup> PrepareAsync(ConversationFollowupQueue queue,
        CancellationToken cancellationToken) => Task.Run(() => PrepareCoreAsync(queue, cancellationToken), cancellationToken);

    private async Task<PreparedConversationFollowup> PrepareCoreAsync(ConversationFollowupQueue queue,
        CancellationToken cancellationToken)
    {
        ConversationFollowupValidation.Validate(queue);
        var item = queue.Items.FirstOrDefault() ?? throw new ConversationFollowupException("FollowupEmpty");
        var task = (await workspaces.GetTasksAsync(item.ProjectId, cancellationToken))
            .SingleOrDefault(task => task.Id == queue.TaskId) ?? throw new ConversationFollowupException("FollowupTaskUnavailable");
        if (task.Status is AgentTaskStatus.Running or AgentTaskStatus.WaitingForApproval)
            throw new ConversationFollowupException("FollowupTaskRunning");
        var provider = (await configurations.GetProvidersAsync(cancellationToken))
            .SingleOrDefault(provider => provider.Id == item.Model.ProviderId && provider.IsEnabled)
            ?? throw new ConversationFollowupException("FollowupModelUnavailable");
        var model = await ResolveModelAsync(provider, item, cancellationToken);
        var project = await ResolveProjectAsync(task, item, cancellationToken);
        if (reviews is not null) await reviews.ResolveAsync(task, cancellationToken);
        var contents = await attachments.ReadAsync(task.Id, item.Id, item.Attachments, cancellationToken);
        ConversationAttachmentValidation.ValidateModel(model, contents);
        cancellationToken.ThrowIfCancellationRequested();
        return new(task, project, provider, model, item, queue.Revision);
    }

    private async Task<ModelProfile> ResolveModelAsync(AiProvider provider, ConversationFollowup item, CancellationToken cancellationToken)
    {
        var current = (await configurations.GetModelsAsync(provider.Id, cancellationToken))
            .SingleOrDefault(model => model.Id == item.Model.Id && model.IsEnabled && model.ProviderId == provider.Id);
        if (current is null || current.ModelId != item.Model.ModelId
            || !ModelReasoningPolicy.Supports(provider.Protocol, item.Model.ModelId, item.Model.ReasoningEffort))
            throw new ConversationFollowupException("FollowupModelUnavailable");
        if (item.AgentProfileId is { } agentId && !(await agents.GetAllAsync(cancellationToken))
            .Any(agent => agent.Id == agentId && agent.IsEnabled))
            throw new ConversationFollowupException("FollowupAgentUnavailable");
        return item.Model with { SupportsImages = item.Model.SupportsImages && current.SupportsImages,
            SupportsFiles = item.Model.SupportsFiles && current.SupportsFiles };
    }

    private async Task<ProjectProfile?> ResolveProjectAsync(AgentTask task, ConversationFollowup item, CancellationToken cancellationToken)
    {
        if (task.ProjectId != item.ProjectId || task.WorktreePath != item.WorktreePath)
            throw new ConversationFollowupException("FollowupScopeChanged");
        if (task.ProjectId is null) return null;
        var project = (await workspaces.GetProjectsAsync(cancellationToken)).SingleOrDefault(project => project.Id == task.ProjectId)
            ?? throw new ConversationFollowupException("FollowupScopeChanged");
        if (task.WorktreePath is not null) await worktrees.ValidateAsync(project, task.Id, task.WorktreePath, cancellationToken);
        var roots = task.WorktreePath is null ? project.AuthorizedDirectories.ToArray() : project.AuthorizedDirectories
            .Where(path => !path.Equals(project.PrimaryDirectory, StringComparison.OrdinalIgnoreCase)).Prepend(task.WorktreePath).ToArray();
        if (!new HashSet<string>(roots, StringComparer.Ordinal).SetEquals(item.AuthorizedRoots))
            throw new ConversationFollowupException("FollowupScopeChanged");
        var context = new ToolExecutionContext(task.Id, task.WorktreePath ?? project.PrimaryDirectory, roots, item.ApprovalPolicy);
        paths.Resolve(context.WorkingDirectory, context);
        foreach (var root in roots) paths.Resolve(root, context);
        return project with { AuthorizedDirectories = project.AuthorizedDirectories.ToArray() };
    }
}
