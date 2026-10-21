using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

public sealed class WindowsAtomicFileWriteSessionFactory : IAtomicFileWriteSessionFactory
{
    public IAtomicFileWriteSession Create(
        string path, IReadOnlyList<string> authorizedRoots, CancellationToken cancellationToken)
    {
        var directory = WindowsWriteDirectoryLease.Acquire(path, authorizedRoots, cancellationToken);
        try { return new WindowsAtomicFileWriteSession(Path.GetFullPath(path), directory, cancellationToken); }
        catch { directory.Dispose(); throw; }
    }
}
