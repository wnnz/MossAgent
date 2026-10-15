using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class PlaywrightSessionDisposalTests
{
    [Fact]
    public async Task FailedOwnedContextClose_StillDisposesRuntimeAndReportsFailure()
    {
        using var probe = new PlaywrightOwnershipProbe();
        var failure = new IOException("Synthetic context close failure.");
        probe.ContextCloseTask = Task.FromException(failure);
        var session = new PlaywrightBrowserSession(probe.Runtime, probe.Context, probe.Page, closeContext: true);

        var error = await Assert.ThrowsAsync<IOException>(() => session.DisposeAsync().AsTask());

        Assert.Same(failure, error);
        Assert.Equal(1, probe.ContextCloseCount);
        Assert.Equal(1, probe.RuntimeDisposeCount);
    }

    [Fact]
    public async Task BorrowedContextDisposal_OnlyDisconnectsRuntime()
    {
        using var probe = new PlaywrightOwnershipProbe();
        var session = new PlaywrightBrowserSession(probe.Runtime, probe.Context, probe.Page, closeContext: false);

        await session.DisposeAsync();

        Assert.Equal(0, probe.ContextCloseCount);
        Assert.Equal(0, probe.PageCloseCount);
        Assert.Equal(0, probe.BrowserCloseCount);
        Assert.Equal(1, probe.RuntimeDisposeCount);
    }
}
