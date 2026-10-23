using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Authorization;

internal sealed class ProjectRootSessionLifetime
{
    private readonly Lock _sync = new();
    private readonly HashSet<TrackedRootIdentitySession> _sessions = [];
    private bool _stopping;

    internal bool HasOutstanding { get { lock (_sync) return _sessions.Count > 0; } }

    internal IRootIdentitySessionFactory Track(IRootIdentitySessionFactory factory) =>
        factory is ITransferableRootIdentitySessionFactory transfers
            ? new TrackedTransferableRootIdentityFactory(this, transfers)
            : new TrackedRootIdentityFactory(this, factory);

    internal Task<IRootIdentitySession> OpenAsync(IRootIdentitySessionFactory factory,
        IReadOnlyList<string> roots, CancellationToken cancellationToken, bool transferable = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TrackedRootIdentitySession session;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_stopping, this);
            session = new(Release);
            _sessions.Add(session);
        }
        return session.OpenAsync(factory, roots.ToArray(), cancellationToken, transferable);
    }

    internal void StopAccepting() { lock (_sync) _stopping = true; }

    internal Task DrainAsync()
    {
        lock (_sync)
        {
            _stopping = true;
            return Task.WhenAll(_sessions.Select(session => session.Released));
        }
    }

    private void Release(TrackedRootIdentitySession session)
    {
        lock (_sync) _sessions.Remove(session);
    }
}
