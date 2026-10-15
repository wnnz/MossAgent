namespace MossAgent.App.Tests;

internal sealed class FilePreviewPickerClock : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<FilePreviewPickerTimer> _timers = [];
    private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
    public TimeSpan LastDueTime { get; private set; }
    public override DateTimeOffset GetUtcNow() { lock (_gate) return _now; }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FilePreviewPickerTimer(this, callback, state, dueTime, period);
        lock (_gate) { LastDueTime = dueTime; _timers.Add(timer); }
        return timer;
    }

    public void Advance(TimeSpan elapsed)
    {
        FilePreviewPickerTimer[] timers;
        DateTimeOffset now;
        lock (_gate) { now = _now += elapsed; timers = _timers.ToArray(); }
        foreach (var timer in timers) timer.FireIfDue(now);
    }
}
