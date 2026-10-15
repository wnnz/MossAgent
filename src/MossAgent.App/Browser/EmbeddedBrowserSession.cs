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
    private readonly Action? _onDisposed;
    private readonly EmbeddedBrowserOperationQueue _operations = new();
    private Uri? _currentUri;
    private bool _disposed;
    public EmbeddedBrowserSession(
        WebViewControl view,
        EmbeddedBrowserPaneViewModel pane,
        Func<CancellationToken, Task<WebViewControl>> createView,
        Action? onDisposed = null)
    {
        _pane = pane;
        _onDisposed = onDisposed;
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
    public bool SupportsDeveloperTools => true;
    private WebViewControl View => _tabs.ActiveView;

    public Task<IReadOnlyList<BrowserTabInfo>> ListTabsAsync(
        CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _tabs.ListAsync(cancellationToken), cancellationToken);

    public async Task<string> NewTabAsync(Uri? uri, CancellationToken cancellationToken)
    {
        var id = await _operations.RunAsync(() => NewTabCoreAsync(uri, cancellationToken), cancellationToken);
        await _pane.RefreshTabsAsync();
        return id;
    }

    private async Task<string> NewTabCoreAsync(Uri? uri, CancellationToken cancellationToken)
    {
        var id = await _tabs.NewAsync(cancellationToken);
        if (uri is not null) await _interactions.NavigateAsync(uri, cancellationToken);
        return id;
    }

    public async Task SwitchTabAsync(string tabId, CancellationToken cancellationToken)
    {
        await _operations.RunAsync(() => _tabs.SwitchAsync(tabId, cancellationToken), cancellationToken);
        await _pane.RefreshTabsAsync();
    }

    public async Task CloseTabAsync(string tabId, CancellationToken cancellationToken)
    {
        await _operations.RunAsync(() => _tabs.CloseAsync(tabId, cancellationToken), cancellationToken);
        await _pane.RefreshTabsAsync();
    }

    public Task NavigateAsync(Uri uri, CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _interactions.NavigateAsync(uri, cancellationToken), cancellationToken);

    public Task GoBackAsync(CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _interactions.GoBackAsync(cancellationToken), cancellationToken);

    public Task GoForwardAsync(CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _interactions.GoForwardAsync(cancellationToken), cancellationToken);

    public Task ReloadAsync(CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _interactions.ReloadAsync(cancellationToken), cancellationToken);

    public Task OpenDeveloperToolsAsync(CancellationToken cancellationToken) =>
        _operations.RunAsync(() => AvaloniaUiThread.RunAsync(() => View.OpenDevToolsWindow(), cancellationToken),
            cancellationToken);

    public Task<string> GetPageTextAsync(CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _interactions.GetPageTextAsync(cancellationToken), cancellationToken);

    public Task<string> GetSnapshotAsync(CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _interactions.GetSnapshotAsync(cancellationToken), cancellationToken);

    public Task ClickAsync(string selector, CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _interactions.ClickAsync(selector, cancellationToken), cancellationToken);

    public Task TypeAsync(string selector, string text, CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _interactions.TypeAsync(selector, text, cancellationToken), cancellationToken);

    public Task SelectOptionAsync(
        string selector,
        string value,
        CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _interactions.SelectOptionAsync(selector, value, cancellationToken), cancellationToken);

    public Task WaitForAsync(
        string selector,
        BrowserElementState state,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _interactions.WaitForAsync(selector, state, timeout, cancellationToken), cancellationToken);

    public Task ScrollAsync(
        string? selector,
        double deltaX,
        double deltaY,
        CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _interactions.ScrollAsync(selector, deltaX, deltaY, cancellationToken), cancellationToken);

    public Task<byte[]> ScreenshotAsync(bool fullPage, CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _interactions.ScreenshotAsync(fullPage, cancellationToken), cancellationToken);

    public Task<BrowserDownloadInfo> DownloadAsync(
        string selector,
        Stream destination,
        CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _interactions.DownloadAsync(selector, destination, cancellationToken), cancellationToken);

    public ValueTask DisposeAsync() => _operations.DisposeAsync(() =>
        AvaloniaUiThread.RunAsync(DisposeOnUiThreadAsync, CancellationToken.None));

    private async Task DisposeOnUiThreadAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tabs.ActiveViewChanged -= HandleActiveViewChanged;
        View.NavigationCompleted -= HandleNavigationCompleted;
        _pane.DetachSession(this);
        try { await _tabs.DisposeAsync(); }
        finally { _onDisposed?.Invoke(); }
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
