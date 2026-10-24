using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

public sealed class WindowsWorktreeRemovalGuardFactory : IWorktreeRemovalGuardFactory
{
    public Task<IWorktreeRemovalGuard> OpenAsync(string path, DirectoryObjectIdentity expected,
        CancellationToken cancellationToken) =>
        Task.Run<IWorktreeRemovalGuard>(
            () => WindowsWorktreeRemovalGuard.Open(path, expected, cancellationToken), cancellationToken);
}
