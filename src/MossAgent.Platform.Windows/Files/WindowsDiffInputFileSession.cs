using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal sealed class WindowsDiffInputFileSession(string path, FileStream stream,
    WindowsWriteDirectoryLease directory, IArtifactSessionJournal? journal = null, long? record = null)
    : IDiffInputFileSession
{
    private bool _disposed;
    public string Path => path;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var cleanupFailed = false;
        try
        {
            try { WindowsRelativeFile.DeleteOnClose(stream.SafeFileHandle); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            { cleanupFailed = true; }
        }
        finally
        {
            try { stream.Dispose(); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            { cleanupFailed = true; }
            finally { directory.Dispose(); }
        }
        if (cleanupFailed) TryMark(id => journal!.MarkCleanupUnknownAsync(id, "diff_input_cleanup_failed", CancellationToken.None));
        else TryMark(id => journal!.MarkDiscardedAsync(id, CancellationToken.None));
        if (cleanupFailed)
            throw new FileMutationSessionException(WindowsFileText.Get("DiffInputCleanupFailed"), "diff_input_cleanup_failed", true);
    }

    // Journal stage transitions are best-effort: a failed mark must never mask the file operation.
    private void TryMark(Func<long, Task> mark)
    {
        if (journal is null || record is not { } id) return;
        try { mark(id).GetAwaiter().GetResult(); }
        catch (Exception) { }
    }
}
