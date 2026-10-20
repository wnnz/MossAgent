namespace MossAgent.Infrastructure.Tests;

internal sealed class ExpiringDraftTimeProvider : TimeProvider
{
    private readonly ManualToolTimeProvider _clock = new();
    public TimeSpan? ObservedTimeout { get; private set; }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ObservedTimeout = dueTime;
        var timer = _clock.CreateTimer(callback, state, dueTime, period);
        _clock.Advance(dueTime);
        return timer;
    }
}
