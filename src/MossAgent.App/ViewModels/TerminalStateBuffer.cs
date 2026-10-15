using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.App.ViewModels;

internal sealed class TerminalStateBuffer(Action<Action> post, Action<int, TerminalSessionSnapshot> publish) : IDisposable
{
    private readonly object _sync = new();
    private Guid? _taskId;
    private int _generation;
    private TerminalSessionSnapshot? _pending;
    private bool _posted;
    private bool _disposed;

    public void Attach(Guid? taskId, int generation)
    {
        lock (_sync) { _taskId = taskId; _generation = generation; _pending = null; }
    }

    public void Append(TerminalSessionSnapshot snapshot)
    {
        lock (_sync)
        {
            if (_disposed || _taskId != snapshot.TaskId) return;
            if (_pending is null || _pending.Revision < snapshot.Revision) _pending = snapshot;
            if (_posted) return;
            _posted = true;
        }
        try { post(Drain); }
        catch { lock (_sync) _posted = false; }
    }

    private void Drain()
    {
        TerminalSessionSnapshot? snapshot;
        int generation;
        lock (_sync)
        {
            _posted = false;
            if (_disposed || _taskId is null) return;
            snapshot = _pending;
            generation = _generation;
            _pending = null;
        }
        if (snapshot is not null) publish(generation, snapshot);
    }

    public void Dispose()
    {
        lock (_sync) { _disposed = true; _taskId = null; _pending = null; }
    }
}
