using MossAgent.Browser;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class LazyBrowserSessionTests
{
    [Fact]
    public async Task DisposeDuringCreation_WaitsForAndClosesCreatedSession()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource<IBrowserSession>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var browser = new StubBrowserSession();
        var lazy = new LazyBrowserSession(_ =>
        {
            started.SetResult();
            return ready.Task;
        });
        var read = lazy.GetPageTextAsync(TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);

        var disposal = lazy.DisposeAsync().AsTask();
        Assert.False(disposal.IsCompleted);
        ready.SetResult(browser);
        await read;
        await disposal;

        Assert.True(browser.IsDisposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            lazy.GetPageTextAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentDisposal_WaitsForTheSameInnerCleanup()
    {
        var browser = new StubBrowserSession();
        var lazy = new LazyBrowserSession(_ => Task.FromResult<IBrowserSession>(browser));
        await lazy.GetPageTextAsync(TestContext.Current.CancellationToken);

        await Task.WhenAll(lazy.DisposeAsync().AsTask(), lazy.DisposeAsync().AsTask());

        Assert.True(browser.IsDisposed);
    }
}
