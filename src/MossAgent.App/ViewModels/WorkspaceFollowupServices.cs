using MossAgent.App.Services;
using MossAgent.Application.Conversations;
using MossAgent.Application.Persistence;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceFollowupServices(IConversationFollowupRepository repository,
    ConversationFollowupPreparer preparer, ConversationAttachmentThumbnailFactory thumbnails)
{
    internal WorkspaceFollowupQueueViewModel Create(WorkspaceViewModel workspace, WorkspaceTaskFactory tasks,
        WorkspaceRunTracker runs, WorkspaceRunController controller, WorkspaceTaskPanelCoordinator panels,
        WorkspaceRunPresentation presentation, Func<bool> canEnqueue, Func<bool> canContinue)
    {
        WorkspaceFollowupRuntime? runtime = workspace.Drafts.CanQueueTo(repository)
            ? new(repository, new(tasks, repository, workspace.Drafts, workspace.Attachments),
                new(repository, preparer, tasks, runs, controller, panels, workspace.Catalog, presentation, thumbnails), runs, controller)
            : null;
        return new(runtime, workspace.Catalog, () => WorkspaceSendRequest.Capture(workspace), canEnqueue, canContinue,
            () => workspace.HasActiveRun, workspace.PrimaryActionCommand);
    }
}
