using MossAgent.App.Browser;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class EmbeddedBrowserOperationQueueTests
{
    [Fact]
    public async Task Operations_AreSerializedAndPreserveTheirResults()
    {
        var queue = new EmbeddedBrowserOperationQueue();
        var release = Completion();
        var first = queue.RunAsync(async () => { await release.Task; return "first"; }, Token);
        var invoked = false;
        var second = queue.RunAsync(() => { invoked = true; return Task.FromResult("second"); }, Token);
        Assert.False(invoked);
        release.SetResult();

        Assert.Equal("first", await first.WaitAsync(TimeSpan.FromSeconds(3), Token));
        Assert.Equal("second", await second.WaitAsync(TimeSpan.FromSeconds(3), Token));
        Assert.True(invoked);
    }

    [Fact]
    public async Task QueuedCancellation_DoesNotInvokeActionOrReleaseOtherOperation()
    {
        var queue = new EmbeddedBrowserOperationQueue();
        var release = Completion();
        var first = queue.RunAsync(() => release.Task, Token);
        using var cancelled = new CancellationTokenSource();
        var invoked = false;
        var second = queue.RunAsync(() => { invoked = true; return Task.CompletedTask; }, cancelled.Token);
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        Assert.False(invoked);
        Assert.False(first.IsCompleted);
        release.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(3), Token);
    }

    [Fact]
    public async Task FailedOperation_ReleasesQueueForNextOperation()
    {
        var queue = new EmbeddedBrowserOperationQueue();
        await Assert.ThrowsAsync<IOException>(() => queue.RunAsync(
            () => Task.FromException(new IOException("fixture")), Token));

        Assert.Equal(42, await queue.RunAsync(() => Task.FromResult(42), Token));
    }

    [Fact]
    public async Task Disposal_WaitsForActiveOperationAndRejectsQueuedWork()
    {
        var queue = new EmbeddedBrowserOperationQueue();
        var release = Completion();
        var first = queue.RunAsync(() => release.Task, Token);
        var invoked = false;
        var second = queue.RunAsync(() => { invoked = true; return Task.CompletedTask; }, Token);
        var disposed = false;
        var disposal = queue.DisposeAsync(() => { disposed = true; return Task.CompletedTask; }).AsTask();
        Assert.False(disposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => queue.RunAsync(() => Task.CompletedTask, Token));
        release.SetResult();

        await first.WaitAsync(TimeSpan.FromSeconds(3), Token);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => second);
        await disposal.WaitAsync(TimeSpan.FromSeconds(3), Token);
        Assert.True(disposed);
        Assert.False(invoked);
    }

    [Fact]
    public async Task ConcurrentDisposal_WaitsForSameCleanupExactlyOnce()
    {
        var queue = new EmbeddedBrowserOperationQueue();
        var release = Completion();
        var count = 0;
        var first = queue.DisposeAsync(() => { count++; return release.Task; }).AsTask();
        var second = queue.DisposeAsync(() => { count++; return Task.CompletedTask; }).AsTask();
        Assert.False(second.IsCompleted);
        release.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(3), Token);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task FailedDisposal_DoesNotAllowSessionReuse()
    {
        var queue = new EmbeddedBrowserOperationQueue();
        await Assert.ThrowsAsync<IOException>(() => queue.DisposeAsync(
            () => Task.FromException(new IOException("fixture"))).AsTask());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => queue.RunAsync(() => Task.CompletedTask, Token));
        await Assert.ThrowsAsync<IOException>(() => queue.DisposeAsync(() => Task.CompletedTask).AsTask());
    }

    [Fact]
    public async Task PreCancelledOperation_NeverInvokesAction()
    {
        var queue = new EmbeddedBrowserOperationQueue();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var invoked = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queue.RunAsync(
            () => { invoked = true; return Task.CompletedTask; }, cancelled.Token));

        Assert.False(invoked);
    }

    [Fact]
    public async Task DifferentSessionQueues_DoNotBlockEachOther()
    {
        var release = Completion();
        var first = new EmbeddedBrowserOperationQueue().RunAsync(() => release.Task, Token);
        var other = new EmbeddedBrowserOperationQueue();
        Assert.Equal("other", await other.RunAsync(() => Task.FromResult("other"), Token));
        Assert.False(first.IsCompleted);
        release.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(3), Token);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static TaskCompletionSource Completion() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
