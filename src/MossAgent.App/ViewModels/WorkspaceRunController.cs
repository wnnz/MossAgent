using MossAgent.Application.Agent;
using MossAgent.Application.Conversations;
using MossAgent.Domain;
using MossAgent.App.Resources;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceRunController(
    WorkspaceRunExecutor executor,
    WorkspaceRunTracker runs,
    WorkspaceTaskFactory taskFactory)
{
    public event Func<WorkspaceRunState, bool, Task>? RunFinished;

    public async Task RunAsync(
        WorkspaceRunState state,
        WorkspaceTaskPanelCoordinator panels,
        Func<WorkspaceRunState, AgentEvent, Task> present,
        Action<WorkspaceRunState, string> setActivity,
        Action<AgentTask> applyTaskUpdate,
        Action runStateChanged)
    {
        var released = false;
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
            state.AuthorizationChanged = IsAuthorizationChange(exception);
            var code = TaskFailureClassifier.CodeFromException(exception);
            var reason = TaskFailureClassifier.FromCode(code);
            var message = exception switch
            {
                SourceReviewException source => PullRequestText.Failure(source.ErrorCode),
                _ => WorkspaceFailureText.Get(reason)
            };
            await present(state, new AgentFailureEvent(code, message));
            setActivity(state, message);
            await SetTerminalStatusAsync(state, AgentTaskStatus.Failed, applyTaskUpdate, reason);
        }
        finally
        {
            try
            {
                await panels.ReleaseBrowserAsync(state.Task.Id);
                released = true;
            }
            catch (Exception)
            {
                setActivity(state, "浏览器会话关闭失败，请重启应用释放资源。");
                throw new InvalidOperationException("Browser release failed.");
            }
            finally
            {
                runs.Remove(state.Task.Id);
                try
                {
                    foreach (var callback in RunFinished?.GetInvocationList().Cast<Func<WorkspaceRunState, bool, Task>>() ?? [])
                        await callback(state, released);
                }
                finally { runStateChanged(); }
            }
        }
    }

    private static bool IsAuthorizationChange(Exception exception) => exception is
        ConversationSteeringException { Code: "SteeringScopeChanged" }
        or SourceReviewException { ErrorCode: "source_scope_denied" };

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
