using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.Tests;

internal sealed class TrackedFileTreeSessions(bool refuse) : IFileTreeReadSessionFactory
{
    public int Creates { get; private set; }
    public IFileTreeReadSession Create(string path, IReadOnlyList<string> authorizedRoots, CancellationToken cancellationToken)
    {
        Creates++;
        if (refuse) throw new FileMutationSessionException("synthetic safe source refusal", "file_tree_unsafe");
        return new WindowsFileTreeReadSessionFactory().Create(path, authorizedRoots, cancellationToken);
    }
}
