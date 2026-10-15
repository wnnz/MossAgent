namespace MossAgent.Infrastructure.Tests;

internal sealed class AutomationTestClock : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<AutomationTestTimer> _timers = [];
    private DateTimeOffset _now = DateTimeOffset.Parse("2026-10-08T00:00:00Z");
    public override DateTimeOffset GetUtcNow() { lock (_gate) return _now; }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new AutomationTestTimer(this, callback, state, dueTime, period);
        lock (_gate) _timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan elapsed)
    {
        AutomationTestTimer[] timers;
        DateTimeOffset now;
        lock (_gate) { now = _now += elapsed; timers = _timers.ToArray(); }
        foreach (var timer in timers) timer.FireIfDue(now);
    }
}
