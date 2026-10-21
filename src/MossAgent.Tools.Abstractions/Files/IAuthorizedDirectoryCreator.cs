namespace MossAgent.Tools.Abstractions.Files;

public interface IAuthorizedDirectoryCreator
{
    Task<int> CreateAsync(string path, IReadOnlyList<string> authorizedRoots,
        Func<CancellationToken, Task> validateAuthorization, CancellationToken cancellationToken);
    Task<IAuthorizedDirectoryLease> AcquireAsync(string path, IReadOnlyList<string> authorizedRoots,
        Func<CancellationToken, Task> validateAuthorization, CancellationToken cancellationToken);
}
