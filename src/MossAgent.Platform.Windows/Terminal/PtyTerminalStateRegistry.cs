using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.Platform.Windows.Terminal;

internal sealed class PtyTerminalStateRegistry
{
    internal const int MaximumCompletedSnapshots = 128;
    private readonly object _sync = new();
    private readonly Dictionary<Guid, TerminalSessionSnapshot> _snapshots = [];
    private readonly Queue<TerminalSessionSnapshot> _completed = [];
    private long _revision;

    public event EventHandler<TerminalStateEventArgs>? StateChanged;

    public TerminalSessionSnapshot? GetSnapshot(Guid taskId)
    {
        lock (_sync) return _snapshots.GetValueOrDefault(taskId);
    }

    public TerminalSessionSnapshot Record(Guid taskId, Guid sessionId, TerminalSessionState state, int? exitCode = null, TerminalGridSize? grid = null)
    {
        lock (_sync)
        {
            _snapshots.TryGetValue(taskId, out var current);
            grid ??= current?.SessionId == sessionId ? current.Grid : TerminalGridSize.Default;
            if (current?.SessionId == sessionId && current.State == state && current.ExitCode == exitCode && current.Grid == grid)
                return current;
            var snapshot = new TerminalSessionSnapshot(taskId, sessionId, ++_revision, state, exitCode, grid);
            _snapshots[taskId] = snapshot;
            if (state is TerminalSessionState.Stopped or TerminalSessionState.Exited or TerminalSessionState.Failed)
            {
                _completed.Enqueue(snapshot);
                while (_completed.Count > MaximumCompletedSnapshots)
                {
                    var previous = _completed.Dequeue();
                    if (_snapshots.GetValueOrDefault(previous.TaskId) == previous) _snapshots.Remove(previous.TaskId);
                }
            }
            return snapshot;
        }
    }

    public void Publish(TerminalSessionSnapshot? snapshot)
    {
        if (snapshot is null || GetSnapshot(snapshot.TaskId) != snapshot) return;
        var args = new TerminalStateEventArgs(snapshot);
        foreach (var subscriber in StateChanged?.GetInvocationList() ?? [])
        {
            try { ((EventHandler<TerminalStateEventArgs>)subscriber)(this, args); }
            catch (Exception) { }
        }
    }
}
