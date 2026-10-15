using Microsoft.Playwright;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class PlaywrightInitializationCancellationTests
{
    [Theory]
    [InlineData(BrowserProfileMode.Managed)]
    [InlineData(BrowserProfileMode.DailyProfile)]
    [InlineData(BrowserProfileMode.ExistingCdp)]
    public async Task AlreadyCancelled_DoesNotStartRuntimeOrResolveEndpoint(BrowserProfileMode mode)
    {
        var creations = 0;
        var factory = new PlaywrightBrowserSessionFactory(() =>
        {
            creations++;
            return Task.FromException<IPlaywright>(new InvalidOperationException("Unexpected runtime creation."));
        });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            factory.CreateAsync(Options(mode), cancellation.Token));

        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(0, creations);
    }

    [Fact]
    public async Task CancellationDuringInitialization_OwnsAndDisposesLateRuntimeBeforeReturning()
    {
        var initialization = new TaskCompletionSource<IPlaywright>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new TrackingPlaywrightRuntime();
        var factory = new PlaywrightBrowserSessionFactory(() => initialization.Task);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = factory.CreateAsync(Options(), cancellation.Token);
        cancellation.Cancel();
        var returnedBeforeInitialization = pending.IsCompleted;
        initialization.SetResult(runtime);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.False(returnedBeforeInitialization);
        Assert.Equal(1, runtime.DisposeCount);
        Assert.Equal(0, runtime.BrowserAccessCount);
    }

    [Fact]
    public async Task CancellationAtRuntimeHandoff_DisposesBeforeAnyBrowserAccess()
    {
        var runtime = new TrackingPlaywrightRuntime();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var factory = new PlaywrightBrowserSessionFactory(() =>
        {
            cancellation.Cancel();
            return Task.FromResult<IPlaywright>(runtime);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => factory.CreateAsync(Options(), cancellation.Token));

        Assert.Equal(1, runtime.DisposeCount);
        Assert.Equal(0, runtime.BrowserAccessCount);
    }

    [Fact]
    public async Task InitializationFailure_IsObservedWithoutAttemptingBrowserAccess()
    {
        var failure = new InvalidOperationException("Synthetic initialization failure.");
        var factory = new PlaywrightBrowserSessionFactory(() => Task.FromException<IPlaywright>(failure));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            factory.CreateAsync(Options(), TestContext.Current.CancellationToken));

        Assert.Same(failure, error);
    }

    [Fact]
    public async Task CancelledInitializationFailure_IsObservedAndKeepsCancellationIdentity()
    {
        var initialization = new TaskCompletionSource<IPlaywright>(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new PlaywrightBrowserSessionFactory(() => initialization.Task);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = factory.CreateAsync(Options(), cancellation.Token);
        cancellation.Cancel();
        initialization.SetException(new IOException("Synthetic driver handshake failure."));

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.True(initialization.Task.IsFaulted);
    }

    private static BrowserLaunchOptions Options(BrowserProfileMode mode = BrowserProfileMode.Managed) =>
        new(BrowserEngine.Chrome, mode, "synthetic-profile", null, null, null, null);
}
