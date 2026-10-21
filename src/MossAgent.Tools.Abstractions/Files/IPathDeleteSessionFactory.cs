namespace MossAgent.Tools.Abstractions.Files;

public interface IPathDeleteSessionFactory
{
    // Preparation must not mutate the tree. A failure must not fall back to path-based deletion.
    IPathDeleteSession Create(string path, IReadOnlyList<string> authorizedRoots,
        bool recursive, bool fileOnly, CancellationToken cancellationToken);
}
