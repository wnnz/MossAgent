using System.Collections.Concurrent;

using MossAgent.App.Resources;
namespace MossAgent.App.ViewModels;

public sealed class WorkspaceRunTracker : IDisposable
{
    private readonly ConcurrentDictionary<Guid, WorkspaceRunState> _runs = [];
    private readonly object _gate = new();
    private readonly HashSet<Task> _executions = [];
    private bool _stopping;
    private bool _executionFailed;

    public bool IsRunning(Guid taskId) => _runs.ContainsKey(taskId);

    public bool TryGet(Guid taskId, out WorkspaceRunState? state) =>
        _runs.TryGetValue(taskId, out state);

    public IReadOnlyList<ChatMessageViewModel>? GetMessages(Guid taskId) =>
        _runs.TryGetValue(taskId, out var state) ? state.Messages : null;

    public void Add(WorkspaceRunState state)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_stopping, this);
            if (!_runs.TryAdd(state.Task.Id, state))
                throw new InvalidOperationException(UiText.T("该任务已经在运行。"));
        }
    }

    public void TrackExecution(Func<Task> execute)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_stopping, this);
            _executions.Add(completion.Task);
        }
        _ = ObserveExecutionAsync(execute, completion);
    }

    private async Task ObserveExecutionAsync(Func<Task> execute, TaskCompletionSource completion)
    {
        try
        {
            await execute();
            completion.SetResult();
        }
        catch (Exception exception)
        {
            lock (_gate) _executionFailed = true;
            completion.SetException(exception);
            _ = completion.Task.Exception;
        }
        finally { lock (_gate) _executions.Remove(completion.Task); }
    }

    public void CancelAll()
    {
        foreach (var state in _runs.Values)
        {
            try { state.Cancel(); }
            catch (Exception) { lock (_gate) _executionFailed = true; }
        }
    }

    public async Task StopAsync()
    {
        Task[] executions;
        lock (_gate)
        {
            _stopping = true;
            executions = _executions.ToArray();
        }
        CancelAll();
        await Task.WhenAll(executions);
        lock (_gate)
        {
            if (_executionFailed) throw new InvalidOperationException("Task finalization failed.");
        }
    }

    public void Cancel(Guid taskId)
    {
        if (_runs.TryGetValue(taskId, out var state))
        {
            state.Cancel();
        }
    }

    public void Remove(Guid taskId)
    {
        if (_runs.TryRemove(taskId, out var state))
        {
            state.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_gate) _stopping = true;
        CancelAll();
        foreach (var taskId in _runs.Keys)
        {
            if (_runs.TryRemove(taskId, out var state))
            {
                state.Dispose();
            }
        }
    }
}

