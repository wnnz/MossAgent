using MossAgent.Application.Git;

namespace MossAgent.App.Tests;

internal sealed class RepositoryStatusProbe : IGitRepositoryInfoService, IDisposable
{
    public List<RepositoryStatusRead> Reads { get; } = [];
    public bool BlockReads { get; set; } = true;

    public Task<string?> GetBranchNameAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        var read = new RepositoryStatusRead(workingDirectory, cancellationToken);
        Reads.Add(read);
        if (!BlockReads) read.Completion.TrySetResult(null);
        return read.Completion.Task;
    }

    public void Dispose()
    {
        foreach (var read in Reads) read.Dispose();
    }
}
