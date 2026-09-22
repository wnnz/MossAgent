using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser.Tests;

internal sealed class StubBrowserSession : IBrowserSession
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public Uri? CurrentUri { get; private set; }
    public string? LastSelector { get; private set; }
    public string? LastValue { get; private set; }
    public BrowserElementState? LastWaitState { get; private set; }
    public TimeSpan? LastTimeout { get; private set; }
    public double LastDeltaX { get; private set; }
    public double LastDeltaY { get; private set; }
    public int BackCount { get; private set; }
    public int ForwardCount { get; private set; }
    public int ReloadCount { get; private set; }
    public string ActiveTabId { get; private set; } = "tab-1";
    public int TabCount { get; private set; } = 1;

    public Task NavigateAsync(Uri uri, CancellationToken cancellationToken)
    {
        CurrentUri = uri;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BrowserTabInfo>> ListTabsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BrowserTabInfo>>(
            Enumerable.Range(1, TabCount)
                .Select(index => new BrowserTabInfo(
                    $"tab-{index}", $"Tab {index}", null, ActiveTabId == $"tab-{index}"))
                .ToArray());

    public Task<string> NewTabAsync(Uri? uri, CancellationToken cancellationToken)
    {
        TabCount++;
        ActiveTabId = $"tab-{TabCount}";
        CurrentUri = uri;
        return Task.FromResult(ActiveTabId);
    }

    public Task SwitchTabAsync(string tabId, CancellationToken cancellationToken)
    {
        ActiveTabId = tabId;
        return Task.CompletedTask;
    }

    public Task CloseTabAsync(string tabId, CancellationToken cancellationToken)
    {
        if (TabCount <= 1)
        {
            throw new InvalidOperationException("不能关闭浏览器会话的最后一个标签页。");
        }

        TabCount--;
        if (ActiveTabId == tabId)
        {
            ActiveTabId = "tab-1";
        }

        return Task.CompletedTask;
    }

    public Task GoBackAsync(CancellationToken cancellationToken)
    {
        BackCount++;
        return Task.CompletedTask;
    }

    public Task GoForwardAsync(CancellationToken cancellationToken)
    {
        ForwardCount++;
        return Task.CompletedTask;
    }

    public Task ReloadAsync(CancellationToken cancellationToken)
    {
        ReloadCount++;
        return Task.CompletedTask;
    }

    public Task<string> GetPageTextAsync(CancellationToken cancellationToken) =>
        Task.FromResult(string.Empty);

    public Task<string> GetSnapshotAsync(CancellationToken cancellationToken) =>
        Task.FromResult(string.Empty);

    public Task ClickAsync(string selector, CancellationToken cancellationToken) =>
        RecordSelectorAsync(selector);

    public Task TypeAsync(
        string selector,
        string text,
        CancellationToken cancellationToken)
    {
        LastSelector = selector;
        LastValue = text;
        return Task.CompletedTask;
    }

    public Task SelectOptionAsync(
        string selector,
        string value,
        CancellationToken cancellationToken)
    {
        LastSelector = selector;
        LastValue = value;
        return Task.CompletedTask;
    }

    public Task WaitForAsync(
        string selector,
        BrowserElementState state,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        LastSelector = selector;
        LastWaitState = state;
        LastTimeout = timeout;
        return Task.CompletedTask;
    }

    public Task ScrollAsync(
        string? selector,
        double deltaX,
        double deltaY,
        CancellationToken cancellationToken)
    {
        LastSelector = selector;
        LastDeltaX = deltaX;
        LastDeltaY = deltaY;
        return Task.CompletedTask;
    }

    public Task<byte[]> ScreenshotAsync(bool fullPage, CancellationToken cancellationToken) =>
        Task.FromResult(Array.Empty<byte>());

    public async Task<BrowserDownloadInfo> DownloadAsync(
        string selector,
        Stream destination,
        CancellationToken cancellationToken)
    {
        LastSelector = selector;
        var bytes = System.Text.Encoding.UTF8.GetBytes("fixture download");
        await destination.WriteAsync(bytes, cancellationToken);
        return new BrowserDownloadInfo("report.txt");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task RecordSelectorAsync(string selector)
    {
        LastSelector = selector;
        return Task.CompletedTask;
    }
}
