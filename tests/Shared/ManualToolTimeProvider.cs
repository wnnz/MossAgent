namespace MossAgent.Testing;

internal sealed class ManualToolTimeProvider : TimeProvider
{
    private readonly List<OneShotToolTestTimer> _timers = [];
    private long _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Interlocked.Read(ref _timestamp);
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new OneShotToolTestTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_timers) _timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan duration, bool dispatchTimers = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        Interlocked.Add(ref _timestamp, duration.Ticks);
        if (dispatchTimers) DispatchTimers();
    }

    public void DispatchTimers()
    {
        OneShotToolTestTimer[] timers;
        lock (_timers) timers = [.. _timers];
        foreach (var timer in timers) timer.FireDue();
    }

    public void Remove(OneShotToolTestTimer timer)
    {
        lock (_timers) _timers.Remove(timer);
    }
}
