using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser;

public sealed class LazyBrowserSession(Func<CancellationToken, Task<IBrowserSession>> factory)
    : IBrowserSession
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IBrowserSession? _inner;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public Uri? CurrentUri => _inner?.CurrentUri;

    public async Task<IReadOnlyList<BrowserTabInfo>> ListTabsAsync(
        CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken)).ListTabsAsync(cancellationToken);

    public async Task<string> NewTabAsync(Uri? uri, CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken)).NewTabAsync(uri, cancellationToken);

    public async Task SwitchTabAsync(string tabId, CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken)).SwitchTabAsync(tabId, cancellationToken);

    public async Task CloseTabAsync(string tabId, CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken)).CloseTabAsync(tabId, cancellationToken);

    public async Task NavigateAsync(Uri uri, CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken)).NavigateAsync(uri, cancellationToken);

    public async Task GoBackAsync(CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken)).GoBackAsync(cancellationToken);

    public async Task GoForwardAsync(CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken)).GoForwardAsync(cancellationToken);

    public async Task ReloadAsync(CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken)).ReloadAsync(cancellationToken);

    public async Task<string> GetPageTextAsync(CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken)).GetPageTextAsync(cancellationToken);

    public async Task<string> GetSnapshotAsync(CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken)).GetSnapshotAsync(cancellationToken);

    public async Task ClickAsync(string selector, CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken)).ClickAsync(selector, cancellationToken);

    public async Task TypeAsync(
        string selector,
        string text,
        CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken)).TypeAsync(selector, text, cancellationToken);

    public async Task SelectOptionAsync(
        string selector,
        string value,
        CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken))
            .SelectOptionAsync(selector, value, cancellationToken);

    public async Task WaitForAsync(
        string selector,
        BrowserElementState state,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken))
            .WaitForAsync(selector, state, timeout, cancellationToken);

    public async Task ScrollAsync(
        string? selector,
        double deltaX,
        double deltaY,
        CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken))
            .ScrollAsync(selector, deltaX, deltaY, cancellationToken);

    public async Task<byte[]> ScreenshotAsync(bool fullPage, CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken)).ScreenshotAsync(fullPage, cancellationToken);

    public async Task<BrowserDownloadInfo> DownloadAsync(
        string selector,
        Stream destination,
        CancellationToken cancellationToken) =>
        await (await GetAsync(cancellationToken))
            .DownloadAsync(selector, destination, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_inner is not null)
        {
            await _inner.DisposeAsync();
        }

        _gate.Dispose();
    }

    private async Task<IBrowserSession> GetAsync(CancellationToken cancellationToken)
    {
        if (_inner is not null)
        {
            return _inner;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            _inner ??= await factory(cancellationToken);
            return _inner;
        }
        finally
        {
            _gate.Release();
        }
    }
}
