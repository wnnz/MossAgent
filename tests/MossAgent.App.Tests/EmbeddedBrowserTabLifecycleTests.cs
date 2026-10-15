using MossAgent.App.Browser;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using NativeWebView.Platform.Windows;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class EmbeddedBrowserTabLifecycleTests
{
    [Fact]
    public async Task CancelledCreation_DisposesUnpublishedViewWithoutChangingActiveTab()
    {
        using var initial = View();
        using var created = View();
        using var cancellation = new CancellationTokenSource();
        var pane = new EmbeddedBrowserPaneViewModel();
        await using var tabs = new EmbeddedBrowserTabSet(initial, pane, _ =>
        {
            cancellation.Cancel();
            return Task.FromResult<NativeWebView.Controls.NativeWebView>(created);
        });
        tabs.Initialize();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tabs.NewAsync(cancellation.Token));

        Assert.Same(initial, tabs.ActiveView);
        Assert.Same(initial, pane.Content);
        Assert.Equal("Disposed", created.LifecycleState.ToString());
    }

    [Fact]
    public async Task CancelledSwitch_DoesNotSeparatePaneAndActiveView()
    {
        using var initial = View();
        using var first = View();
        using var second = View();
        var pending = new Queue<AppEmbeddedBrowserView>([first, second]);
        var pane = new EmbeddedBrowserPaneViewModel();
        await using var tabs = new EmbeddedBrowserTabSet(initial, pane,
            _ => Task.FromResult<NativeWebView.Controls.NativeWebView>(pending.Dequeue()));
        tabs.Initialize();
        var firstId = await tabs.NewAsync(Token);
        await tabs.NewAsync(Token);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tabs.SwitchAsync(firstId, cancelled.Token));

        Assert.Same(second, tabs.ActiveView);
        Assert.Same(second, pane.Content);
    }

    [Fact]
    public async Task ClosingActiveTab_SelectsRemainingViewBeforeDisposingIt()
    {
        using var initial = View();
        using var created = View();
        var pane = new EmbeddedBrowserPaneViewModel();
        await using var tabs = new EmbeddedBrowserTabSet(initial, pane,
            _ => Task.FromResult<NativeWebView.Controls.NativeWebView>(created));
        tabs.Initialize();
        var id = await tabs.NewAsync(Token);
        await tabs.CloseAsync(id, Token);

        Assert.Same(initial, tabs.ActiveView);
        Assert.Same(initial, pane.Content);
        Assert.Equal("Disposed", created.LifecycleState.ToString());
        await Assert.ThrowsAsync<InvalidOperationException>(() => tabs.CloseAsync(id, Token));
        Assert.Same(initial, tabs.ActiveView);
    }

    [Fact]
    public async Task CancelledClose_DoesNotDisposeOrSwitchActiveView()
    {
        using var initial = View();
        using var created = View();
        var pane = new EmbeddedBrowserPaneViewModel();
        await using var tabs = new EmbeddedBrowserTabSet(initial, pane,
            _ => Task.FromResult<NativeWebView.Controls.NativeWebView>(created));
        tabs.Initialize();
        var id = await tabs.NewAsync(Token);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tabs.CloseAsync(id, cancelled.Token));

        Assert.Same(created, tabs.ActiveView);
        Assert.Same(created, pane.Content);
        Assert.NotEqual("Disposed", created.LifecycleState.ToString());
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static AppEmbeddedBrowserView View() => new(new WindowsNativeWebViewBackend());
}
