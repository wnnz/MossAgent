using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

public sealed class WindowsPathMoveSessionFactory : IPathMoveSessionFactory
{
    public IPathMoveSession Create(string source, string destination,
        IReadOnlyList<string> authorizedRoots, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Path.IsPathFullyQualified(source) || !Path.IsPathFullyQualified(destination))
            throw new UnauthorizedAccessException(WindowsFileText.Get("MoveDenied"));
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        if (!string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(destination), StringComparison.OrdinalIgnoreCase))
            throw new FileMutationSessionException(WindowsFileText.Get("MoveCrossVolume"), "file_move_cross_volume");
        WindowsWriteDirectoryLease? directory = null;
        try
        {
            directory = WindowsWriteDirectoryLease.Acquire(source, authorizedRoots, cancellationToken, false);
            return new WindowsPathMoveSession(source, destination, authorizedRoots.ToArray(), directory, cancellationToken);
        }
        catch (Exception exception)
        {
            directory?.Dispose();
            if (exception is FileWriteSessionException or IOException or UnauthorizedAccessException)
                throw new FileMutationSessionException(WindowsFileText.Get("MoveDenied"), "file_move_unsafe");
            throw;
        }
    }
}
