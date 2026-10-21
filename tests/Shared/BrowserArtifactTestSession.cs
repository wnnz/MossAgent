using System.Text;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Testing;

internal sealed class BrowserArtifactTestSession : IBrowserSession
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public Uri? CurrentUri => null;
    public byte[] ScreenshotBytes { get; set; } = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC");
    public string SuggestedName { get; set; } = "report.txt";
    public Func<Stream, CancellationToken, Task>? Transfer { get; set; }
    public Func<CancellationToken, Task>? Capture { get; set; }
    public int Captures { get; private set; }
    public int Downloads { get; private set; }
    public bool FullPage { get; private set; }
    public bool IsDisposed { get; private set; }

    public async Task<byte[]> ScreenshotAsync(bool fullPage, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Captures++;
        FullPage = fullPage;
        if (Capture is not null) await Capture(token);
        return ScreenshotBytes;
    }

    public async Task<BrowserDownloadInfo> DownloadAsync(string selector, Stream destination, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Downloads++;
        if (Transfer is null) await destination.WriteAsync(Encoding.UTF8.GetBytes("fixture download"), token);
        else await Transfer(destination, token);
        return new(SuggestedName);
    }

    public Task<IReadOnlyList<BrowserTabInfo>> ListTabsAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<BrowserTabInfo>>([]);
    public Task<string> NewTabAsync(Uri? uri, CancellationToken token) => throw new NotSupportedException();
    public Task SwitchTabAsync(string tabId, CancellationToken token) => throw new NotSupportedException();
    public Task CloseTabAsync(string tabId, CancellationToken token) => throw new NotSupportedException();
    public Task NavigateAsync(Uri uri, CancellationToken token) => throw new NotSupportedException();
    public Task GoBackAsync(CancellationToken token) => throw new NotSupportedException();
    public Task GoForwardAsync(CancellationToken token) => throw new NotSupportedException();
    public Task ReloadAsync(CancellationToken token) => throw new NotSupportedException();
    public Task<string> GetPageTextAsync(CancellationToken token) => Task.FromResult("");
    public Task<string> GetSnapshotAsync(CancellationToken token) => Task.FromResult("");
    public Task ClickAsync(string selector, CancellationToken token) => throw new NotSupportedException();
    public Task TypeAsync(string selector, string text, CancellationToken token) => throw new NotSupportedException();
    public Task SelectOptionAsync(string selector, string value, CancellationToken token) => throw new NotSupportedException();
    public Task WaitForAsync(string selector, BrowserElementState state, TimeSpan timeout, CancellationToken token) => throw new NotSupportedException();
    public Task ScrollAsync(string? selector, double x, double y, CancellationToken token) => throw new NotSupportedException();
    public ValueTask DisposeAsync() { IsDisposed = true; return ValueTask.CompletedTask; }
}
