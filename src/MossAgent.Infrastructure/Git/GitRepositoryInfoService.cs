using MossAgent.Application.Git;

namespace MossAgent.Infrastructure.Git;

public sealed class GitRepositoryInfoService(GitProcessRunner git) : IGitRepositoryInfoService
{
    public async Task<string?> GetBranchNameAsync(
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(workingDirectory))
        {
            return null;
        }

        try
        {
            var result = await git.RunAsync(workingDirectory, ["symbolic-ref", "--quiet", "--short", "HEAD"], cancellationToken).ConfigureAwait(false);
            if (result.ExitCode == 1 && !result.OutputTruncated)
                result = await git.RunAsync(workingDirectory, ["rev-parse", "--abbrev-ref", "HEAD"], cancellationToken).ConfigureAwait(false);
            var branch = result.StandardOutput.Trim();
            return result.ExitCode == 0 && !result.OutputTruncated && branch.Length > 0 ? branch : null;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
