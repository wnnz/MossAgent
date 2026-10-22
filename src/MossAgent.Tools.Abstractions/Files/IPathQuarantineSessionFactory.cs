namespace MossAgent.Tools.Abstractions.Files;

public interface IPathQuarantineSessionFactory
{
    IPathQuarantineSession Create(string source, string destination,
        IReadOnlyList<string> authorizedRoots, CancellationToken cancellationToken);
}
