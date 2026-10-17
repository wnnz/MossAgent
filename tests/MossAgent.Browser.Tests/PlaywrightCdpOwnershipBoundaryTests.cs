using Microsoft.Playwright;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class PlaywrightCdpOwnershipBoundaryTests
{
    [Fact]
    public async Task UnrelatedPopups_AreNotImportedAndOwnedPopupIdsRemainStable()
    {
        var probe = new PlaywrightCdpSessionProbe();
        var token = TestContext.Current.CancellationToken;
        await using var session = await probe.ConnectAsync(token);
        var foreign = probe.CreatePage("foreign-popup");
        probe.Existing.OpenPopup(foreign);
        var parent = probe.Pages.Single(page => page.Name == "task-page-1");
        var owned = probe.CreatePage("owned-popup");
        parent.OpenPopup(owned);
        parent.OpenPopup(owned);

        var first = await session.ListTabsAsync(token);
        var second = await session.ListTabsAsync(token);

        Assert.Equal(2, first.Count);
        Assert.Equal(first.Select(tab => tab.Id), second.Select(tab => tab.Id));
        Assert.DoesNotContain(first, tab => tab.Title is "user-page" or "foreign-popup");
        Assert.Equal(1, owned.SubscriptionCount);
        await session.DisposeAsync();
        Assert.False(foreign.IsClosed);
        Assert.True(owned.IsClosed);
    }

    [Fact]
    public async Task PopupArrivingWhileParentCloses_IsDrainedBeforeRuntimeRelease()
    {
        var probe = new PlaywrightCdpSessionProbe();
        var session = await probe.ConnectAsync(TestContext.Current.CancellationToken);
        var parent = probe.Pages.Single(page => page.Name == "task-page-1");
        var late = probe.CreatePage("closing-popup");
        parent.OnClose = () => parent.OpenPopup(late);

        await session.DisposeAsync();

        Assert.True(parent.IsClosed);
        Assert.True(late.IsClosed);
        Assert.Equal(0, late.SubscriptionCount);
        Assert.False(probe.Existing.IsClosed);
        Assert.Equal(1, probe.RuntimeDisposeCount);
    }

    [Fact]
    public async Task CancelledPageCreationFailure_PreservesCancellationIdentity()
    {
        var probe = new PlaywrightCdpSessionProbe();
        var token = TestContext.Current.CancellationToken;
        await using var session = await probe.ConnectAsync(token);
        var handoff = new TaskCompletionSource<IPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.CreatePageOverride = () => handoff.Task;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var pending = session.NewTabAsync(null, cancellation.Token);
        cancellation.Cancel();
        handoff.SetException(new IOException("Synthetic late SDK failure"));

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Single(await session.ListTabsAsync(token));
        Assert.False(probe.Existing.IsClosed);
    }

    [Fact]
    public async Task CancelledOrDisposedNewTab_DoesNotEnterTheSdk()
    {
        var probe = new PlaywrightCdpSessionProbe();
        var token = TestContext.Current.CancellationToken;
        var session = await probe.ConnectAsync(token);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.NewTabAsync(null, cancellation.Token));
        await session.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.NewTabAsync(null, token));

        Assert.Equal(1, probe.NewPageCount);
        Assert.Equal(1, probe.RuntimeDisposeCount);
        Assert.False(probe.Existing.IsClosed);
    }

    [Fact]
    public async Task ExplicitlyBorrowedInitialPage_IsRetainedButNewTabsAreReclaimed()
    {
        var probe = new PlaywrightCdpSessionProbe();
        var runtime = PlaywrightApiProxy.Create<IPlaywright>((method, _) => method.Name == "Dispose"
            ? null : throw new NotSupportedException(method.Name));
        var session = new PlaywrightBrowserSession(runtime, probe.Context, probe.Existing.Page, closeContext: false);
        await session.NewTabAsync(null, TestContext.Current.CancellationToken);

        await session.DisposeAsync();

        Assert.False(probe.Existing.IsClosed);
        Assert.True(probe.Pages.Last().IsClosed);
        Assert.Equal(0, probe.Existing.SubscriptionCount);
        Assert.Equal(0, probe.ContextCloseCount);
    }

    [Fact]
    public async Task OwnedContext_StillDiscoversItsOwnPagesAndClosesTheWholeContext()
    {
        var probe = new PlaywrightCdpSessionProbe();
        var runtime = PlaywrightApiProxy.Create<IPlaywright>((method, _) => method.Name == "Dispose"
            ? null : throw new NotSupportedException(method.Name));
        var session = new PlaywrightBrowserSession(runtime, probe.Context, probe.Existing.Page, closeContext: true);
        probe.CreatePage("managed-context-page");

        Assert.Equal(2, (await session.ListTabsAsync(TestContext.Current.CancellationToken)).Count);
        await session.DisposeAsync();

        Assert.Equal(1, probe.ContextCloseCount);
        Assert.All(probe.Pages, page => Assert.Equal(0, page.SubscriptionCount));
    }
}
