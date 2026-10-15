using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.App.Terminal;

internal sealed class TerminalScreenProjection(bool requiresSession, Action<Action> post,
    Action<int, TerminalFrame?> publish) : IDisposable
{
    private readonly object _sync = new();
    private readonly TerminalScreenStore _store = new(requiresSession);
    private readonly TerminalFrameBuffer _frames = new(post, publish);
    private readonly CancellationTokenSource _lifetime = new();
    private int _generation;
    private bool _dirty;
    private bool _working;
    private bool _disposed;

    public TerminalFrame? Attach(ToolExecutionContext? context, TerminalSessionSnapshot? snapshot, int generation)
    {
        lock (_sync)
        {
            if (_disposed) return null;
            _generation = generation;
            _frames.Attach(generation);
            _store.Attach(context, snapshot);
            return _store.Capture();
        }
    }

    public bool Append(TerminalOutputEventArgs output, TerminalSessionSnapshot? snapshot)
    {
        if (!_store.Append(output, snapshot)) return false;
        RequestCapture();
        return true;
    }

    public void ApplyState(TerminalSessionSnapshot snapshot)
    {
        if (_store.ApplyState(snapshot)) RequestCapture();
    }

    public bool IsCurrent(TerminalFrame frame) => _store.IsCurrent(frame);

    private void RequestCapture()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _dirty = true;
            if (_working) return;
            _working = true;
            _ = Task.Run(CaptureAsync);
        }
    }

    private async Task CaptureAsync()
    {
        try
        {
            while (true)
            {
                await Task.Delay(16, _lifetime.Token).ConfigureAwait(false);
                lock (_sync)
                {
                    if (_disposed) return;
                    _dirty = false;
                    _frames.Append(_generation, _store.Capture());
                }
                await Task.Delay(16, _lifetime.Token).ConfigureAwait(false);
                lock (_sync)
                {
                    if (_disposed || !_dirty) return;
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            lock (_sync)
            {
                _working = false;
                if (_disposed) _lifetime.Dispose();
                else if (_dirty) { _working = true; _ = Task.Run(CaptureAsync); }
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _lifetime.Cancel();
            if (!_working) _lifetime.Dispose();
            _frames.Dispose();
            _store.Dispose();
        }
    }
}
