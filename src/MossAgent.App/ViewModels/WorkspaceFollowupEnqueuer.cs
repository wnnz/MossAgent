using MossAgent.Application.Attachments;
using MossAgent.Application.Conversations;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.App.Browser;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceFollowupEnqueuer(
    WorkspaceTaskFactory tasks,
    IConversationFollowupRepository repository,
    WorkspaceComposerDraftsViewModel drafts,
    WorkspaceAttachmentManagerViewModel attachments)
{
    public async Task<ConversationFollowupQueue> EnqueueAsync(WorkspaceSendRequest request, CancellationToken cancellationToken)
    {
        var task = request.ExistingTask ?? throw new ConversationFollowupException("FollowupTaskUnavailable");
        if (request.Scope != new ConversationDraftScope(task.ProjectId, task.Id) || task.ProjectId != request.Project?.Id)
            throw new ConversationFollowupException("FollowupScopeChanged");
        var contents = request.Attachments.Select(item => item.Content).ToArray();
        ConversationAttachmentValidation.ValidateModel(request.Model, request.Provider.Protocol, contents);
        var queue = await repository.GetAsync(task.Id, cancellationToken);
        if (queue.Items.Count >= ConversationFollowupQueue.MaximumItems) throw new ConversationFollowupException("FollowupFull");
        var id = Guid.NewGuid();
        var snapshots = await tasks.SnapshotAttachmentsAsync(task.Id, id, contents, cancellationToken);
        ConversationAttachmentValidation.Validate(task.Id, id, snapshots);
        if (snapshots.Count != contents.Length) throw new ConversationAttachmentUnavailableException();
        var item = new ConversationFollowup(id, task.Id, task.ProjectId, request.Draft.Trim(), request.Model,
            request.Preference?.AgentProfileId, request.AgentInstructions,
            request.Project is null ? ApprovalPolicy.ReadOnly : request.ApprovalPolicy,
            request.Project is null ? [] : tasks.GetAuthorizedRoots(request.Project, task).ToArray(), task.WorktreePath,
            snapshots.ToArray(), DateTimeOffset.UtcNow);
        var updated = queue with { Items = queue.Items.Append(item).ToArray() };
        var submission = drafts.CreateQueuedSubmission(repository, request.Scope, request.Draft,
            request.Attachments.Select(item => item.SelectionId).ToArray());
        await submission.SaveAsync(updated, cancellationToken);
        await AvaloniaUiThread.RunAsync(() => attachments.Consume(request.Attachments), CancellationToken.None);
        return updated with { Revision = checked(queue.Revision + 1) };
    }
}
