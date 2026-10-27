using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Authorization;

public sealed class ProjectAuthorizationMonitor : IAsyncDisposable
{
    private readonly Lock _sync = new();
    private readonly HashSet<ProjectAuthorizationLease> _leases = [];
    private readonly Dictionary<Guid, long> _highest = [];
    private readonly Dictionary<Guid, AuthorizationDrainState> _drainStates = [];
    private readonly Dictionary<Guid, AuthorizationDrainState> _published = [];
    private readonly IProjectAuthorizationReader _reader;
    private readonly IProjectAuthorizationChanges _changes;
    private readonly TimeProvider _time;
    private readonly IRootIdentitySessionFactory? _roots;
    private readonly IProjectDrainStateBoard? _board;
    private Task _publishTail = Task.CompletedTask;
    private bool _disposed;
    private Task? _disposal;

    /// <summary>Cumulative bound for a revoked lease to finish draining local resources;
    /// exceeding it marks the lease release-failed (cleanup unknown) and retains the admission barrier.</summary>
    public static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Settle delay before the single retry of a failed root-session release —
    /// gives the OS a moment to finish closing transient resources; still under DrainTimeout.</summary>
    public static readonly TimeSpan RootReleaseRetryDelay = TimeSpan.FromSeconds(2);

    /// <summary>Shared-board rows untouched longer than this window count as CleanupUnknown:
    /// their owner instance is unreachable, so its cleanup cannot be confirmed finished.</summary>
    public static readonly TimeSpan BoardStaleWindow = TimeSpan.FromSeconds(120);

    public ProjectAuthorizationMonitor(IProjectAuthorizationReader reader, IProjectAuthorizationChanges changes,
        TimeProvider? timeProvider = null, IRootIdentitySessionFactory? roots = null,
        IProjectDrainStateBoard? board = null)
    {
        _reader = reader;
        _changes = changes;
        _time = timeProvider ?? TimeProvider.System;
        _roots = roots;
        _board = board;
        changes.Changed += RevokeOlder;
    }

    /// <summary>Identity this process publishes under on the shared drain-state board.</summary>
    public Guid InstanceId { get; } = Guid.NewGuid();

    public event Action<Guid>? DrainStateChanged;

    public AuthorizationDrainState DrainState(Guid projectId)
    {
        lock (_sync) return DrainStateLocked(projectId);
    }

    private AuthorizationDrainState DrainStateLocked(Guid projectId)
    {
        var draining = false;
        foreach (var lease in _leases)
        {
            if (lease.ProjectId != projectId) continue;
            if (lease.ReleaseFailed) return AuthorizationDrainState.CleanupUnknown;
            if (lease.IsRevoked) draining = true;
        }
        return draining ? AuthorizationDrainState.Draining : AuthorizationDrainState.Idle;
    }

    private bool UpdateDrainStateLocked(Guid projectId)
    {
        var current = DrainStateLocked(projectId);
        if (current == _drainStates.GetValueOrDefault(projectId)) return false;
        if (current == AuthorizationDrainState.Idle) _drainStates.Remove(projectId);
        else _drainStates[projectId] = current;
        return true;
    }

    /// <summary>Worst of the local drain state and every instance's shared-board row.
    /// Board failures degrade to the local state so the badge never breaks.</summary>
    public async Task<AuthorizationDrainState> ObserveDrainStateAsync(Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var local = DrainState(projectId);
        if (_board is null) return local;
        AuthorizationDrainState shared;
        try
        {
            shared = await _board.AggregateAsync(projectId,
                _time.GetUtcNow() - BoardStaleWindow, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return local; }
        return shared > local ? shared : local;
    }

    private void RaiseDrainStateChanged(Guid projectId)
    {
        QueueBoardPublish(projectId);
        try { DrainStateChanged?.Invoke(projectId); }
        catch { /* Observer failures cannot affect lease cleanup or admission. */ }
    }

    private void QueueBoardPublish(Guid projectId)
    {
        if (_board is null) return;
        lock (_sync)
        {
            var state = DrainStateLocked(projectId);
            if (_published.TryGetValue(projectId, out var last) && last == state) return;
            _published[projectId] = state;
            var at = _time.GetUtcNow();
            _publishTail = _publishTail
                .ContinueWith(_ => PublishBoardSafelyAsync(projectId, state, at), CancellationToken.None,
                    TaskContinuationOptions.None, TaskScheduler.Default)
                .Unwrap();
        }
    }

    private async Task PublishBoardSafelyAsync(Guid projectId, AuthorizationDrainState state,
        DateTimeOffset at)
    {
        try { await _board!.PublishAsync(projectId, InstanceId, state, at, CancellationToken.None).ConfigureAwait(false); }
        catch { /* The board is best-effort bookkeeping; it must never affect revocation or admission. */ }
    }

    public async Task ValidateAsync(ProjectProfile expected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync) ThrowIfAdmissionBlockedLocked(expected);
        var current = await _reader.ReadAuthorizationAsync(expected.Id, cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        if (current is not null) RevokeOlder(current.Id, current.AuthorizationRevision);
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
        lock (_sync) ThrowIfAdmissionBlockedLocked(expected);
    }

    private void ThrowIfAdmissionBlockedLocked(ProjectProfile expected)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_highest.TryGetValue(expected.Id, out var highest) && expected.AuthorizationRevision < highest)
            throw new WorkspaceAuthorizationException();
        foreach (var lease in _leases)
        {
            if (lease.ProjectId != expected.Id) continue;
            if (lease.Revision < expected.AuthorizationRevision
                || (lease.Revision == expected.AuthorizationRevision && lease.IsRevoked))
                throw new WorkspaceAuthorizationException();
        }
    }

    public async Task<ITaskWorkspaceLease> AcquireAsync(ProjectProfile expected, CancellationToken cancellationToken)
    {
        var snapshot = expected.Snapshot();
        await ValidateAsync(snapshot, cancellationToken).ConfigureAwait(false);
        var roots = _roots is null ? null : await ProjectRootObjectValidation.OpenAsync(_roots, snapshot, cancellationToken).ConfigureAwait(false);
        var lease = new ProjectAuthorizationLease(this, snapshot, _time, roots);
        try
        {
            bool changed;
            lock (_sync)
            {
                ThrowIfAdmissionBlockedLocked(snapshot);
                _leases.Add(lease);
                changed = UpdateDrainStateLocked(snapshot.Id);
            }
            if (changed) RaiseDrainStateChanged(snapshot.Id);
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
        lock (_sync)
        {
            if (!_highest.TryGetValue(projectId, out var highest) || revision > highest)
                _highest[projectId] = revision;
            leases = _leases.Where(lease => lease.ProjectId == projectId && lease.Revision < revision).ToArray();
        }
        foreach (var lease in leases) lease.Revoke();
        if (leases.Length != 0) NotifyDrainStateAfterMutation(projectId);
    }

    private void RevokeMatching(Guid projectId, long revision)
    {
        ProjectAuthorizationLease[] leases;
        lock (_sync) leases = _leases.Where(lease => lease.ProjectId == projectId && lease.Revision == revision).ToArray();
        foreach (var lease in leases) lease.Revoke();
        if (leases.Length != 0) NotifyDrainStateAfterMutation(projectId);
    }

    internal void Release(ProjectAuthorizationLease lease)
    {
        bool changed;
        lock (_sync)
        {
            _leases.Remove(lease);
            changed = UpdateDrainStateLocked(lease.ProjectId);
        }
        if (changed) RaiseDrainStateChanged(lease.ProjectId);
    }

    internal void NotifyReleaseFailed(ProjectAuthorizationLease lease) => NotifyDrainStateAfterMutation(lease.ProjectId);

    internal void NotifyLeaseRevoked(ProjectAuthorizationLease lease) => NotifyDrainStateAfterMutation(lease.ProjectId);

    private void NotifyDrainStateAfterMutation(Guid projectId)
    {
        bool changed;
        lock (_sync) changed = UpdateDrainStateLocked(projectId);
        if (changed) RaiseDrainStateChanged(projectId);
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

    private async Task DisposeOwnedAsync(ProjectAuthorizationLease[] leases, TaskCompletionSource completion)
    {
        Exception? failure = null;
        try
        {
            foreach (var lease in leases) lease.Revoke();
            await Task.WhenAll(leases.Select(lease => lease.DisposeAsync().AsTask())).ConfigureAwait(false);
        }
        catch (Exception exception) { failure = exception; }
        await ClearBoardAsync().ConfigureAwait(false);
        if (failure is null) completion.SetResult();
        else completion.SetException(failure);
    }

    /// <summary>Best-effort removal of this instance's board rows on clean exit. Bounded so a
    /// stuck board write can never hang disposal; failures leave rows to the staleness rule.</summary>
    private async Task ClearBoardAsync()
    {
        if (_board is null) return;
        try
        {
            Task pending;
            lock (_sync) pending = _publishTail;
            await pending.ConfigureAwait(false);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5), _time);
            await _board.ClearInstanceAsync(InstanceId, deadline.Token)
                .WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch { /* Row cleanup is best-effort; leftover rows age out via the staleness window. */ }
    }
}
