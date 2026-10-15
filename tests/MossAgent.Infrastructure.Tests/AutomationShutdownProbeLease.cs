using MossAgent.Application.Automations;

namespace MossAgent.Infrastructure.Tests;

internal sealed class AutomationShutdownProbeLease : IAutomationLease
{
    private int _held;
    private int _releases;
    public bool IsHeld => Volatile.Read(ref _held) != 0;
    public int Releases => Volatile.Read(ref _releases);
    public bool TryAcquire() => Interlocked.CompareExchange(ref _held, 1, 0) == 0;
    public void Release()
    {
        if (Interlocked.Exchange(ref _held, 0) != 0) Interlocked.Increment(ref _releases);
    }
}
