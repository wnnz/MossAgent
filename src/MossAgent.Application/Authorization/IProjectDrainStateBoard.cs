namespace MossAgent.Application.Authorization;

/// <summary>Cross-instance drain-state board shared through the workspace database. Each
/// instance upserts its own per-project row when its local drain state changes so every
/// instance can aggregate the worst state across live and crashed peers. The board is
/// best-effort bookkeeping: it must never gate revocation, admission, or cleanup.</summary>
public interface IProjectDrainStateBoard
{
    /// <summary>Upserts this instance's row for the project. Publishing
    /// <see cref="AuthorizationDrainState.Idle"/> deletes the row instead of storing it.</summary>
    Task PublishAsync(Guid projectId, Guid instanceId, AuthorizationDrainState state,
        DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Deletes every row this instance published, for a clean shutdown.</summary>
    Task ClearInstanceAsync(Guid instanceId, CancellationToken cancellationToken);

    /// <summary>Returns the worst state across all instances' rows for the project:
    /// CleanupUnknown over Draining over Idle. A non-Idle row whose UpdatedAt is not newer
    /// than <paramref name="staleBefore"/> counts as CleanupUnknown because its owner is
    /// unreachable and cleanup cannot be confirmed finished. No rows yields Idle.</summary>
    Task<AuthorizationDrainState> AggregateAsync(Guid projectId, DateTimeOffset staleBefore,
        CancellationToken cancellationToken);

    /// <summary>Deletes every row — any project, any instance — whose UpdatedAt is not newer
    /// than <paramref name="staleBefore"/>. A row that old can only belong to an instance
    /// that stopped publishing, and a dead owner's in-process lease barrier died with it, so
    /// any observer may reclaim the row; returns the number of rows deleted.</summary>
    Task<int> ReapStaleAsync(DateTimeOffset staleBefore, CancellationToken cancellationToken);
}
