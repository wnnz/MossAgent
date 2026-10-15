namespace MossAgent.App.Terminal;

internal sealed class TerminalFrameBuffer(Action<Action> post, Action<int, TerminalFrame?> publish) : IDisposable
{
    private readonly object _sync = new();
    private TerminalFrame? _pending;
    private int _generation;
    private bool _dirty;
    private bool _posted;
    private bool _disposed;

    public void Attach(int generation)
    {
        lock (_sync) { _generation = generation; _pending = null; _dirty = false; }
    }

    public void Append(int generation, TerminalFrame? frame)
    {
        lock (_sync)
        {
            if (_disposed || generation != _generation) return;
            _pending = frame;
            _dirty = true;
            if (_posted) return;
            _posted = true;
        }
        try { post(Drain); }
        catch { lock (_sync) _posted = false; }
    }

    private void Drain()
    {
        TerminalFrame? frame;
        int generation;
        lock (_sync)
        {
            _posted = false;
            if (_disposed || !_dirty) return;
            _dirty = false;
            frame = _pending;
            _pending = null;
            generation = _generation;
        }
        publish(generation, frame);
    }

    public void Dispose()
    {
        lock (_sync) { _disposed = true; _dirty = false; _pending = null; }
    }
}
