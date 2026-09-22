using MossAgent.Application.Agent;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceRunController(
    WorkspaceRunExecutor executor,
    WorkspaceRunTracker runs,
    WorkspaceTaskFactory taskFactory)
{
    public async Task RunAsync(
        WorkspaceRunState state,
        WorkspaceTaskPanelCoordinator panels,
        Func<WorkspaceRunState, AgentEvent, Task> present,
        Action<WorkspaceRunState, string> setActivity,
        Action<AgentTask> applyTaskUpdate,
        Action runStateChanged)
    {
        try
        {
            var context = panels.CreateContext(state.Task, state.Project);
            state.Task = await executor.ExecuteAsync(
                state,
                context,
                agentEvent => present(state, agentEvent),
                state.CancellationToken);
            applyTaskUpdate(state.Task);
        }
        catch (OperationCanceledException)
        {
            setActivity(state, "任务已取消。");
            await SetTerminalStatusAsync(state, AgentTaskStatus.Cancelled, applyTaskUpdate);
        }
        catch (Exception exception)
        {
            setActivity(state, $"任务失败：{exception.Message}");
            await SetTerminalStatusAsync(state, AgentTaskStatus.Failed, applyTaskUpdate);
        }
        finally
        {
            runs.Remove(state.Task.Id);
            runStateChanged();
        }
    }

    private async Task SetTerminalStatusAsync(
        WorkspaceRunState state,
        AgentTaskStatus status,
        Action<AgentTask> applyTaskUpdate)
    {
        state.Task = await taskFactory.SetStatusAsync(
            state.Task, status, CancellationToken.None);
        applyTaskUpdate(state.Task);
    }
}
