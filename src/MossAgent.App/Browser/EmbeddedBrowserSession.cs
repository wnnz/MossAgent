using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions.Browser;
using NativeWebView.Core;
using WebViewControl = NativeWebView.Controls.NativeWebView;

namespace MossAgent.App.Browser;

public sealed class EmbeddedBrowserSession : IBrowserSession
{
    private readonly EmbeddedBrowserTabSet _tabs;
    private readonly EmbeddedBrowserInteractionDriver _interactions;
    private readonly EmbeddedBrowserPaneViewModel _pane;
    private Uri? _currentUri;
    private bool _disposed;
    public EmbeddedBrowserSession(
        WebViewControl view,
        EmbeddedBrowserPaneViewModel pane,
        Func<CancellationToken, Task<WebViewControl>> createView)
    {
        _pane = pane;
        _tabs = new EmbeddedBrowserTabSet(view, pane, createView);
        _interactions = new EmbeddedBrowserInteractionDriver(() => View, () => _disposed);
        _tabs.ActiveViewChanged += HandleActiveViewChanged;
        _tabs.Initialize();
        pane.AttachSession(this);
        _currentUri = view.CurrentUrl;
        view.NavigationCompleted += HandleNavigationCompleted;
    }

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public Uri? CurrentUri => _currentUri;
    private WebViewControl View => _tabs.ActiveView;

    public Task<IReadOnlyList<BrowserTabInfo>> ListTabsAsync(
        CancellationToken cancellationToken) => _tabs.ListAsync(cancellationToken);

    public async Task<string> NewTabAsync(Uri? uri, CancellationToken cancellationToken)
    {
        var id = await _tabs.NewAsync(cancellationToken);
        if (uri is not null)
        {
            await NavigateAsync(uri, cancellationToken);
        }

        await _pane.RefreshTabsAsync();
        return id;
    }

    public async Task SwitchTabAsync(string tabId, CancellationToken cancellationToken)
    {
        await _tabs.SwitchAsync(tabId, cancellationToken);
        await _pane.RefreshTabsAsync();
    }

    public async Task CloseTabAsync(string tabId, CancellationToken cancellationToken)
    {
        await _tabs.CloseAsync(tabId, cancellationToken);
        await _pane.RefreshTabsAsync();
    }

    public Task NavigateAsync(Uri uri, CancellationToken cancellationToken) =>
        _interactions.NavigateAsync(uri, cancellationToken);

    public Task GoBackAsync(CancellationToken cancellationToken) =>
        _interactions.GoBackAsync(cancellationToken);

    public Task GoForwardAsync(CancellationToken cancellationToken) =>
        _interactions.GoForwardAsync(cancellationToken);

    public Task ReloadAsync(CancellationToken cancellationToken) =>
        _interactions.ReloadAsync(cancellationToken);

    public Task<string> GetPageTextAsync(CancellationToken cancellationToken) =>
        _interactions.GetPageTextAsync(cancellationToken);

    public Task<string> GetSnapshotAsync(CancellationToken cancellationToken) =>
        _interactions.GetSnapshotAsync(cancellationToken);

    public Task ClickAsync(string selector, CancellationToken cancellationToken) =>
        _interactions.ClickAsync(selector, cancellationToken);

    public Task TypeAsync(string selector, string text, CancellationToken cancellationToken) =>
        _interactions.TypeAsync(selector, text, cancellationToken);

    public Task SelectOptionAsync(
        string selector,
        string value,
        CancellationToken cancellationToken) =>
        _interactions.SelectOptionAsync(selector, value, cancellationToken);

    public Task WaitForAsync(
        string selector,
        BrowserElementState state,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        _interactions.WaitForAsync(selector, state, timeout, cancellationToken);

    public Task ScrollAsync(
        string? selector,
        double deltaX,
        double deltaY,
        CancellationToken cancellationToken) =>
        _interactions.ScrollAsync(selector, deltaX, deltaY, cancellationToken);

    public Task<byte[]> ScreenshotAsync(bool fullPage, CancellationToken cancellationToken) =>
        _interactions.ScreenshotAsync(fullPage, cancellationToken);

    public Task<BrowserDownloadInfo> DownloadAsync(
        string selector,
        Stream destination,
        CancellationToken cancellationToken) =>
        _interactions.DownloadAsync(selector, destination, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tabs.ActiveViewChanged -= HandleActiveViewChanged;
        View.NavigationCompleted -= HandleNavigationCompleted;
        _pane.DetachSession(this);
        await _tabs.DisposeAsync();
    }

    private void HandleNavigationCompleted(
        object? sender,
        NativeWebViewNavigationCompletedEventArgs args)
    {
        _currentUri = args.Uri;
        _ = _pane.RefreshTabsAsync();
    }

    private void HandleActiveViewChanged(WebViewControl? previous, WebViewControl next)
    {
        if (previous is not null)
        {
            previous.NavigationCompleted -= HandleNavigationCompleted;
        }

        next.NavigationCompleted += HandleNavigationCompleted;
        _currentUri = next.CurrentUrl;
    }
}
