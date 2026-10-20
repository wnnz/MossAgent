using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.Tools.Tests;

internal sealed class GitIndexRepositoryProbe : IGitRepositoryAuthorization
{
    public int Calls { get; private set; }
    public Func<string, CancellationToken, Task<ToolGitRepository>>? Authorize { get; set; }

    public Task<ToolGitRepository> AuthorizeAsync(string directory, ToolExecutionContext context, CancellationToken token)
    {
        Calls++;
        return Authorize?.Invoke(directory, token) ?? Task.FromResult(new ToolGitRepository(directory,
            Path.Combine(directory, ".git"), Path.Combine(directory, ".git")));
    }
}
