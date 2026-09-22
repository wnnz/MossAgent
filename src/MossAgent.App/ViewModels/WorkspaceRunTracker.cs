using System.Collections.Concurrent;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceRunTracker : IDisposable
{
    private readonly ConcurrentDictionary<Guid, WorkspaceRunState> _runs = [];

    public bool IsRunning(Guid taskId) => _runs.ContainsKey(taskId);

    public bool TryGet(Guid taskId, out WorkspaceRunState? state) =>
        _runs.TryGetValue(taskId, out state);

    public IReadOnlyList<ChatMessageViewModel>? GetMessages(Guid taskId) =>
        _runs.TryGetValue(taskId, out var state) ? state.Messages : null;

    public void Add(WorkspaceRunState state)
    {
        if (!_runs.TryAdd(state.Task.Id, state))
        {
            throw new InvalidOperationException("该任务已经在运行。");
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
        foreach (var taskId in _runs.Keys)
        {
            if (_runs.TryRemove(taskId, out var state))
            {
                state.Cancel();
                state.Dispose();
            }
        }
    }
}
