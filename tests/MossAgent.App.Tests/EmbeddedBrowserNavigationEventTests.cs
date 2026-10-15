using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class EmbeddedBrowserNavigationEventTests
{
    [Fact]
    public void BackendOriginatedNavigation_UpdatesAddressAndStatus()
    {
        using var backend = new NavigationEventBackend();
        using var view = new AppEmbeddedBrowserView(backend);
        var pane = new EmbeddedBrowserPaneViewModel();
        pane.Attach(view);

        backend.Navigate(new Uri("https://example.com/first"));

        Assert.Equal("https://example.com/first", pane.Address);
        Assert.Equal("页面加载完成。", pane.Status);
    }

    [Fact]
    public void HistoryNavigation_KeepsAddressAndCommandsInSync()
    {
        using var backend = new NavigationEventBackend();
        using var view = new AppEmbeddedBrowserView(backend);
        using var fixture = new BrowserToolbarFixture();
        var pane = fixture.Pane;
        pane.Attach(view);
        backend.Navigate(new Uri("https://example.com/first"));
        backend.Navigate(new Uri("https://example.com/second"));
        Assert.True(pane.BackCommand.CanExecute(null));

        backend.GoBack();
        Assert.Equal("https://example.com/first", pane.Address);
        Assert.False(pane.BackCommand.CanExecute(null));
        Assert.True(pane.ForwardCommand.CanExecute(null));
        backend.GoForward();
        Assert.Equal("https://example.com/second", pane.Address);
        Assert.True(pane.BackCommand.CanExecute(null));
        Assert.False(pane.ForwardCommand.CanExecute(null));
    }

    [Fact]
    public void ReplacedAndDetachedViews_CannotChangeCurrentPresentation()
    {
        using var firstBackend = new NavigationEventBackend();
        using var secondBackend = new NavigationEventBackend();
        using var first = new AppEmbeddedBrowserView(firstBackend);
        using var second = new AppEmbeddedBrowserView(secondBackend);
        var pane = new EmbeddedBrowserPaneViewModel();
        pane.Attach(first);
        pane.Attach(second);
        secondBackend.Navigate(new Uri("https://example.com/current"));
        firstBackend.Navigate(new Uri("https://example.com/stale"));

        Assert.Equal("https://example.com/current", pane.Address);
        pane.Detach(second);
        secondBackend.Navigate(new Uri("https://example.com/detached"));
        Assert.Equal("https://example.com/current", pane.Address);
        Assert.Equal("浏览器会话已关闭。", pane.Status);
    }

    [Fact]
    public void ReattachingTheSameView_DoesNotDuplicateHistoryNotifications()
    {
        using var backend = new NavigationEventBackend();
        using var view = new AppEmbeddedBrowserView(backend);
        var pane = new EmbeddedBrowserPaneViewModel();
        pane.Attach(view);
        pane.Attach(view);
        pane.Attach(view);
        var notifications = 0;
        pane.BackCommand.CanExecuteChanged += (_, _) => notifications++;

        backend.Navigate(new Uri("https://example.com/first"));

        Assert.Equal(1, notifications);
    }
}
