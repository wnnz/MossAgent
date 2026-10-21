using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Authorization;

public sealed class ProjectAuthorizationMonitor : IAsyncDisposable
{
    private readonly Lock _sync = new();
    private readonly HashSet<ProjectAuthorizationLease> _leases = [];
    private readonly IProjectAuthorizationReader _reader;
    private readonly IProjectAuthorizationChanges _changes;
    private readonly TimeProvider _time;
    private readonly IRootIdentitySessionFactory? _roots;
    private bool _disposed;
    private Task? _disposal;

    public ProjectAuthorizationMonitor(IProjectAuthorizationReader reader, IProjectAuthorizationChanges changes,
        TimeProvider? timeProvider = null, IRootIdentitySessionFactory? roots = null)
    {
        _reader = reader;
        _changes = changes;
        _time = timeProvider ?? TimeProvider.System;
        _roots = roots;
        changes.Changed += RevokeOlder;
    }

    public async Task ValidateAsync(ProjectProfile expected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync) ObjectDisposedException.ThrowIf(_disposed, this);
        var current = await _reader.ReadAuthorizationAsync(expected.Id, cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (expected.AuthorizationRevision <= 0 || current is null || current.AuthorizationRevision != expected.AuthorizationRevision
            || !comparer.Equals(current.PrimaryDirectory, expected.PrimaryDirectory)
            || !current.AuthorizedDirectories.SequenceEqual(expected.AuthorizedDirectories, comparer)
            || !current.RootAuthorizations.SequenceEqual(expected.RootAuthorizations))
        {
            if (current is not null && current.AuthorizationRevision > expected.AuthorizationRevision)
                _changes.Publish(expected.Id, current.AuthorizationRevision);
            RevokeMatching(expected.Id, expected.AuthorizationRevision);
            throw new WorkspaceAuthorizationException();
        }
        if (_roots is not null)
        {
            try { using var verified = await ProjectRootObjectValidation.OpenAsync(_roots, expected, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch { RevokeMatching(expected.Id, expected.AuthorizationRevision); throw new WorkspaceAuthorizationException(); }
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task<ITaskWorkspaceLease> AcquireAsync(ProjectProfile expected, CancellationToken cancellationToken)
    {
        var snapshot = expected with { AuthorizedDirectories = expected.AuthorizedDirectories.ToArray(), RootAuthorizations = expected.RootAuthorizations.ToArray() };
        await ValidateAsync(snapshot, cancellationToken).ConfigureAwait(false);
        var roots = _roots is null ? null : await ProjectRootObjectValidation.OpenAsync(_roots, snapshot, cancellationToken).ConfigureAwait(false);
        var lease = new ProjectAuthorizationLease(this, snapshot, _time, roots);
        try
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _leases.Add(lease);
            }
            await ValidateAsync(snapshot, cancellationToken).ConfigureAwait(false);
            lease.Start();
            if (lease.IsRevoked) throw new WorkspaceAuthorizationException();
            return lease;
        }
        catch { await lease.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private void RevokeOlder(Guid projectId, long revision)
    {
        ProjectAuthorizationLease[] leases;
        lock (_sync) leases = _leases.Where(lease => lease.ProjectId == projectId && lease.Revision < revision).ToArray();
        foreach (var lease in leases) lease.Revoke();
    }

    private void RevokeMatching(Guid projectId, long revision)
    {
        ProjectAuthorizationLease[] leases;
        lock (_sync) leases = _leases.Where(lease => lease.ProjectId == projectId && lease.Revision == revision).ToArray();
        foreach (var lease in leases) lease.Revoke();
    }

    internal void Release(ProjectAuthorizationLease lease)
    {
        lock (_sync) _leases.Remove(lease);
    }

    public ValueTask DisposeAsync()
    {
        ProjectAuthorizationLease[] leases;
        TaskCompletionSource completion;
        lock (_sync)
        {
            if (_disposal is not null) return new(_disposal);
            _disposed = true;
            leases = _leases.ToArray();
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposal = completion.Task;
        }
        _changes.Changed -= RevokeOlder;
        _ = DisposeOwnedAsync(leases, completion);
        return new(completion.Task);
    }

    private static async Task DisposeOwnedAsync(ProjectAuthorizationLease[] leases, TaskCompletionSource completion)
    {
        try
        {
            foreach (var lease in leases) lease.Revoke();
            await Task.WhenAll(leases.Select(lease => lease.DisposeAsync().AsTask())).ConfigureAwait(false);
            completion.SetResult();
        }
        catch (Exception exception) { completion.SetException(exception); }
    }
}
