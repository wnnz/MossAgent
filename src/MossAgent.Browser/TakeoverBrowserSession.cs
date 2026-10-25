using MossAgent.Browser.Resources;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser;

internal sealed class TakeoverBrowserSession(
    IBrowserSession inner,
    BrowserTaskSessionState state,
    Action? onInnerDisposed = null)
    : IBrowserSession
{
    public string Id => inner.Id;
    public Uri? CurrentUri => state.IsAutomated ? inner.CurrentUri : null;
    public bool SupportsDeveloperTools => inner.SupportsDeveloperTools;

    public Task<IReadOnlyList<BrowserTabInfo>> ListTabsAsync(
        CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.ListTabsAsync(token), cancellationToken);

    public Task<string> NewTabAsync(Uri? uri, CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.NewTabAsync(uri, token), cancellationToken);

    public Task SwitchTabAsync(string tabId, CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.SwitchTabAsync(tabId, token), cancellationToken);

    public Task CloseTabAsync(string tabId, CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.CloseTabAsync(tabId, token), cancellationToken);

    public Task NavigateAsync(Uri uri, CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.NavigateAsync(uri, token), cancellationToken);

    public Task GoBackAsync(CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.GoBackAsync(token), cancellationToken);

    public Task GoForwardAsync(CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.GoForwardAsync(token), cancellationToken);

    public Task ReloadAsync(CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.ReloadAsync(token), cancellationToken);

    public Task OpenDeveloperToolsAsync(CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.OpenDeveloperToolsAsync(token), cancellationToken);

    public Task<string> GetPageTextAsync(CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.GetPageTextAsync(token), cancellationToken);

    public Task<string> GetSnapshotAsync(CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.GetSnapshotAsync(token), cancellationToken);

    public Task ClickAsync(string selector, CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.ClickAsync(selector, token), cancellationToken);

    public Task TypeAsync(string selector, string text, CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.TypeAsync(selector, text, token), cancellationToken);

    public Task SelectOptionAsync(
        string selector,
        string value,
        CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.SelectOptionAsync(selector, value, token), cancellationToken);

    public Task WaitForAsync(
        string selector,
        BrowserElementState elementState,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.WaitForAsync(selector, elementState, timeout, token),
            cancellationToken);

    public Task ScrollAsync(
        string? selector,
        double deltaX,
        double deltaY,
        CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.ScrollAsync(selector, deltaX, deltaY, token), cancellationToken);

    public Task<BrowserDownloadInfo> DownloadAsync(
        string selector,
        Stream destination,
        CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.DownloadAsync(selector, destination, token), cancellationToken);

    public Task<byte[]> ScreenshotAsync(bool fullPage, CancellationToken cancellationToken) =>
        RunAsync((session, token) => session.ScreenshotAsync(fullPage, token), cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    private async Task<TResult> RunAsync<TResult>(
        Func<IBrowserSession, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        using var linked = state.EnterAutomation(cancellationToken);
        try
        {
            return await operation(inner, linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            state.ThrowIfNotAutomated();
            throw;
        }
        catch (ObjectDisposedException)
        {
            onInnerDisposed?.Invoke();
            throw new BrowserTakeoverException(
                BrowserToolText.Get("BrowserSessionDisconnected"),
                BrowserControlState.Disconnected);
        }
    }

    private Task RunAsync(
        Func<IBrowserSession, CancellationToken, Task> operation,
        CancellationToken cancellationToken) =>
        RunAsync(async (session, token) =>
        {
            await operation(session, token);
            return true;
        }, cancellationToken);
}
