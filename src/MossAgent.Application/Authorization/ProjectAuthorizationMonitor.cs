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
    private readonly IAuthorizationResourceRegistry? _resources;
    private bool _disposed;
    private Task? _disposal;

    public ProjectAuthorizationMonitor(IProjectAuthorizationReader reader, IProjectAuthorizationChanges changes,
        TimeProvider? timeProvider = null, IRootIdentitySessionFactory? roots = null,
        IAuthorizationResourceRegistry? resources = null)
    {
        _reader = reader;
        _changes = changes;
        _time = timeProvider ?? TimeProvider.System;
        _roots = roots;
        _resources = resources;
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
        var lease = new ProjectAuthorizationLease(this, snapshot, _time, roots, _resources);
        try
        {
            await lease.RegisterAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task<ProjectAuthorizationDrainResult> WaitForQuiescenceAsync(
        Guid projectId,
        long maximumRevision,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        ProjectAuthorizationLease[] leases;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            leases = _leases.Where(lease => lease.ProjectId == projectId && lease.Revision <= maximumRevision).ToArray();
        }
        foreach (var lease in leases) lease.Revoke();
        using var deadline = new CancellationTokenSource(timeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var outstanding = 0;
        try
        {
            if (leases.Length > 0)
                await Task.WhenAll(leases.Select(lease => lease.Released))
                    .WaitAsync(linked.Token).ConfigureAwait(false);
            outstanding = leases.Count(lease => !lease.HasExited);
            if (outstanding > 0 || _resources is null) return new(outstanding == 0, outstanding);

            while (true)
            {
                outstanding = await _resources.CountOutstandingAsync(
                    projectId, maximumRevision, linked.Token).ConfigureAwait(false);
                if (outstanding == 0) return new(true, 0);
                await Task.Delay(TimeSpan.FromMilliseconds(100), _time, linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { }
        catch (Exception) { outstanding = Math.Max(1, outstanding); }
        outstanding = Math.Max(outstanding, leases.Count(lease => !lease.HasExited));
        if (_resources is not null)
        {
            using var registryDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(2), _time);
            try
            {
                outstanding = Math.Max(outstanding, await _resources.CountOutstandingAsync(
                    projectId, maximumRevision, registryDeadline.Token).ConfigureAwait(false));
            }
            catch { outstanding = Math.Max(1, outstanding); }
        }
        return new(outstanding == 0, outstanding);
    }

    public async Task<AuthorizationResourceInspection> InspectResourcesAsync(
        Guid projectId,
        TimeSpan staleAfter,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("Invalid project identity.", nameof(projectId));
        if (staleAfter <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(staleAfter));
        if (_resources is null) return AuthorizationResourceInspection.Empty;
        var outstanding = await _resources.ListOutstandingAsync(projectId, cancellationToken).ConfigureAwait(false);
        var staleBefore = _time.GetUtcNow() - staleAfter;
        var stale = outstanding.Where(item => item.HeartbeatAt <= staleBefore)
            .Select(item => item.LeaseId).ToArray();
        return new(outstanding, stale);
    }

    public async Task<int> AbandonStaleResourcesAsync(
        Guid projectId,
        IReadOnlyCollection<Guid> leaseIds,
        TimeSpan staleAfter,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("Invalid project identity.", nameof(projectId));
        if (staleAfter <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(staleAfter));
        if (_resources is null) throw new AuthorizationResourceChangedException();
        var ids = leaseIds.Distinct().ToArray();
        if (ids.Length is < 1 or > 128 || ids.Any(id => id == Guid.Empty))
            throw new ArgumentException("Invalid authorization resource confirmation.", nameof(leaseIds));
        var staleBefore = _time.GetUtcNow() - staleAfter;
        var abandoned = await _resources.AbandonStaleAsync(
            projectId, ids, staleBefore, cancellationToken).ConfigureAwait(false);
        if (abandoned != ids.Length) throw new AuthorizationResourceChangedException();
        return abandoned;
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
