namespace MossAgent.Tools.Abstractions.Files;

public interface IRootIdentitySessionFactory
{
    Task<IRootIdentitySession> OpenAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken);
}
