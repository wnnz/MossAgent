using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

public sealed class WindowsPathDeleteSessionFactory : IPathDeleteSessionFactory
{
    public IPathDeleteSession Create(string path, IReadOnlyList<string> authorizedRoots,
        bool recursive, bool fileOnly, CancellationToken cancellationToken)
    {
        try
        {
            path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            if (authorizedRoots.Select(Path.GetFullPath).Any(root =>
                root.Equals(path, StringComparison.OrdinalIgnoreCase)
                || root.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new FileDeleteSessionException(WindowsFileText.DeleteHandleDenied);
            return WindowsPathDeleteSession.Prepare(path, authorizedRoots, recursive, fileOnly, cancellationToken);
        }
        catch (FileDeleteSessionException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FileDeleteSessionException(WindowsFileText.DeleteHandleDenied);
        }
    }
}
