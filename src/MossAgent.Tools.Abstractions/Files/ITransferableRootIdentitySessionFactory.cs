namespace MossAgent.Tools.Abstractions.Files;

/// <summary>
/// Opens a single root identity handle that can be transferred to a quarantining
/// move without releasing and reopening the path.
/// </summary>
public interface ITransferableRootIdentitySessionFactory : IRootIdentitySessionFactory
{
    Task<IRootIdentitySession> OpenTransferableAsync(
        IReadOnlyList<string> roots, CancellationToken cancellationToken);
}
