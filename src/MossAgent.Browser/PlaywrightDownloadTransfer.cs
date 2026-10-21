using Microsoft.Playwright;
using MossAgent.Browser.Resources;
using MossAgent.Tools.Abstractions.Browser;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Browser;

internal static class PlaywrightDownloadTransfer
{
    public static async Task<BrowserDownloadInfo> RunAsync(IPage page, string selector, Stream destination, CancellationToken token)
    {
        var download = await page.RunAndWaitForDownloadAsync(() => page.Locator(selector).ClickAsync(),
            new PageRunAndWaitForDownloadOptions { Timeout = 60_000 });
        var complete = false;
        try
        {
            token.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(await download.FailureAsync()))
                throw new InvalidOperationException(BrowserToolText.Get("DownloadFailed"));
            token.ThrowIfCancellationRequested();
            await using var source = await download.CreateReadStreamAsync();
            await source.CopyToAsync(destination, token);
            token.ThrowIfCancellationRequested();
            complete = true;
            return new(download.SuggestedFilename);
        }
        finally { await CleanupAsync(download, !complete); }
    }

    private static async Task CleanupAsync(IDownload download, bool cancel)
    {
        var failed = false;
        if (cancel)
        {
            try { await download.CancelAsync(); }
            catch (Exception) { failed = true; }
        }
        try { await download.DeleteAsync(); }
        catch (Exception) { failed = true; }
        if (failed)
            throw new FileMutationSessionException(BrowserToolText.Get("DownloadCleanupFailed"), "browser_download_cleanup_failed", true);
    }
}
