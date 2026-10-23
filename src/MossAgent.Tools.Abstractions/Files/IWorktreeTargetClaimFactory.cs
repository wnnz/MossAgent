namespace MossAgent.Tools.Abstractions.Files;

public interface IWorktreeTargetClaimFactory
{
    Task<IWorktreeTargetClaim> CreateAsync(string path, CancellationToken cancellationToken);
}
