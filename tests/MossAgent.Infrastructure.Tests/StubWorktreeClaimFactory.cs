using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class StubWorktreeClaimFactory : IWorktreeTargetClaimFactory
{
    public Task<IWorktreeTargetClaim> CreateAsync(string path, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(path);
        return Task.FromResult<IWorktreeTargetClaim>(new StubWorktreeClaim(path));
    }
}
