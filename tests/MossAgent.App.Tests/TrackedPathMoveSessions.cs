using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.Tests;

internal sealed class TrackedPathMoveSessions(bool refuse) : IPathMoveSessionFactory
{
    public int Creates { get; private set; }
    public IPathMoveSession Create(string source, string destination, IReadOnlyList<string> authorizedRoots, CancellationToken cancellationToken)
    {
        Creates++;
        if (refuse) throw new FileMutationSessionException("synthetic safe move refusal", "file_move_unsafe");
        return new WindowsPathMoveSessionFactory().Create(source, destination, authorizedRoots, cancellationToken);
    }
}
