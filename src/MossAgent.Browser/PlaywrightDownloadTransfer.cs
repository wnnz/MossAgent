using Microsoft.Playwright;
using MossAgent.Browser.Artifacts;
using MossAgent.Browser.Resources;
using MossAgent.Tools.Abstractions.Browser;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Browser;

internal static class PlaywrightDownloadTransfer
{
    public static async Task<BrowserDownloadInfo> RunAsync(IPage page, string selector, Stream destination,
        IArtifactSessionJournal? journal, Guid? taskId, CancellationToken token)
    {
        var download = await page.RunAndWaitForDownloadAsync(() => page.Locator(selector).ClickAsync(),
            new PageRunAndWaitForDownloadOptions { Timeout = 60_000 });
        long? record = null;
        var complete = false;
        try
        {
            token.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(await download.FailureAsync()))
                throw new InvalidOperationException(BrowserToolText.Get("DownloadFailed"));
            token.ThrowIfCancellationRequested();
            if (await ResolveSdkPathAsync(download) is { } path)
            {
                record = await RecordSdkTempAsync(journal, taskId, path, token);
                ThrowIfOversized(path);
            }
            await using var source = await download.CreateReadStreamAsync();
            await source.CopyToAsync(destination, token);
            token.ThrowIfCancellationRequested();
            complete = true;
            return new(download.SuggestedFilename);
        }
        finally { await CleanupAsync(download, !complete, journal, record); }
    }

    // PathAsync may return null or throw when the SDK cannot expose a temp path;
    // without a path there is nothing to journal and the stream is used as-is.
    private static async Task<string?> ResolveSdkPathAsync(IDownload download)
    {
        string? path;
        try { path = await download.PathAsync().ConfigureAwait(false); }
        catch (Exception) { return null; }
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    // Recording is fail-closed: a journal failure aborts the transfer via the cleanup path.
    private static async Task<long?> RecordSdkTempAsync(IArtifactSessionJournal? journal,
        Guid? taskId, string path, CancellationToken token)
    {
        if (journal is null || taskId is not { } id) return null;
        return await journal.RecordAsync(id, Path.GetFileName(path), path,
            ArtifactSessionKind.ExternalSdkTemp, null, token).ConfigureAwait(false);
    }

    // The resolved SDK temp size is the only honest bound: Playwright exposes no
    // mid-flight byte count before CreateReadStreamAsync.
    private static void ThrowIfOversized(string path)
    {
        var temp = new FileInfo(path);
        if (temp.Exists && temp.Length > BrowserArtifactLimits.MaximumBytes)
            throw new FileMutationSessionException(BrowserToolText.Get("ArtifactTooLarge"), "browser_artifact_too_large");
    }

    private static async Task CleanupAsync(IDownload download, bool cancel,
        IArtifactSessionJournal? journal, long? record)
    {
        var failed = false;
        if (cancel)
        {
            try { await download.CancelAsync(); }
            catch (Exception) { failed = true; }
        }
        try { await download.DeleteAsync(); }
        catch (Exception) { failed = true; }
        await MarkAsync(journal, record, failed).ConfigureAwait(false);
        if (failed)
            throw new FileMutationSessionException(BrowserToolText.Get("DownloadCleanupFailed"), "browser_download_cleanup_failed", true);
    }

    // Journal marks are best-effort and must never mask the cleanup result.
    private static async Task MarkAsync(IArtifactSessionJournal? journal, long? record, bool cleanupFailed)
    {
        if (journal is null || record is not { } id) return;
        try
        {
            if (cleanupFailed)
                await journal.MarkCleanupUnknownAsync(id, "browser_download_cleanup_failed", CancellationToken.None).ConfigureAwait(false);
            else
                await journal.MarkDiscardedAsync(id, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception) { }
    }
}
