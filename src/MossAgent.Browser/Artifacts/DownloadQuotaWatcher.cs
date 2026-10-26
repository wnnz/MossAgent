namespace MossAgent.Browser.Artifacts;

/// <summary>
/// Polls a still-growing SDK temp file while a download is in flight and asks the SDK
/// to cancel once the file exceeds the artifact limit. Playwright cannot cap bytes on
/// the wire, so this is best-effort: <c>IDownload.PathAsync</c> may not resolve until
/// the download finishes (then nothing is enforced mid-flight and the post-wait size
/// check plus the bounded artifact copy remain the bounds). The loop never throws for
/// path, size, or cancel failures and stops when the download wait completes or its
/// token cancels; it never blocks or outlives the transfer that owns it.
/// </summary>
internal sealed class DownloadQuotaWatcher
{
    private readonly Func<Task<string?>> _resolvePathAsync;
    private readonly Func<string, long> _lengthOf;
    private readonly Func<Task> _cancelAsync;
    private readonly Func<bool> _isDone;
    private readonly TimeSpan _interval;
    private readonly long _maximumBytes;
    private volatile bool _quotaExceeded;
    private volatile string? _resolvedPath;

    public DownloadQuotaWatcher(Func<Task<string?>> resolvePathAsync, Func<string, long> lengthOf,
        Func<Task> cancelAsync, Func<bool> isDone, TimeSpan interval, long maximumBytes)
    {
        _resolvePathAsync = resolvePathAsync;
        _lengthOf = lengthOf;
        _cancelAsync = cancelAsync;
        _isDone = isDone;
        _interval = interval;
        _maximumBytes = maximumBytes;
    }

    /// <summary>Set before the cancel is attempted so the caller can translate the
    /// outcome to the quota error deterministically even when the cancel is lost.</summary>
    public bool QuotaExceeded => _quotaExceeded;

    /// <summary>The temp path observed while polling; null when it never resolved.</summary>
    public string? ResolvedPath => _resolvedPath;

    public async Task RunAsync(CancellationToken token)
    {
        Task<string?>? pending = null;
        while (!_isDone())
        {
            // Interval-first: the download wait stays authoritative, and a fast transfer
            // never pays a poll before its own checks run.
            await Task.Delay(_interval, token).ConfigureAwait(false);
            if (_isDone()) return;
            if (_resolvedPath is null)
            {
                // A thrown or not-yet-completing resolution means "no path yet": keep the
                // single outstanding call and look again on the next tick.
                pending ??= TryResolve();
                if (pending is { IsCompleted: true })
                {
                    if (pending.Status == TaskStatus.RanToCompletion
                        && !string.IsNullOrWhiteSpace(pending.Result))
                        _resolvedPath = pending.Result;
                    pending = null;
                }
            }
            if (_resolvedPath is { } path && Oversized(path))
            {
                _quotaExceeded = true;
                try { await _cancelAsync().ConfigureAwait(false); }
                catch (Exception) { /* The shared cleanup path retries the cancel. */ }
                return;
            }
        }
    }

    private Task<string?>? TryResolve()
    {
        try { return _resolvePathAsync(); }
        catch (Exception) { return null; }
    }

    private bool Oversized(string path)
    {
        try { return _lengthOf(path) > _maximumBytes; }
        catch (Exception) { return false; }
    }
}
