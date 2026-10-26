namespace MossAgent.Application.Persistence;

/// <summary>
/// The action-intent half of the browser session journal, consumed by the session wrapper
/// that guards mutating operations. Recording is fail-closed at the call site: a caller
/// that cannot journal an intent must not emit the unaudited side effect.
/// </summary>
public interface IBrowserSessionActionJournal
{
    /// <summary>
    /// Records a mutating call as <see cref="BrowserActionIntentState.Committed"/> before it
    /// reaches the inner session. Returns the intent id, or null when the task has no
    /// attached journaled session — callers must refuse the side effect then.
    /// </summary>
    Task<Guid?> RecordActionAsync(Guid taskId, BrowserActionKind kind, string? targetHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Transitions a committed intent to a terminal outcome; a no-op once any non-committed
    /// state exists, so resolution is idempotent. Best-effort: never masks the call result.
    /// </summary>
    Task ResolveActionAsync(Guid intentId, BrowserActionIntentState state,
        CancellationToken cancellationToken);
}
