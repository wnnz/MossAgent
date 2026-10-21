namespace MossAgent.Tools.Abstractions.Files;

public interface IFileTreeReadSessionFactory
{
    IFileTreeReadSession Create(string path, IReadOnlyList<string> authorizedRoots, CancellationToken cancellationToken);
}
