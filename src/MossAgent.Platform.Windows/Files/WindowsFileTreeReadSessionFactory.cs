using MossAgent.Platform.Windows.Resources;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

public sealed class WindowsFileTreeReadSessionFactory : IFileTreeReadSessionFactory
{
    public IFileTreeReadSession Create(string path, IReadOnlyList<string> authorizedRoots, CancellationToken cancellationToken)
    {
        try { return WindowsFileTreeReadSession.Prepare(path, authorizedRoots, cancellationToken); }
        catch (FileMutationSessionException exception) when (exception.ErrorCode == "file_tree_limit") { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FileMutationSessionException(WindowsFileText.Get("CopySourceDenied"), "file_tree_unsafe");
        }
    }
}
