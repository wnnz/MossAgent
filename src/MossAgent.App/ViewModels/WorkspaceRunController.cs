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
            await WorkspaceMessageTimestampPresenter.StampAsync(state.Assistant, state.Task.UpdatedAt);
            applyTaskUpdate(state.Task);
        }
        catch (OperationCanceledException) when (state.IsCancellationRequested)
        {
            await present(state, new AgentCancelledEvent());
            setActivity(state, WorkspaceText.Get("RunCancelled"));
            await SetTerminalStatusAsync(state, AgentTaskStatus.Cancelled, applyTaskUpdate);
        }
        catch (Exception exception)
        {
            var code = TaskFailureClassifier.CodeFromException(exception);
            var reason = TaskFailureClassifier.FromCode(code);
            var message = WorkspaceFailureText.Get(reason);
            await present(state, new AgentFailureEvent(code, message));
            setActivity(state, message);
            await SetTerminalStatusAsync(state, AgentTaskStatus.Failed, applyTaskUpdate, reason);
        }
        finally
        {
            try
            {
                await panels.ReleaseBrowserAsync(state.Task.Id);
            }
            catch (Exception)
            {
                setActivity(state, "浏览器会话关闭失败，请重启应用释放资源。");
                throw new InvalidOperationException("Browser release failed.");
            }
            finally
            {
                runs.Remove(state.Task.Id);
                runStateChanged();
            }
        }
    }

    private async Task SetTerminalStatusAsync(
        WorkspaceRunState state,
        AgentTaskStatus status,
        Action<AgentTask> applyTaskUpdate,
        TaskFailureReason? failureReason = null)
    {
        state.Task = await taskFactory.CompleteAsync(
            state.Task, state.PendingAssistantContent, status, CancellationToken.None, failureReason);
        state.MarkAssistantPersisted();
        await WorkspaceMessageTimestampPresenter.StampAsync(state.Assistant, state.Task.UpdatedAt);
        applyTaskUpdate(state.Task);
    }
}
