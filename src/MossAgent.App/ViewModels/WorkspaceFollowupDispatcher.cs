using System.Collections.ObjectModel;
using MossAgent.App.Services;
using MossAgent.App.Browser;
using MossAgent.Application.Conversations;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceFollowupDispatcher(
    IConversationFollowupRepository repository,
    ConversationFollowupPreparer preparer,
    WorkspaceTaskFactory tasks,
    WorkspaceRunTracker runs,
    WorkspaceRunController controller,
    WorkspaceTaskPanelCoordinator panels,
    WorkspaceCatalogViewModel catalog,
    WorkspaceRunPresentation presentation,
    ConversationAttachmentThumbnailFactory? thumbnails)
{
    public async Task DispatchAsync(Guid taskId, Action<WorkspaceRunState> arm, CancellationToken cancellationToken)
    {
        var queue = await repository.GetAsync(taskId, cancellationToken);
        if (queue.Items.Count == 0) return;
        var prepared = await preparer.PrepareAsync(queue, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var dispatch = await repository.DispatchAsync(prepared, cancellationToken)
            ?? throw new ConversationFollowupException("FollowupConflict");
        WorkspaceRunState? state = null;
        try
        {
            var history = await tasks.GetMessagesAsync(taskId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await AvaloniaUiThread.RunAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var assistant = new ChatMessageViewModel("MossAgent", string.Empty);
                var messages = new ObservableCollection<ChatMessageViewModel>(history.Select(message =>
                    WorkspaceConversationMapper.ToChatMessage(message, thumbnails))) { assistant };
                state = new(dispatch.Task, prepared.Project, prepared.Provider, prepared.Model, messages, assistant,
                    agentInstructions: prepared.Followup.AgentInstructions);
                runs.Add(state);
                catalog.UpsertTask(state.Task, select: catalog.SelectedTask?.Id == taskId);
                catalog.ShowActiveMessages(taskId);
                if (catalog.SelectedTask?.Id == taskId) panels.Attach(state.Task, prepared.Project);
                cancellationToken.ThrowIfCancellationRequested();
                presentation.RunStateChanged();
                arm(state);
                runs.TrackExecution(() => controller.RunAsync(state, panels, presentation.PresentEvent,
                    presentation.SetActivity, presentation.ApplyTaskUpdate, presentation.RunStateChanged));
            }, CancellationToken.None);
        }
        catch (Exception)
        {
            state?.Cancel();
            if (state is not null && runs.TryGet(taskId, out var active) && ReferenceEquals(state, active)) runs.Remove(taskId);
            else state?.Dispose();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var status = cancellationToken.IsCancellationRequested ? AgentTaskStatus.Cancelled : AgentTaskStatus.Failed;
            var stopped = await tasks.SetStatusAsync(dispatch.Task, status, deadline.Token);
            await AvaloniaUiThread.RunAsync(async () =>
            {
                catalog.UpsertTask(stopped, select: catalog.SelectedTask?.Id == taskId);
                if (catalog.SelectedTask?.Id == taskId) await catalog.LoadMessagesAsync(stopped);
            }, CancellationToken.None);
            throw new ConversationFollowupException("FollowupCommittedFailed");
        }
    }
}
