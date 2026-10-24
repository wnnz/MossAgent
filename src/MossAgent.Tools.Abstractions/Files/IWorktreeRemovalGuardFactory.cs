using MossAgent.Domain;

namespace MossAgent.Tools.Abstractions.Files;

public interface IWorktreeRemovalGuardFactory
{
    Task<IWorktreeRemovalGuard> OpenAsync(string path, DirectoryObjectIdentity expected,
        CancellationToken cancellationToken);
}
