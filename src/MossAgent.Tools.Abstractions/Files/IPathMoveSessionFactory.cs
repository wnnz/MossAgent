namespace MossAgent.Tools.Abstractions.Files;

public interface IPathMoveSessionFactory
{
    IPathMoveSession Create(string source, string destination,
        IReadOnlyList<string> authorizedRoots, CancellationToken cancellationToken);
}
