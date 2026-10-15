using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.App.Terminal;

internal sealed class TerminalScreenStore(bool requiresSession) : IDisposable
{
    internal const int MaximumScreens = 8;
    internal const int MaximumCachedCells = 400_000;
    private readonly object _sync = new();
    private readonly Dictionary<Guid, TerminalScreenOwner> _owners = [];
    private Guid? _selected;
    private long _visits;
    private bool _disposed;

    public void Attach(ToolExecutionContext? context, TerminalSessionSnapshot? snapshot)
    {
        lock (_sync)
        {
            if (_disposed) return;
            _selected = context?.TaskId;
            if (context is null) return;
            if (_owners.TryGetValue(context.TaskId, out var owner) && !owner.Matches(context))
            {
                var blocked = owner.SessionId ?? snapshot?.SessionId;
                owner.Dispose();
                _owners[context.TaskId] = owner = new(context, blocked, !requiresSession);
            }
            if (owner is null)
            {
                if (_owners.Count == MaximumScreens)
                {
                    var oldest = _owners.MinBy(pair => pair.Value.LastVisit);
                    oldest.Value.Dispose();
                    _owners.Remove(oldest.Key);
                }
                _owners.Add(context.TaskId, owner = new(context));
            }
            owner.LastVisit = ++_visits;
            if (snapshot?.TaskId == context.TaskId) owner.ApplyState(snapshot);
            TrimBudget(context.TaskId);
        }
    }

    public bool ApplyState(TerminalSessionSnapshot snapshot)
    {
        lock (_sync)
        {
            if (_disposed || !_owners.TryGetValue(snapshot.TaskId, out var owner)) return false;
            var changed = owner.ApplyState(snapshot);
            TrimBudget(_selected ?? snapshot.TaskId);
            return changed;
        }
    }

    public bool Append(TerminalOutputEventArgs output, TerminalSessionSnapshot? snapshot = null)
    {
        lock (_sync)
        {
            if (_disposed || !_owners.TryGetValue(output.TaskId, out var owner)) return false;
            if (snapshot?.TaskId == output.TaskId) owner.ApplyState(snapshot);
            TrimBudget(_selected ?? output.TaskId);
            if (!_owners.ContainsKey(output.TaskId)) return false;
            return owner.Append(output, requiresSession);
        }
    }

    public TerminalFrame? Capture()
    {
        lock (_sync) return !_disposed && _selected is { } task && _owners.TryGetValue(task, out var owner)
            ? owner.Screen.Capture(owner.ViewId, task, owner.SessionId, owner.Failed) : null;
    }

    public bool IsCurrent(TerminalFrame frame)
    {
        lock (_sync) return !_disposed && _selected == frame.TaskId && _owners.TryGetValue(frame.TaskId, out var owner)
            && owner.ViewId == frame.ViewId && owner.SessionId == frame.SessionId;
    }

    private void TrimBudget(Guid protectedTask)
    {
        while (_owners.Values.Sum(owner => owner.Screen.EstimatedCells) > MaximumCachedCells && _owners.Count > 1)
        {
            var oldest = _owners.Where(pair => pair.Key != protectedTask).MinBy(pair => pair.Value.LastVisit);
            oldest.Value.Dispose();
            _owners.Remove(oldest.Key);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var owner in _owners.Values) owner.Dispose();
            _owners.Clear();
            _selected = null;
        }
    }
}
