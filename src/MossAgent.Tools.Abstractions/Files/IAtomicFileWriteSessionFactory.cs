namespace MossAgent.Tools.Abstractions.Files;

/// <summary>Creates a staging file in a pinned, authorized parent directory.</summary>
public interface IAtomicFileWriteSessionFactory
{
    IAtomicFileWriteSession Create(
        string path, IReadOnlyList<string> authorizedRoots, CancellationToken cancellationToken);
}
