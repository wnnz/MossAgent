using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser.Tests;

internal sealed class ProbeBrowserSession : IBrowserSession
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public Uri? CurrentUri { get; private set; }
    public int ListTabsCalls { get; private set; }
    public Exception? ListTabsFailure { get; set; }
    public Exception? OperationFailure { get; set; }
    public bool BlockNavigate { get; set; }
    public bool BlockListTabs { get; set; }
    public bool IsDisposed { get; private set; }
    public TaskCompletionSource NavigateStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ListTabsStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<IReadOnlyList<BrowserTabInfo>> ListTabsAsync(
        CancellationToken cancellationToken)
    {
        ListTabsCalls++;
        if (BlockListTabs)
        {
            ListTabsStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        if (ListTabsFailure is not null)
        {
            throw ListTabsFailure;
        }

        return [new BrowserTabInfo("tab-1", "Tab 1", CurrentUri, true)];
    }

    public Task<string> NewTabAsync(Uri? uri, CancellationToken cancellationToken) =>
        Task.FromResult("tab-1");

    public Task SwitchTabAsync(string tabId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task CloseTabAsync(string tabId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task NavigateAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (BlockNavigate)
        {
            NavigateStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        CurrentUri = uri;
    }

    public Task GoBackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task GoForwardAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task ReloadAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<string> GetPageTextAsync(CancellationToken cancellationToken) =>
        OperationFailure is null
            ? Task.FromResult(string.Empty)
            : Task.FromException<string>(OperationFailure);
    public Task<string> GetSnapshotAsync(CancellationToken cancellationToken) =>
        Task.FromResult(string.Empty);
    public Task ClickAsync(string selector, CancellationToken cancellationToken) =>
        Task.CompletedTask;
    public Task TypeAsync(string selector, string text, CancellationToken cancellationToken) =>
        Task.CompletedTask;
    public Task SelectOptionAsync(
        string selector, string value, CancellationToken cancellationToken) =>
        Task.CompletedTask;
    public Task WaitForAsync(
        string selector, BrowserElementState state, TimeSpan timeout,
        CancellationToken cancellationToken) => Task.CompletedTask;
    public Task ScrollAsync(
        string? selector, double deltaX, double deltaY,
        CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<BrowserDownloadInfo> DownloadAsync(
        string selector, Stream destination, CancellationToken cancellationToken) =>
        Task.FromResult(new BrowserDownloadInfo("file.bin"));
    public Task<byte[]> ScreenshotAsync(bool fullPage, CancellationToken cancellationToken) =>
        Task.FromResult(Array.Empty<byte>());

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
