using System.Collections.Concurrent;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Infrastructure.Tests;

internal sealed class RootIdentityProbe : IRootIdentitySessionFactory
{
    private readonly ConcurrentDictionary<string, DirectoryObjectIdentity> _identities = new(StringComparer.OrdinalIgnoreCase);
    private int _opened;
    private int _disposed;
    public int ActiveSessions => Volatile.Read(ref _opened) - Volatile.Read(ref _disposed);
    public Func<IReadOnlyList<string>, CancellationToken, Task<IRootIdentitySession>>? Override { get; set; }
    public DirectoryObjectIdentity Replace(string path) => _identities[path] = new(path, "synthetic-volume", Guid.NewGuid().ToString("N"));

    public Task<IRootIdentitySession> OpenAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Override is { } run) return run(roots, cancellationToken);
        var identities = roots.Select(path => _identities.GetOrAdd(path, value =>
            new DirectoryObjectIdentity(value, "synthetic-volume", Guid.NewGuid().ToString("N")))).ToArray();
        Interlocked.Increment(ref _opened);
        return Task.FromResult<IRootIdentitySession>(new RootIdentityProbeSession(identities, () => Interlocked.Increment(ref _disposed)));
    }
}
