using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.App.Tests;

internal sealed class DelayedBrowserSession : IBrowserSession
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public Uri? CurrentUri { get; private set; }
    public Queue<TaskCompletionSource<IReadOnlyList<BrowserTabInfo>>> Requests { get; } = [];

    public Task<IReadOnlyList<BrowserTabInfo>> ListTabsAsync(CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<IReadOnlyList<BrowserTabInfo>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Requests.Enqueue(completion);
        return completion.Task.WaitAsync(cancellationToken);
    }

    public Task<string> NewTabAsync(Uri? uri, CancellationToken cancellationToken) =>
        Task.FromException<string>(new NotSupportedException());
    public Task SwitchTabAsync(string tabId, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task CloseTabAsync(string tabId, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task NavigateAsync(Uri uri, CancellationToken cancellationToken)
    {
        CurrentUri = uri;
        return Task.CompletedTask;
    }
    public Task GoBackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task GoForwardAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task ReloadAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<string> GetPageTextAsync(CancellationToken cancellationToken) => Task.FromResult(string.Empty);
    public Task<string> GetSnapshotAsync(CancellationToken cancellationToken) => Task.FromResult(string.Empty);
    public Task ClickAsync(string selector, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task TypeAsync(string selector, string text, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task SelectOptionAsync(string selector, string value, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task WaitForAsync(string selector, BrowserElementState state, TimeSpan timeout,
        CancellationToken cancellationToken) => Task.CompletedTask;
    public Task ScrollAsync(string? selector, double deltaX, double deltaY,
        CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<BrowserDownloadInfo> DownloadAsync(string selector, Stream destination,
        CancellationToken cancellationToken) => Task.FromException<BrowserDownloadInfo>(new NotSupportedException());
    public Task<byte[]> ScreenshotAsync(bool fullPage, CancellationToken cancellationToken) =>
        Task.FromResult(Array.Empty<byte>());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
