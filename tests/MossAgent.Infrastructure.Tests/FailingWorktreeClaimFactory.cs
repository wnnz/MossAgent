using MossAgent.Application.Git;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class FailingWorktreeClaimFactory : IWorktreeTargetClaimFactory
{
    public Task<IWorktreeTargetClaim> CreateAsync(string target, CancellationToken token) =>
        throw new WorktreeAuthorizationException();
}
