using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

internal sealed class WindowsArtifactFileSession(string path, IAtomicFileWriteSession inner,
    ToolExecutionContext context) : IArtifactFileSession
{
    private bool _complete;
    private bool _retained;
    private bool _disposed;
    public string Path => path;
    public FileStream Stream => inner.Stream;

    public async Task CompleteAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_complete) throw new InvalidOperationException();
        await context.ValidateWorkspaceAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!await inner.TryCommitAsync(null, cancellationToken).ConfigureAwait(false))
            throw new FileMutationSessionException(WindowsFileText.Get("ArtifactConflict"), "artifact_write_conflict");
        _complete = true;
    }

    public void Retain()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_complete) throw new InvalidOperationException();
        _retained = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var cleanupFailed = false;
        try
        {
            if (!_retained)
            {
                try { WindowsRelativeFile.DeleteOnClose(Stream.SafeFileHandle); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                { cleanupFailed = true; }
            }
        }
        finally
        {
            try { inner.Dispose(); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            { cleanupFailed = true; }
        }
        if (cleanupFailed)
            throw new FileMutationSessionException(WindowsFileText.Get("ArtifactCleanupFailed"), "artifact_cleanup_failed", true);
    }
}
