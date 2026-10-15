namespace MossAgent.Testing;

internal sealed class OneShotToolTestTimer(ManualToolTimeProvider clock, TimerCallback callback, object? state) : ITimer
{
    private readonly object _sync = new();
    private long _due = long.MaxValue;
    private bool _disposed;

    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        if (period != Timeout.InfiniteTimeSpan) throw new NotSupportedException();
        if (dueTime < TimeSpan.Zero && dueTime != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(dueTime));
        lock (_sync)
        {
            if (_disposed) return false;
            _due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock.GetTimestamp() + dueTime.Ticks;
            return true;
        }
    }

    public void FireDue()
    {
        lock (_sync)
        {
            if (_disposed || clock.GetTimestamp() < _due) return;
            _due = long.MaxValue;
        }
        callback(state);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
        }
        clock.Remove(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
