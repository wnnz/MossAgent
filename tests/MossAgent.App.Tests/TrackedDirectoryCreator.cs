using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.Tests;

internal sealed class TrackedDirectoryCreator(bool refuse) : IAuthorizedDirectoryCreator
{
    public int Calls { get; private set; }
    public Task<int> CreateAsync(string path, IReadOnlyList<string> authorizedRoots,
        Func<CancellationToken, Task> validateAuthorization, CancellationToken cancellationToken)
    {
        Calls++;
        if (refuse) throw new FileMutationSessionException("synthetic safe directory refusal", "directory_create_unsafe");
        return new WindowsAuthorizedDirectoryCreator().CreateAsync(path, authorizedRoots, validateAuthorization, cancellationToken);
    }

    public Task<IAuthorizedDirectoryLease> AcquireAsync(string path, IReadOnlyList<string> authorizedRoots,
        Func<CancellationToken, Task> validateAuthorization, CancellationToken cancellationToken)
    {
        Calls++;
        if (refuse) throw new FileMutationSessionException("synthetic safe directory refusal", "directory_create_unsafe");
        return new WindowsAuthorizedDirectoryCreator().AcquireAsync(path, authorizedRoots, validateAuthorization, cancellationToken);
    }
}
