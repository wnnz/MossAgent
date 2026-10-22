using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

public sealed class WindowsRootIdentitySessionFactory : ITransferableRootIdentitySessionFactory
{
    public Task<IRootIdentitySession> OpenAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        var snapshot = roots.ToArray();
        return Task.Run<IRootIdentitySession>(
            () => new WindowsRootIdentitySession(snapshot, transferable: false, cancellationToken),
            cancellationToken);
    }

    public Task<IRootIdentitySession> OpenTransferableAsync(
        IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        var snapshot = roots.ToArray();
        if (snapshot.Length != 1)
            throw new ArgumentException("Exactly one transferable root is supported.", nameof(roots));
        return Task.Run<IRootIdentitySession>(
            () => new WindowsRootIdentitySession(snapshot, transferable: true, cancellationToken),
            cancellationToken);
    }
}
