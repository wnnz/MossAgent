using MossAgent.Browser.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Browser;

internal sealed class PlaywrightBrowserOperation(Func<ValueTask> closeSession)
{
    private int _stopped;

    public void Stop() => Interlocked.Exchange(ref _stopped, 1);

    public Task RunAsync(Func<Task> action, CancellationToken cancellationToken) =>
        RunAsync(async () => { await action().ConfigureAwait(false); return true; }, cancellationToken);

    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _stopped) != 0, this);
        var pending = action();
        try
        {
            var result = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Stop();
            var closeFailed = false;
            try { await closeSession().ConfigureAwait(false); }
            catch (Exception) { closeFailed = true; }
            finally { await ((Task)pending).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing); }
            if (pending.Exception?.InnerExceptions.OfType<FileMutationSessionException>()
                .FirstOrDefault(exception => exception.HasSideEffects) is { } cleanupFailure)
                throw closeFailed ? new FileMutationSessionException(cleanupFailure.Message + " " + BrowserToolText.Get("SessionCleanupFailed"),
                    cleanupFailure.ErrorCode, true) : cleanupFailure;
            if (closeFailed)
                throw new FileMutationSessionException(BrowserToolText.Get("SessionCleanupFailed"), "browser_session_cleanup_failed", true);
            throw;
        }
    }
}
