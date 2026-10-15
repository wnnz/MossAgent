using System.Text;

namespace MossAgent.App.ViewModels;

internal sealed class TerminalOutputBuffer(Action<Action> post, Action<int, string, bool> publish) : IDisposable
{
    internal const int MaximumCharacters = 200_000;
    private readonly object _sync = new();
    private readonly StringBuilder _pending = new();
    private Guid? _taskId;
    private int _generation;
    private bool _posted;
    private bool _truncated;
    private bool _disposed;

    public void Attach(Guid? taskId, int generation)
    {
        lock (_sync)
        {
            _taskId = taskId;
            _generation = generation;
            _pending.Clear();
            _truncated = false;
        }
    }

    public void Append(Guid taskId, string text)
    {
        lock (_sync)
        {
            if (_disposed || _taskId != taskId || text.Length == 0) return;
            var incoming = text.AsSpan();
            var cut = false;
            if (incoming.Length >= MaximumCharacters)
            {
                _truncated |= _pending.Length > 0 || incoming.Length > MaximumCharacters;
                cut = _pending.Length > 0 || incoming.Length > MaximumCharacters;
                _pending.Clear();
                incoming = incoming[^MaximumCharacters..];
            }
            var overflow = _pending.Length + incoming.Length - MaximumCharacters;
            if (overflow > 0) { _pending.Remove(0, overflow); _truncated = true; cut = true; }
            _pending.Append(incoming);
            if (cut && _pending.Length > 0 && char.IsLowSurrogate(_pending[0])) { _pending.Remove(0, 1); _truncated = true; }
            if (_posted) return;
            _posted = true;
        }
        try { post(Drain); }
        catch
        {
            lock (_sync) { _posted = false; _pending.Clear(); _truncated = true; }
        }
    }

    private void Drain()
    {
        string text;
        int generation;
        bool truncated;
        lock (_sync)
        {
            _posted = false;
            if (_disposed || _taskId is null || _pending.Length == 0) return;
            text = _pending.ToString();
            generation = _generation;
            truncated = _truncated;
            _pending.Clear();
            _truncated = false;
        }
        publish(generation, text, truncated);
    }

    public void Dispose()
    {
        lock (_sync) { _disposed = true; _taskId = null; _pending.Clear(); }
    }
}
