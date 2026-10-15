namespace MossAgent.App.Tests;

internal sealed class FilePreviewPickerTimer : ITimer
{
    private readonly FilePreviewPickerClock _clock;
    private readonly TimerCallback _callback;
    private readonly object? _state;
    private readonly object _gate = new();
    private DateTimeOffset? _due;
    private TimeSpan _period;
    private bool _disposed;

    public FilePreviewPickerTimer(FilePreviewPickerClock clock, TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    { _clock = clock; _callback = callback; _state = state; Change(dueTime, period); }
    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            _due = dueTime == Timeout.InfiniteTimeSpan ? null : _clock.GetUtcNow() + dueTime;
            _period = period;
            return true;
        }
    }
    public void FireIfDue(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_disposed || _due is null || _due > now) return;
            _due = _period == Timeout.InfiniteTimeSpan ? null : now + _period;
        }
        _callback(_state);
    }
    public void Dispose() { lock (_gate) { _disposed = true; _due = null; } }
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
}
