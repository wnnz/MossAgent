using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

public sealed class WindowsWorktreeTargetClaimFactory : IWorktreeTargetClaimFactory
{
    public Task<IWorktreeTargetClaim> CreateAsync(string path, CancellationToken cancellationToken) =>
        Task.Run<IWorktreeTargetClaim>(() => WindowsWorktreeTargetClaim.Create(path, cancellationToken), cancellationToken);
}
