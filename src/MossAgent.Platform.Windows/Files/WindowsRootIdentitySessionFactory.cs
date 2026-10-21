using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

public sealed class WindowsRootIdentitySessionFactory : IRootIdentitySessionFactory
{
    public Task<IRootIdentitySession> OpenAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        var snapshot = roots.ToArray();
        return Task.Run<IRootIdentitySession>(() => new WindowsRootIdentitySession(snapshot, cancellationToken), cancellationToken);
    }
}
