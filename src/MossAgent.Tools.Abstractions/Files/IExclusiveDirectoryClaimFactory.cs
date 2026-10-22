namespace MossAgent.Tools.Abstractions.Files;

public interface IExclusiveDirectoryClaimFactory
{
    IExclusiveDirectoryClaim Create(string path, IReadOnlyList<string> authorizedRoots,
        CancellationToken cancellationToken);
}
