using Xunit;

namespace MossAgent.App.Tests;

internal static class CancellationObservedSnapshot
{
    public static IReadOnlyList<TItem> Read<TItem>(CancellationToken token, IReadOnlyList<TItem> items)
    {
        using var observed = new ManualResetEventSlim(token.IsCancellationRequested);
        using var registration = token.Register(observed.Set);
        if (!observed.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
            throw new TimeoutException("The repository read did not receive cancellation.");
        return items;
    }
}
