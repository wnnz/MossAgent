using MossAgent.Application.Authorization;

namespace MossAgent.App.Tests;

/// <summary>Minimal <see cref="IProjectDrainStateBoard"/> double for tracker tests: returns a
/// configurable aggregate state and counts aggregate calls so tests can observe how often the
/// drain tracker re-observes the board.</summary>
internal sealed class CountingProjectDrainStateBoard : IProjectDrainStateBoard
{
    private int _aggregates;
    private int _state;

    public int Aggregates => Volatile.Read(ref _aggregates);

    public AuthorizationDrainState State
    {
        get => (AuthorizationDrainState)Volatile.Read(ref _state);
        set => Volatile.Write(ref _state, (int)value);
    }

    public Task PublishAsync(Guid projectId, Guid instanceId, AuthorizationDrainState state,
        DateTimeOffset at, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ClearInstanceAsync(Guid instanceId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<AuthorizationDrainState> AggregateAsync(Guid projectId, DateTimeOffset staleBefore,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _aggregates);
        return Task.FromResult(State);
    }

    public Task<int> ReapStaleAsync(DateTimeOffset staleBefore, CancellationToken cancellationToken) =>
        Task.FromResult(0);
}
