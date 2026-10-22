using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

public sealed class WindowsExclusiveDirectoryClaimFactory : IExclusiveDirectoryClaimFactory
{
    public IExclusiveDirectoryClaim Create(string path, IReadOnlyList<string> authorizedRoots,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Path.IsPathFullyQualified(path)) throw new UnauthorizedAccessException();
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var parent = WindowsWriteDirectoryLease.Acquire(fullPath, authorizedRoots, cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new WindowsExclusiveDirectoryClaim(fullPath, parent);
        }
        catch
        {
            parent.Dispose();
            throw;
        }
    }
}
