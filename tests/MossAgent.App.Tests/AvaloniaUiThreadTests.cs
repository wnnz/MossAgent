using MossAgent.App.Browser;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AvaloniaUiThreadTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task PreCancelledDispatch_DoesNotInvokeAction(int overload)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var invoked = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DispatchAsync(overload, () => invoked = true, cancellation.Token));

        Assert.False(invoked);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task WithoutApplication_DispatchDoesNotNeedUiPump(int overload)
    {
        var invoked = false;
        await DispatchAsync(overload, () => invoked = true, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

        Assert.True(invoked);
    }

    private static Task DispatchAsync(int overload, Action action, CancellationToken cancellationToken) =>
        overload switch
        {
            0 => AvaloniaUiThread.RunAsync(action, cancellationToken),
            1 => AvaloniaUiThread.RunAsync(() => { action(); return Task.CompletedTask; }, cancellationToken),
            2 => AvaloniaUiThread.RunAsync(() => { action(); return true; }, cancellationToken),
            _ => AvaloniaUiThread.RunAsync(() => { action(); return Task.FromResult(true); }, cancellationToken)
        };
}
