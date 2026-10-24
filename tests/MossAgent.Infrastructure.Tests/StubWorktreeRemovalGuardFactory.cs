using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class StubWorktreeRemovalGuardFactory(Exception? openFailure = null,
    Exception? confirmFailure = null) : IWorktreeRemovalGuardFactory
{
    public List<StubWorktreeRemovalGuard> Guards { get; } = [];

    public Task<IWorktreeRemovalGuard> OpenAsync(string path, DirectoryObjectIdentity expected,
        CancellationToken cancellationToken)
    {
        if (openFailure is not null) throw openFailure;
        var guard = new StubWorktreeRemovalGuard(path, expected, confirmFailure);
        Guards.Add(guard);
        return Task.FromResult<IWorktreeRemovalGuard>(guard);
    }
}
