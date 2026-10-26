using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Application.Persistence;

/// <summary>
/// Persists task browser session identity plus its pending mutating operations so a crash
/// leaves a viewable terminal record instead of a silent orphan. Session registration is
/// fail-closed at the call site; lifecycle transitions and intent resolutions are
/// best-effort and must never mask the browser operation itself.
/// </summary>
public interface IBrowserSessionJournal : IBrowserSessionActionJournal
{
    /// <summary>
    /// Records a newly attached session before it is returned to the caller.
    /// Returns false when the task row is absent or archived and no row was written;
    /// callers treat that as "cannot journal" and refuse the unjournaled session.
    /// </summary>
    Task<bool> RecordSessionAsync(Guid taskId, BrowserEngine engine,
        BrowserProfileMode profileMode, CancellationToken cancellationToken);

    /// <summary>Best-effort: attached sessions of the task become detached on release.</summary>
    Task RecordDetachedAsync(Guid taskId, CancellationToken cancellationToken);

    /// <summary>Best-effort: attached sessions of the task become disconnected when the inner session dies.</summary>
    Task RecordDisconnectedAsync(Guid taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Startup reconciliation: marks leftover attached sessions and committed intents
    /// interrupted. Never replays an intent or re-attaches a session.
    /// </summary>
    Task<int> ReconcileAsync(CancellationToken cancellationToken);

    /// <summary>Lists journaled sessions for one task, oldest first.</summary>
    Task<IReadOnlyList<BrowserSessionRecord>> ReadSessionsAsync(Guid taskId, CancellationToken cancellationToken);

    /// <summary>Lists journaled action intents for one task, oldest first.</summary>
    Task<IReadOnlyList<BrowserActionIntentRecord>> ReadIntentsAsync(Guid taskId, CancellationToken cancellationToken);
}
