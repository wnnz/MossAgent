using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class TestGitWorktreeRemovalFactory :
    IPathDeleteSessionFactory, IPathQuarantineSessionFactory
{
    public IPathDeleteSession Create(string path, IReadOnlyList<string> authorizedRoots,
        bool recursive, bool fileOnly, CancellationToken cancellationToken) =>
        new TestGitWorktreeDeleteSession(path, authorizedRoots, recursive, fileOnly);

    public IPathQuarantineSession Create(string source, string destination,
        IReadOnlyList<string> authorizedRoots, CancellationToken cancellationToken) =>
        new TestGitWorktreeQuarantineSession(source, destination, authorizedRoots);
}
