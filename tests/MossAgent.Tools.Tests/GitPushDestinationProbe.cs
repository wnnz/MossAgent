using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.Tools.Tests;

internal sealed class GitPushDestinationProbe : IGitPushDestinationAuthorization
{
    public Task<ToolGitRepository> AuthorizeAsync(string directory, string targetBranch, ToolExecutionContext context, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Network-only fixture must not authorize a local target.");
}
