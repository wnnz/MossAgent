namespace MossAgent.App.Tests;

internal sealed class AutomationDeadlineTestClock : TimeProvider
{
    private readonly List<AutomationDeadlineTestTimer> _timers = [];
    public TimeSpan LastDueTime { get; private set; }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        LastDueTime = dueTime;
        var timer = new AutomationDeadlineTestTimer(callback, state);
        _timers.Add(timer);
        return timer;
    }

    public void Expire() { foreach (var timer in _timers.ToArray()) timer.Fire(); }
}
