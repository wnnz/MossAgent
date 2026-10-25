using System.Collections.ObjectModel;
using MossAgent.Application.Attachments;
using MossAgent.Domain;

using MossAgent.App.Resources;
namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceSendCoordinator(
    WorkspaceTaskFactory tasks,
    WorkspaceRunTracker runs,
    WorkspaceRunController controller,
    WorkspaceTaskPanelCoordinator panels,
    WorkspaceCatalogViewModel catalog,
    WorkspaceAttachmentManagerViewModel attachments,
    WorkspaceComposerDraftsViewModel drafts,
    WorkspaceSendPreparation preparation,
    WorkspaceConversationPreferences? preferences,
    WorkspaceRunPresentation presentation,
    Func<bool> isDisposed,
    Func<bool> isShuttingDown,
    Action<string> setActivity)
{
    public async Task SendAsync(WorkspaceSendRequest request, CancellationToken cancellationToken)
    {
        var selected = request.Attachments;
        var prompt = request.Draft.Trim();
        ConversationAttachmentValidation.ValidateModel(request.Model, selected.Select(item => item.Content).ToArray());
        var attachmentIds = selected.Select(item => item.SelectionId).ToArray();
        var submission = drafts.CreateSubmission(tasks.Submissions, request.Scope, request.Draft, attachmentIds);
        var task = await tasks.PrepareAsync(request.Project, request.ExistingTask, prompt, request.ApprovalPolicy,
            selected.Select(item => item.Content).ToArray(), cancellationToken, request.UseWorktree, submission, request.WorktreeBaseReference);
        if (isShuttingDown() || isDisposed() || cancellationToken.IsCancellationRequested)
        {
            if (!isDisposed()) catalog.UpsertTask(task, select: false);
            task = await preparation.CancelPreparedTaskAsync(tasks, task);
            if (!isDisposed())
            {
                catalog.UpsertTask(task, select: request.ExistingTask is not null || submission is not null);
                setActivity(WorkspaceText.Get(submission is null ? "PreparationCancelled" : "PreparationCommittedCancelled"));
            }
            return;
        }
        preferences?.Remember(task.Id, request.Preference);
        if (submission is null) drafts.AcceptSubmission(request.Scope, task.Id, request.Draft, attachmentIds);
        StartRun(request, task, prompt);
    }

    private void StartRun(WorkspaceSendRequest request, AgentTask task, string prompt)
    {
        var messageAttachments = request.Attachments.Select(item => new ChatMessageAttachmentViewModel(
            item.DisplayPath, item.SizeLabel, item.Thumbnail, item.KindLabel)).ToArray();
        var assistant = new ChatMessageViewModel("MossAgent", string.Empty);
        var messages = new ObservableCollection<ChatMessageViewModel>(request.VisibleMessages)
            { new(UiText.T("你"), prompt, attachments: messageAttachments, createdAt: task.UpdatedAt), assistant };
        var state = new WorkspaceRunState(task, request.Project, request.Provider, request.Model, messages, assistant,
            agentInstructions: request.AgentInstructions);
        attachments.Consume(request.Attachments);
        runs.Add(state);
        catalog.UpsertTask(task);
        catalog.ShowActiveMessages(task.Id);
        panels.Attach(task, request.Project);
        runs.TrackExecution(() => controller.RunAsync(state, panels, presentation.PresentEvent,
            presentation.SetActivity, presentation.ApplyTaskUpdate, presentation.RunStateChanged));
    }
}

