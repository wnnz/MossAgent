namespace MossAgent.Tools.Tests;

internal sealed class FileChangeRestoreLateTimeProvider(Action? onCompletion = null) : TimeProvider
{
    private int _timestampRequests;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp()
    {
        if (Interlocked.Increment(ref _timestampRequests) < 3) return 0;
        onCompletion?.Invoke();
        return TimeSpan.FromDays(2).Ticks;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        System.CreateTimer(callback, state, dueTime, period);
}
