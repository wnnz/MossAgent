using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.App.Tests;

internal sealed class BrowserToolbarSession : IBrowserSession
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public Uri? CurrentUri { get; private set; }
    public bool SupportsDeveloperTools { get; set; } = true;
    public List<string> Calls { get; } = [];
    public List<BrowserTabInfo> Tabs { get; } = [new("first", "First", null, true), new("second", "Second", null, false)];
    public Func<string, CancellationToken, Task> Perform { get; set; } = (_, _) => Task.CompletedTask;

    private async Task CallAsync(string name, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Calls.Add(name);
        await Perform(name, token);
    }

    public Task<IReadOnlyList<BrowserTabInfo>> ListTabsAsync(CancellationToken token) =>
        Task.FromResult<IReadOnlyList<BrowserTabInfo>>(Tabs.ToArray());
    public async Task NavigateAsync(Uri uri, CancellationToken token)
    {
        await CallAsync("navigate", token);
        CurrentUri = uri;
    }
    public async Task<string> NewTabAsync(Uri? uri, CancellationToken token)
    {
        await CallAsync("new_tab", token);
        var id = Guid.NewGuid().ToString("N");
        for (var index = 0; index < Tabs.Count; index++) Tabs[index] = Tabs[index] with { IsActive = false };
        Tabs.Add(new(id, "New", uri, true));
        return id;
    }
    public async Task SwitchTabAsync(string tabId, CancellationToken token)
    {
        await CallAsync("switch_tab", token);
        for (var index = 0; index < Tabs.Count; index++) Tabs[index] = Tabs[index] with { IsActive = Tabs[index].Id == tabId };
    }
    public async Task CloseTabAsync(string tabId, CancellationToken token)
    {
        await CallAsync("close_tab", token);
        Tabs.RemoveAll(tab => tab.Id == tabId);
        if (Tabs.Count > 0) Tabs[0] = Tabs[0] with { IsActive = true };
    }
    public Task GoBackAsync(CancellationToken token) => CallAsync("back", token);
    public Task GoForwardAsync(CancellationToken token) => CallAsync("forward", token);
    public Task ReloadAsync(CancellationToken token) => CallAsync("reload", token);
    public Task OpenDeveloperToolsAsync(CancellationToken token) => SupportsDeveloperTools
        ? CallAsync("devtools", token) : Task.FromException(new NotSupportedException());
    public Task<string> GetPageTextAsync(CancellationToken token) => Task.FromResult(string.Empty);
    public Task<string> GetSnapshotAsync(CancellationToken token) => Task.FromResult(string.Empty);
    public Task ClickAsync(string selector, CancellationToken token) => CallAsync("click", token);
    public Task TypeAsync(string selector, string text, CancellationToken token) => CallAsync("type", token);
    public Task SelectOptionAsync(string selector, string value, CancellationToken token) => CallAsync("select", token);
    public Task WaitForAsync(string selector, BrowserElementState state, TimeSpan timeout, CancellationToken token) => CallAsync("wait", token);
    public Task ScrollAsync(string? selector, double deltaX, double deltaY, CancellationToken token) => CallAsync("scroll", token);
    public Task<BrowserDownloadInfo> DownloadAsync(string selector, Stream destination, CancellationToken token) =>
        Task.FromException<BrowserDownloadInfo>(new NotSupportedException());
    public Task<byte[]> ScreenshotAsync(bool fullPage, CancellationToken token) => Task.FromResult(Array.Empty<byte>());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
