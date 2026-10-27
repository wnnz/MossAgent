using MossAgent.Application.Authorization;

namespace MossAgent.Infrastructure.Tests;

/// <summary>In-memory <see cref="IProjectDrainStateBoard"/> double that applies the same
/// semantics as the SQLite store — publishing Idle deletes the row, stale non-Idle rows
/// aggregate as CleanupUnknown and are reclaimed project-scoped by the same aggregate —
/// so monitor tests observe publishes deterministically.</summary>
internal sealed class RecordingProjectDrainStateBoard : IProjectDrainStateBoard
{
    private readonly object _sync = new();
    private readonly Dictionary<(Guid Project, Guid Instance), ProjectDrainStateRow> _rows = [];
    private readonly List<(Guid Project, Guid Instance, AuthorizationDrainState State)> _publishes = [];
    private TaskCompletionSource _signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _clears;
    private int _reapCalls;
    private DateTimeOffset _lastStaleBefore = DateTimeOffset.MinValue;

    public Exception? PublishFailure { get; set; }
    public Exception? AggregateFailure { get; set; }

    public IReadOnlyList<(Guid Project, Guid Instance, AuthorizationDrainState State)> Publishes
    {
        get { lock (_sync) return [.. _publishes]; }
    }

    public int Clears { get { lock (_sync) return _clears; } }
    public int ReapCalls { get { lock (_sync) return _reapCalls; } }
    public DateTimeOffset LastStaleBefore { get { lock (_sync) return _lastStaleBefore; } }

    public Task PublishAsync(Guid projectId, Guid instanceId, AuthorizationDrainState state,
        DateTimeOffset at, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _publishes.Add((projectId, instanceId, state));
            Signal();
        }
        if (PublishFailure is not null) return Task.FromException(PublishFailure);
        lock (_sync)
        {
            if (state == AuthorizationDrainState.Idle) _rows.Remove((projectId, instanceId));
            else _rows[(projectId, instanceId)] = new(projectId, instanceId, state, at);
        }
        return Task.CompletedTask;
    }

    public Task ClearInstanceAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _clears++;
            foreach (var key in _rows.Keys.Where(key => key.Instance == instanceId).ToArray())
                _rows.Remove(key);
            Signal();
        }
        return Task.CompletedTask;
    }

    public Task<AuthorizationDrainState> AggregateAsync(Guid projectId, DateTimeOffset staleBefore,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _lastStaleBefore = staleBefore;
            if (AggregateFailure is not null)
                return Task.FromException<AuthorizationDrainState>(AggregateFailure);
            var worst = AuthorizationDrainState.Idle;
            var sawStale = false;
            foreach (var row in _rows.Values.Where(row => row.ProjectId == projectId))
            {
                var effective = row.UpdatedAt <= staleBefore
                    ? AuthorizationDrainState.CleanupUnknown
                    : row.State;
                sawStale |= row.UpdatedAt <= staleBefore;
                if (effective > worst) worst = effective;
            }
            if (sawStale)
            {
                foreach (var key in _rows.Keys.Where(key =>
                        key.Project == projectId && _rows[key].UpdatedAt <= staleBefore).ToArray())
                    _rows.Remove(key);
                Signal();
            }
            return Task.FromResult(worst);
        }
    }

    public Task<int> ReapStaleAsync(DateTimeOffset staleBefore, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _reapCalls++;
            var stale = _rows.Where(pair => pair.Value.UpdatedAt <= staleBefore)
                .Select(pair => pair.Key).ToArray();
            foreach (var key in stale) _rows.Remove(key);
            if (stale.Length != 0) Signal();
            return Task.FromResult(stale.Length);
        }
    }

    /// <summary>Completes once at least <paramref name="count"/> publish calls were recorded.</summary>
    public async Task WaitForPublishCountAsync(int count, CancellationToken cancellationToken)
    {
        while (true)
        {
            Task signal;
            lock (_sync)
            {
                if (_publishes.Count >= count) return;
                signal = _signal.Task;
            }
            await signal.WaitAsync(cancellationToken);
        }
    }

    private void Signal()
    {
        _signal.TrySetResult();
        _signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
