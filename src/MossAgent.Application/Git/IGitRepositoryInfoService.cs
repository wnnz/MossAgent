namespace MossAgent.Application.Git;

public interface IGitRepositoryInfoService
{
    Task<string?> GetBranchNameAsync(
        string workingDirectory,
        CancellationToken cancellationToken);
}
