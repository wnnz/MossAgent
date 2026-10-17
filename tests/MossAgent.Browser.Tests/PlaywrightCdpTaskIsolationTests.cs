using Microsoft.Playwright;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class PlaywrightCdpTaskIsolationTests
{
    [Fact]
    public async Task TwoConnections_CreateIndependentTaskPagesWithoutAdoptingUserTabs()
    {
        var probe = new PlaywrightCdpSessionProbe();
        var token = TestContext.Current.CancellationToken;
        await using var first = await probe.ConnectAsync(token);
        await using var second = await probe.ConnectAsync(token);

        Assert.Equal(2, probe.NewPageCount);
        Assert.Equal("task-page-1", Assert.Single(await first.ListTabsAsync(token)).Title);
        Assert.Equal("task-page-2", Assert.Single(await second.ListTabsAsync(token)).Title);
        await first.NavigateAsync(new Uri("https://first.example/"), token);
        await second.NavigateAsync(new Uri("https://second.example/"), token);
        Assert.Equal(new Uri("https://first.example/"), first.CurrentUri);
        Assert.Equal(new Uri("https://second.example/"), second.CurrentUri);
        Assert.Equal("about:blank", probe.Existing.Url);
    }

    [Fact]
    public async Task ForeignTabIds_AreRejectedAndReleasingOneTaskKeepsOtherPagesAlive()
    {
        var probe = new PlaywrightCdpSessionProbe();
        var token = TestContext.Current.CancellationToken;
        var first = await probe.ConnectAsync(token);
        await using var second = await probe.ConnectAsync(token);
        var foreign = Assert.Single(await second.ListTabsAsync(token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => first.SwitchTabAsync(foreign.Id, token));
        await first.NewTabAsync(null, token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => first.CloseTabAsync(foreign.Id, token));

        await first.DisposeAsync();
        await first.DisposeAsync();

        Assert.Equal(1, probe.RuntimeDisposeCount);
        Assert.False(probe.Existing.IsClosed);
        Assert.False(probe.Pages.Single(page => page.Name == "task-page-2").IsClosed);
        Assert.All(probe.Pages.Where(page => page.Name is "task-page-1" or "task-page-3"), page => Assert.True(page.IsClosed));
        Assert.Equal(0, probe.ContextCloseCount);
        Assert.Equal(0, probe.BrowserCloseCount);
        await second.NavigateAsync(new Uri("https://still-running.example/"), token);
        Assert.Equal(new Uri("https://still-running.example/"), second.CurrentUri);
    }

    [Fact]
    public async Task Popups_AreOwnedOnlyByTheirParentTaskAndSubscriptionsAreReleased()
    {
        var probe = new PlaywrightCdpSessionProbe();
        var token = TestContext.Current.CancellationToken;
        await using var first = await probe.ConnectAsync(token);
        await using var second = await probe.ConnectAsync(token);
        var parent = probe.Pages.SingleOrDefault(page => page.Name == "task-page-1") ?? probe.Existing;
        var popup = probe.CreatePage("task-popup");
        parent.OpenPopup(popup);
        var nested = probe.CreatePage("nested-popup");
        popup.OpenPopup(nested);
        Assert.Equal(3, (await first.ListTabsAsync(token)).Count);
        Assert.Single(await second.ListTabsAsync(token));
        var captured = parent.CapturePopupHandler();

        await first.DisposeAsync();

        Assert.True(popup.IsClosed);
        Assert.True(nested.IsClosed);
        Assert.Equal(0, parent.SubscriptionCount);
        Assert.Equal(0, popup.SubscriptionCount);
        captured?.Invoke(parent.Page, probe.CreatePage("late-event").Page);
        Assert.Single(await second.ListTabsAsync(token));
        Assert.False(probe.Existing.IsClosed);
    }

    [Fact]
    public async Task CancelledNewTabHandoff_IsDrainedAndClosesOnlyTheLateOwnedPage()
    {
        var probe = new PlaywrightCdpSessionProbe();
        var token = TestContext.Current.CancellationToken;
        await using var session = await probe.ConnectAsync(token);
        var handoff = new TaskCompletionSource<IPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.CreatePageOverride = () => handoff.Task;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var pending = session.NewTabAsync(null, cancellation.Token);
        cancellation.Cancel();
        await Task.Delay(20, token);
        var returnedEarly = pending.IsCompleted;
        var page = probe.CreatePage("late-owned-page");
        handoff.SetResult(page.Page);
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.False(returnedEarly);
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.True(page.IsClosed);
        Assert.False(probe.Existing.IsClosed);
        Assert.Single(await session.ListTabsAsync(token));
    }

    [Fact]
    public async Task Disposal_DrainsNewPageHandoffBeforeDisconnectingRuntime()
    {
        var probe = new PlaywrightCdpSessionProbe();
        var token = TestContext.Current.CancellationToken;
        var session = await probe.ConnectAsync(token);
        var handoff = new TaskCompletionSource<IPage>(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.CreatePageOverride = () => handoff.Task;
        var pending = session.NewTabAsync(null, token);
        var disposing = session.DisposeAsync().AsTask();
        var disposedEarly = disposing.IsCompleted;
        var releasedEarly = probe.RuntimeDisposeCount;
        var page = probe.CreatePage("late-owned-page");
        handoff.SetResult(page.Page);
        try { await pending; } catch (ObjectDisposedException) { }
        await disposing;

        Assert.False(disposedEarly);
        Assert.Equal(0, releasedEarly);
        Assert.True(page.IsClosed);
        Assert.Equal(1, probe.RuntimeDisposeCount);
        Assert.False(probe.Existing.IsClosed);
    }

    [Fact]
    public async Task FailedPageClose_StillClosesOtherOwnedPagesAndDisconnectsOnlyOnce()
    {
        var probe = new PlaywrightCdpSessionProbe();
        var token = TestContext.Current.CancellationToken;
        var session = await probe.ConnectAsync(token);
        await session.NewTabAsync(null, token);
        var first = probe.Pages.First(page => page.Name != "user-page");
        first.CloseFailure = new IOException("Synthetic owned-page close failure");

        await Assert.ThrowsAsync<IOException>(() => session.DisposeAsync().AsTask());

        Assert.True(probe.Pages.Last().IsClosed);
        Assert.False(probe.Existing.IsClosed);
        Assert.Equal(1, probe.RuntimeDisposeCount);
        Assert.Equal(0, probe.ContextCloseCount);
        Assert.All(probe.Pages, page => Assert.Equal(0, page.SubscriptionCount));
    }
}
