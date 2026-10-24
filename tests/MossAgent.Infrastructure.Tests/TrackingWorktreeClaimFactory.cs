using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class TrackingWorktreeClaimFactory(bool failCreate = false, bool failVerify = false)
    : IWorktreeTargetClaimFactory
{
    public List<TrackingWorktreeClaim> Claims { get; } = [];

    public Task<IWorktreeTargetClaim> CreateAsync(string path, CancellationToken cancellationToken)
    {
        if (failCreate) throw new IOException("synthetic already exists");
        var claim = new TrackingWorktreeClaim(path, failVerify);
        Claims.Add(claim);
        return Task.FromResult<IWorktreeTargetClaim>(claim);
    }
}
