using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.Tests;

internal sealed class TrackedPathDeleteSessions(bool refuse) : IPathDeleteSessionFactory
{
    public int Creates { get; private set; }
    public IPathDeleteSession Create(string path, IReadOnlyList<string> authorizedRoots,
        bool recursive, bool fileOnly, CancellationToken cancellationToken)
    {
        Creates++;
        if (refuse) throw new FileDeleteSessionException("synthetic safe deletion refusal");
        return new WindowsPathDeleteSessionFactory().Create(path, authorizedRoots, recursive, fileOnly, cancellationToken);
    }
}
