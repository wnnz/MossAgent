namespace MossAgent.Tools.Abstractions.Browser;

public interface IBrowserSession : IAsyncDisposable
{
    string Id { get; }
    Uri? CurrentUri { get; }
    Task<IReadOnlyList<BrowserTabInfo>> ListTabsAsync(CancellationToken cancellationToken);
    Task<string> NewTabAsync(Uri? uri, CancellationToken cancellationToken);
    Task SwitchTabAsync(string tabId, CancellationToken cancellationToken);
    Task CloseTabAsync(string tabId, CancellationToken cancellationToken);
    Task NavigateAsync(Uri uri, CancellationToken cancellationToken);
    Task GoBackAsync(CancellationToken cancellationToken);
    Task GoForwardAsync(CancellationToken cancellationToken);
    Task ReloadAsync(CancellationToken cancellationToken);
    Task<string> GetPageTextAsync(CancellationToken cancellationToken);
    Task<string> GetSnapshotAsync(CancellationToken cancellationToken);
    Task ClickAsync(string selector, CancellationToken cancellationToken);
    Task TypeAsync(string selector, string text, CancellationToken cancellationToken);
    Task SelectOptionAsync(
        string selector,
        string value,
        CancellationToken cancellationToken);
    Task WaitForAsync(
        string selector,
        BrowserElementState state,
        TimeSpan timeout,
        CancellationToken cancellationToken);
    Task ScrollAsync(
        string? selector,
        double deltaX,
        double deltaY,
        CancellationToken cancellationToken);
    Task<BrowserDownloadInfo> DownloadAsync(
        string selector,
        Stream destination,
        CancellationToken cancellationToken);
    Task<byte[]> ScreenshotAsync(bool fullPage, CancellationToken cancellationToken);
}
