using System.Diagnostics;
using MossAgent.Application.Git;

namespace MossAgent.Infrastructure.Git;

public sealed class GitRepositoryInfoService : IGitRepositoryInfoService
{
    public async Task<string?> GetBranchNameAsync(
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(workingDirectory))
        {
            return null;
        }

        try
        {
            var startInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("rev-parse");
            startInfo.ArgumentList.Add("--abbrev-ref");
            startInfo.ArgumentList.Add("HEAD");
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var branch = (await output).Trim();
            return process.ExitCode == 0 && branch.Length > 0 ? branch : null;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
