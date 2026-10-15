namespace MossAgent.App.Tests;

internal sealed class AutomationDeadlineTestTimer(TimerCallback callback, object? state) : ITimer
{
    private bool _disposed;
    public bool Change(TimeSpan dueTime, TimeSpan period) => !_disposed;
    public void Fire() { if (!_disposed) callback(state); }
    public void Dispose() => _disposed = true;
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
}
