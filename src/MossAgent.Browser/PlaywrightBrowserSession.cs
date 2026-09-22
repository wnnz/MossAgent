using Microsoft.Playwright;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser;

public sealed class PlaywrightBrowserSession : IBrowserSession
{
    private readonly IPlaywright _playwright;
    private readonly IBrowserContext _context;
    private readonly bool _closeContext;
    private readonly Dictionary<IPage, string> _tabIds = [];
    private IPage _page;

    public PlaywrightBrowserSession(
        IPlaywright playwright,
        IBrowserContext context,
        IPage page,
        bool closeContext)
    {
        _playwright = playwright;
        _context = context;
        _page = page;
        _closeContext = closeContext;
        SynchronizeTabs();
    }

    public string Id { get; } = Guid.NewGuid().ToString("N");

    public Uri? CurrentUri => Uri.TryCreate(_page.Url, UriKind.Absolute, out var uri) ? uri : null;

    public async Task<IReadOnlyList<BrowserTabInfo>> ListTabsAsync(
        CancellationToken cancellationToken)
    {
        SynchronizeTabs();
        var tabs = new List<BrowserTabInfo>();
        foreach (var pair in _tabIds)
        {
            var title = await pair.Key.TitleAsync().WaitAsync(cancellationToken);
            var uri = Uri.TryCreate(pair.Key.Url, UriKind.Absolute, out var value) ? value : null;
            tabs.Add(new BrowserTabInfo(pair.Value, title, uri, ReferenceEquals(pair.Key, _page)));
        }

        return tabs;
    }

    public async Task<string> NewTabAsync(Uri? uri, CancellationToken cancellationToken)
    {
        var page = await _context.NewPageAsync().WaitAsync(cancellationToken);
        var id = AddTab(page);
        _page = page;
        if (uri is not null)
        {
            await NavigateAsync(uri, cancellationToken);
        }

        return id;
    }

    public Task SwitchTabAsync(string tabId, CancellationToken cancellationToken)
    {
        SynchronizeTabs();
        _page = _tabIds.FirstOrDefault(pair => pair.Value == tabId).Key
            ?? throw new InvalidOperationException($"浏览器标签不存在：{tabId}");
        return _page.BringToFrontAsync().WaitAsync(cancellationToken);
    }

    public async Task CloseTabAsync(string tabId, CancellationToken cancellationToken)
    {
        SynchronizeTabs();
        if (_tabIds.Count <= 1)
        {
            throw new InvalidOperationException("不能关闭浏览器会话的最后一个标签页。");
        }

        var page = _tabIds.FirstOrDefault(pair => pair.Value == tabId).Key
            ?? throw new InvalidOperationException($"浏览器标签不存在：{tabId}");
        await page.CloseAsync().WaitAsync(cancellationToken);
        _tabIds.Remove(page);
        if (ReferenceEquals(page, _page))
        {
            _page = _tabIds.Keys.First();
            await _page.BringToFrontAsync().WaitAsync(cancellationToken);
        }
    }

    public async Task NavigateAsync(Uri uri, CancellationToken cancellationToken)
    {
        await _page.GotoAsync(uri.ToString(), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60_000
        }).WaitAsync(cancellationToken);
    }

    public async Task GoBackAsync(CancellationToken cancellationToken) =>
        await _page.GoBackAsync(new PageGoBackOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60_000
        }).WaitAsync(cancellationToken);

    public async Task GoForwardAsync(CancellationToken cancellationToken) =>
        await _page.GoForwardAsync(new PageGoForwardOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60_000
        }).WaitAsync(cancellationToken);

    public async Task ReloadAsync(CancellationToken cancellationToken) =>
        await _page.ReloadAsync(new PageReloadOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60_000
        }).WaitAsync(cancellationToken);

    public async Task<string> GetPageTextAsync(CancellationToken cancellationToken) =>
        await _page.Locator("body").InnerTextAsync().WaitAsync(cancellationToken);

    public async Task<string> GetSnapshotAsync(CancellationToken cancellationToken) =>
        await _page.Locator("body").AriaSnapshotAsync().WaitAsync(cancellationToken);

    public async Task ClickAsync(string selector, CancellationToken cancellationToken) =>
        await _page.Locator(selector).ClickAsync().WaitAsync(cancellationToken);

    public async Task TypeAsync(
        string selector,
        string text,
        CancellationToken cancellationToken) =>
        await _page.Locator(selector).FillAsync(text).WaitAsync(cancellationToken);

    public async Task SelectOptionAsync(
        string selector,
        string value,
        CancellationToken cancellationToken) =>
        await _page.Locator(selector).SelectOptionAsync(value).WaitAsync(cancellationToken);

    public async Task WaitForAsync(
        string selector,
        BrowserElementState state,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        await _page.Locator(selector).WaitForAsync(new LocatorWaitForOptions
        {
            State = state switch
            {
                BrowserElementState.Attached => WaitForSelectorState.Attached,
                BrowserElementState.Detached => WaitForSelectorState.Detached,
                BrowserElementState.Hidden => WaitForSelectorState.Hidden,
                _ => WaitForSelectorState.Visible
            },
            Timeout = (float)timeout.TotalMilliseconds
        }).WaitAsync(cancellationToken);

    public async Task ScrollAsync(
        string? selector,
        double deltaX,
        double deltaY,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            await _page.EvaluateAsync("([x,y]) => window.scrollBy(x,y)", new[] { deltaX, deltaY })
                .WaitAsync(cancellationToken);
            return;
        }

        await _page.Locator(selector).EvaluateAsync(
            "(element, value) => element.scrollBy(value.x, value.y)",
            new { x = deltaX, y = deltaY }).WaitAsync(cancellationToken);
    }

    public async Task<byte[]> ScreenshotAsync(bool fullPage, CancellationToken cancellationToken) =>
        await _page.ScreenshotAsync(new PageScreenshotOptions
        {
            FullPage = fullPage,
            Type = ScreenshotType.Png
        }).WaitAsync(cancellationToken);

    public async Task<BrowserDownloadInfo> DownloadAsync(
        string selector,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var download = await _page.RunAndWaitForDownloadAsync(
            () => _page.Locator(selector).ClickAsync(),
            new PageRunAndWaitForDownloadOptions { Timeout = 60_000 })
            .WaitAsync(cancellationToken);
        var failure = await download.FailureAsync().WaitAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(failure))
        {
            throw new InvalidOperationException($"浏览器下载失败：{failure}");
        }

        await using var source = await download.CreateReadStreamAsync().WaitAsync(cancellationToken);
        await source.CopyToAsync(destination, cancellationToken);
        return new BrowserDownloadInfo(download.SuggestedFilename);
    }

    public async ValueTask DisposeAsync()
    {
        if (_closeContext)
        {
            await _context.CloseAsync();
        }

        _playwright.Dispose();
    }

    private void SynchronizeTabs()
    {
        foreach (var page in _context.Pages.Where(static page => !page.IsClosed))
        {
            AddTab(page);
        }

        foreach (var page in _tabIds.Keys.Where(static page => page.IsClosed).ToArray())
        {
            _tabIds.Remove(page);
        }
    }

    private string AddTab(IPage page)
    {
        if (_tabIds.TryGetValue(page, out var existing))
        {
            return existing;
        }

        var id = Guid.NewGuid().ToString("N");
        _tabIds[page] = id;
        return id;
    }
}
