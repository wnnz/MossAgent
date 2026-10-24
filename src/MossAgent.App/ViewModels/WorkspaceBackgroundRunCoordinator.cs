using System.Collections.ObjectModel;
using MossAgent.App.Browser;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;

using MossAgent.App.Resources;
namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceBackgroundRunCoordinator(
    WorkspaceTaskFactory tasks,
    WorkspaceRunTracker runs,
    WorkspaceRunController controller,
    WorkspaceTaskPanelCoordinator panels,
    WorkspaceCatalogViewModel catalog,
    Func<bool> isReady,
    Func<WorkspaceRunState, AgentEvent, Task> presentEvent,
    Action<WorkspaceRunState, string> setActivity,
    Action<AgentTask> applyTaskUpdate,
    Action notifyRunState,
    Action<Guid, ConversationModelPreference?> rememberPreference)
{
    public async Task<AgentTask> RunAsync(ProjectProfile project, AiProvider provider,
        ModelProfile model, AutomationSchedule schedule, Guid taskId, CancellationToken cancellationToken)
    {
        await AvaloniaUiThread.RunAsync(() =>
        {
            if (!isReady()) throw new InvalidOperationException("Workspace is not ready for automation.");
        }, cancellationToken);
        var task = await tasks.CreateAsync(project, schedule.Prompt, schedule.ApprovalPolicy,
            false, cancellationToken, taskId);
        var completion = new TaskCompletionSource<AgentTask>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (!isReady()) throw new OperationCanceledException(cancellationToken);
                var assistant = new ChatMessageViewModel("MossAgent", string.Empty);
                var messages = new ObservableCollection<ChatMessageViewModel> { new(UiText.T("你"), schedule.Prompt), assistant };
                var state = new WorkspaceRunState(task, project, provider, model, messages, assistant, string.Empty);
                runs.Add(state);
                rememberPreference(task.Id, new(provider.Id, model.Id, ModelReasoningPolicy.Normalize(model.ReasoningEffort)));
                catalog.UpsertTask(task, select: false);
                runs.TrackExecution(async () =>
                {
                    using var registration = cancellationToken.Register(state.Cancel);
                    try
                    {
                        await controller.RunAsync(state, panels, presentEvent, setActivity, applyTaskUpdate, notifyRunState);
                        completion.TrySetResult(state.Task);
                    }
                    catch (Exception exception) { completion.TrySetException(exception); throw; }
                });
                notifyRunState();
            }, CancellationToken.None);
        }
        catch (Exception)
        {
            await tasks.SetStatusAsync(task, AgentTaskStatus.Cancelled, CancellationToken.None);
            throw;
        }
        return await completion.Task;
    }
}

