using Microsoft.Playwright;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser;

public sealed class PlaywrightBrowserSession : IBrowserSession
{
    private readonly IPlaywright _playwright;
    private readonly PlaywrightBrowserTabSet _tabs;
    private readonly PlaywrightBrowserOperation _operations;
    private readonly object _lifetimeSync = new();
    private Task? _disposeTask;
    private IPage _page;

    public PlaywrightBrowserSession(
        IPlaywright playwright,
        IBrowserContext context,
        IPage page,
        bool closeContext,
        bool ownsInitialPage = false)
    {
        _playwright = playwright;
        _page = page;
        _tabs = new PlaywrightBrowserTabSet(context, page, closeContext, ownsInitialPage);
        _operations = new(DisposeAsync);
    }

    public string Id { get; } = Guid.NewGuid().ToString("N");

    public Uri? CurrentUri => Uri.TryCreate(_page.Url, UriKind.Absolute, out var uri) ? uri : null;

    public Task<IReadOnlyList<BrowserTabInfo>> ListTabsAsync(
        CancellationToken cancellationToken) => _operations.RunAsync<IReadOnlyList<BrowserTabInfo>>(async () =>
    {
        var tabs = new List<BrowserTabInfo>();
        foreach (var pair in _tabs.Snapshot())
        {
            var title = await pair.Key.TitleAsync();
            var uri = Uri.TryCreate(pair.Key.Url, UriKind.Absolute, out var value) ? value : null;
            tabs.Add(new BrowserTabInfo(pair.Value, title, uri, ReferenceEquals(pair.Key, _page)));
        }

        return tabs;
    }, cancellationToken);

    public Task<string> NewTabAsync(Uri? uri, CancellationToken cancellationToken) => _operations.RunAsync(async () =>
    {
        var created = await _tabs.CreateAsync(cancellationToken);
        _page = created.Page;
        if (uri is not null)
        {
            await NavigateAsync(uri, cancellationToken);
        }

        return created.Id;
    }, cancellationToken);

    public Task SwitchTabAsync(string tabId, CancellationToken cancellationToken) => _operations.RunAsync(() =>
    {
        _page = _tabs.Find(tabId);
        return _page.BringToFrontAsync();
    }, cancellationToken);

    public Task CloseTabAsync(string tabId, CancellationToken cancellationToken) => _operations.RunAsync(async () =>
    {
        if (_tabs.Snapshot().Length <= 1)
        {
            throw new InvalidOperationException("不能关闭浏览器会话的最后一个标签页。");
        }

        var page = _tabs.Find(tabId);
        await page.CloseAsync();
        _tabs.Remove(page);
        if (ReferenceEquals(page, _page))
        {
            _page = _tabs.Snapshot().First().Key;
            await _page.BringToFrontAsync();
        }
    }, cancellationToken);

    public Task NavigateAsync(Uri uri, CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _page.GotoAsync(uri.ToString(), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60_000
        }), cancellationToken);

    public Task GoBackAsync(CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _page.GoBackAsync(new PageGoBackOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60_000
        }), cancellationToken);

    public Task GoForwardAsync(CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _page.GoForwardAsync(new PageGoForwardOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60_000
        }), cancellationToken);

    public Task ReloadAsync(CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _page.ReloadAsync(new PageReloadOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60_000
        }), cancellationToken);

    public Task<string> GetPageTextAsync(CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _page.Locator("body").InnerTextAsync(), cancellationToken);

    public Task<string> GetSnapshotAsync(CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _page.Locator("body").AriaSnapshotAsync(), cancellationToken);

    public Task ClickAsync(string selector, CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _page.Locator(selector).ClickAsync(), cancellationToken);

    public Task TypeAsync(
        string selector,
        string text,
        CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _page.Locator(selector).FillAsync(text), cancellationToken);

    public Task SelectOptionAsync(
        string selector,
        string value,
        CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _page.Locator(selector).SelectOptionAsync(value), cancellationToken);

    public Task WaitForAsync(
        string selector,
        BrowserElementState state,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _page.Locator(selector).WaitForAsync(new LocatorWaitForOptions
        {
            State = state switch
            {
                BrowserElementState.Attached => WaitForSelectorState.Attached,
                BrowserElementState.Detached => WaitForSelectorState.Detached,
                BrowserElementState.Hidden => WaitForSelectorState.Hidden,
                _ => WaitForSelectorState.Visible
            },
            Timeout = (float)timeout.TotalMilliseconds
        }), cancellationToken);

    public Task ScrollAsync(
        string? selector,
        double deltaX,
        double deltaY,
        CancellationToken cancellationToken) => _operations.RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            await _page.EvaluateAsync("([x,y]) => window.scrollBy(x,y)", new[] { deltaX, deltaY });
            return;
        }

        await _page.Locator(selector).EvaluateAsync(
            "(element, value) => element.scrollBy(value.x, value.y)",
            new { x = deltaX, y = deltaY });
    }, cancellationToken);

    public Task<byte[]> ScreenshotAsync(bool fullPage, CancellationToken cancellationToken) =>
        _operations.RunAsync(() => _page.ScreenshotAsync(new PageScreenshotOptions
        {
            FullPage = fullPage,
            Type = ScreenshotType.Png
        }), cancellationToken);

    public Task<BrowserDownloadInfo> DownloadAsync(
        string selector,
        Stream destination,
        CancellationToken cancellationToken) => _operations.RunAsync(async () =>
    {
        var download = await _page.RunAndWaitForDownloadAsync(
            () => _page.Locator(selector).ClickAsync(),
            new PageRunAndWaitForDownloadOptions { Timeout = 60_000 });
        var failure = await download.FailureAsync();
        if (!string.IsNullOrWhiteSpace(failure))
        {
            throw new InvalidOperationException($"浏览器下载失败：{failure}");
        }

        await using var source = await download.CreateReadStreamAsync();
        await source.CopyToAsync(destination, cancellationToken);
        return new BrowserDownloadInfo(download.SuggestedFilename);
    }, cancellationToken);

    public ValueTask DisposeAsync()
    {
        _operations.Stop();
        lock (_lifetimeSync) return new ValueTask(_disposeTask ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            await _tabs.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _playwright.Dispose();
        }
    }

}
