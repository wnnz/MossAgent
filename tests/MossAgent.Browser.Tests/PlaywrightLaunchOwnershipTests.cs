using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class PlaywrightLaunchOwnershipTests
{
    [Theory]
    [InlineData(BrowserProfileMode.Managed)]
    [InlineData(BrowserProfileMode.DailyProfile)]
    [InlineData(BrowserProfileMode.ExistingCdp)]
    public async Task CancelledBrowserHandoff_WaitsForOwnedOperationAndPreservesBorrowedBrowser(BrowserProfileMode mode)
    {
        using var probe = new PlaywrightOwnershipProbe();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = probe.Factory.CreateAsync(probe.Options(mode), cancellation.Token);
        cancellation.Cancel();
        await Task.Delay(20, TestContext.Current.CancellationToken);
        var returnedEarly = pending.IsCompleted;
        var disposedEarly = probe.RuntimeDisposeCount;
        probe.CompleteBrowser(mode);

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.False(returnedEarly);
        Assert.Equal(0, disposedEarly);
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(1, probe.RuntimeDisposeCount);
        Assert.Equal(mode == BrowserProfileMode.ExistingCdp ? 0 : 1, probe.ContextCloseCount);
        Assert.Equal(0, probe.BrowserCloseCount);
        Assert.Equal(0, probe.ContextReadCount);
        Assert.Equal(0, probe.PagesReadCount);
    }

    [Theory]
    [InlineData(BrowserProfileMode.Managed)]
    [InlineData(BrowserProfileMode.ExistingCdp)]
    public async Task CancelledSdkFailure_IsObservedAndKeepsCancellationIdentity(BrowserProfileMode mode)
    {
        using var probe = new PlaywrightOwnershipProbe();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = probe.Factory.CreateAsync(probe.Options(mode), cancellation.Token);
        cancellation.Cancel();
        var failure = new IOException("Synthetic browser handoff failure.");
        if (mode == BrowserProfileMode.ExistingCdp)
            probe.Connection.SetException(failure);
        else
            probe.Launch.SetException(failure);

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(1, probe.RuntimeDisposeCount);
        Assert.Equal(0, probe.ContextCloseCount);
        Assert.Equal(0, probe.BrowserCloseCount);
    }

    [Theory]
    [InlineData(BrowserProfileMode.Managed)]
    [InlineData(BrowserProfileMode.ExistingCdp)]
    public async Task CancelledPageHandoff_ReclaimsOnlyOwnedResources(BrowserProfileMode mode)
    {
        using var probe = new PlaywrightOwnershipProbe { HasPages = false };
        probe.CompleteBrowser(mode);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = probe.Factory.CreateAsync(probe.Options(mode), cancellation.Token);
        await probe.PageStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await Task.Delay(20, TestContext.Current.CancellationToken);
        var returnedEarly = pending.IsCompleted;
        var disposedEarly = probe.RuntimeDisposeCount;
        probe.PageCreation.SetResult(probe.Page);

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.False(returnedEarly);
        Assert.Equal(0, disposedEarly);
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(1, probe.RuntimeDisposeCount);
        Assert.Equal(mode == BrowserProfileMode.ExistingCdp ? 0 : 1, probe.ContextCloseCount);
        Assert.Equal(mode == BrowserProfileMode.ExistingCdp ? 1 : 0, probe.PageCloseCount);
        Assert.Equal(0, probe.BrowserCloseCount);
    }

    [Fact]
    public async Task OwnedPageInitializationFailure_ClosesContextBeforeReleasingRuntime()
    {
        using var probe = new PlaywrightOwnershipProbe { HasPages = false };
        probe.Launch.SetResult(probe.Context);
        var failure = new IOException("Synthetic page creation failure.");
        probe.PageCreation.SetException(failure);

        var error = await Assert.ThrowsAsync<IOException>(() =>
            probe.Factory.CreateAsync(probe.Options(BrowserProfileMode.Managed), TestContext.Current.CancellationToken));

        Assert.Same(failure, error);
        Assert.Equal(1, probe.ContextCloseCount);
        Assert.Equal(1, probe.RuntimeDisposeCount);
    }

    [Fact]
    public async Task SuccessfulOwnedLaunch_TransfersRuntimeUntilSessionDisposal()
    {
        using var probe = new PlaywrightOwnershipProbe();
        probe.Launch.SetResult(probe.Context);
        var session = await probe.Factory.CreateAsync(probe.Options(BrowserProfileMode.Managed), TestContext.Current.CancellationToken);
        Assert.Equal(0, probe.RuntimeDisposeCount);
        Assert.Equal(0, probe.ContextCloseCount);

        await session.DisposeAsync();

        Assert.Equal(1, probe.RuntimeDisposeCount);
        Assert.Equal(1, probe.ContextCloseCount);
    }

    [Fact]
    public async Task CancelledOwnedLaunch_WaitsForContextCleanupBeforeReleasingRuntime()
    {
        var closing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var probe = new PlaywrightOwnershipProbe { ContextCloseTask = closing.Task };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = probe.Factory.CreateAsync(probe.Options(BrowserProfileMode.Managed), cancellation.Token);
        cancellation.Cancel();
        probe.Launch.SetResult(probe.Context);
        await probe.ContextCloseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var returnedEarly = pending.IsCompleted;
        var disposedEarly = probe.RuntimeDisposeCount;
        closing.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.False(returnedEarly);
        Assert.Equal(0, disposedEarly);
        Assert.Equal(1, probe.ContextCloseCount);
        Assert.Equal(1, probe.RuntimeDisposeCount);
    }

    [Fact]
    public async Task SuccessfulCdpConnection_DoesNotCloseExistingBrowserContextOrPage()
    {
        using var probe = new PlaywrightOwnershipProbe();
        probe.Connection.SetResult(probe.Browser);
        var session = await probe.Factory.CreateAsync(probe.Options(BrowserProfileMode.ExistingCdp), TestContext.Current.CancellationToken);
        Assert.Equal(0, probe.RuntimeDisposeCount);

        await session.DisposeAsync();

        Assert.Equal(1, probe.RuntimeDisposeCount);
        Assert.Equal(0, probe.ContextCloseCount);
        Assert.Equal(0, probe.PageCloseCount);
        Assert.Equal(0, probe.BrowserCloseCount);
    }

    [Theory]
    [InlineData(BrowserProfileMode.Managed)]
    [InlineData(BrowserProfileMode.ExistingCdp)]
    public async Task SdkFailureWithoutCancellation_PreservesFailureAndReleasesRuntime(BrowserProfileMode mode)
    {
        using var probe = new PlaywrightOwnershipProbe();
        var failure = new IOException("Synthetic browser initialization failure.");
        if (mode == BrowserProfileMode.ExistingCdp)
            probe.Connection.SetException(failure);
        else
            probe.Launch.SetException(failure);

        var error = await Assert.ThrowsAsync<IOException>(() =>
            probe.Factory.CreateAsync(probe.Options(mode), TestContext.Current.CancellationToken));

        Assert.Same(failure, error);
        Assert.Equal(1, probe.RuntimeDisposeCount);
        Assert.Equal(0, probe.ContextCloseCount);
        Assert.Equal(0, probe.BrowserCloseCount);
    }
}
