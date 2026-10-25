using MossAgent.Domain;

namespace MossAgent.Tools.Abstractions.Files;

/// <summary>
/// Persists every artifact/diff-input file session so a crash or failed cleanup leaves a
/// recoverable record instead of a silent leftover. Recording is fail-closed at create;
/// stage transitions are best-effort and must never mask the file operation itself.
/// </summary>
public interface IArtifactSessionJournal
{
    /// <summary>Records a created session before it is returned to the caller.</summary>
    Task<long> RecordAsync(Guid taskId, string fileName, string fullPath, ArtifactSessionKind kind,
        long? byteCount, CancellationToken cancellationToken);

    /// <summary>Marks a recorded session committed; sets the final byte count.</summary>
    Task MarkCompletedAsync(long recordId, long byteCount, CancellationToken cancellationToken);

    /// <summary>Terminal: the caller retained the committed file.</summary>
    Task MarkRetainedAsync(long recordId, CancellationToken cancellationToken);

    /// <summary>Terminal: the file was cleaned up or never produced on disk.</summary>
    Task MarkDiscardedAsync(long recordId, CancellationToken cancellationToken);

    /// <summary>Disposal could not confirm cleanup; the file may still exist.</summary>
    Task MarkCleanupUnknownAsync(long recordId, string errorCode, CancellationToken cancellationToken);

    /// <summary>
    /// Startup entry point: reclassifies stale non-terminal records against the filesystem.
    /// Never deletes files and never auto-acknowledges; unverifiable paths become orphaned.
    /// </summary>
    Task ReconcileAsync(CancellationToken cancellationToken);

    /// <summary>Lists orphaned records awaiting user review for one project's tasks.</summary>
    Task<IReadOnlyList<ArtifactSessionRecord>> ReadUnresolvedAsync(
        ProjectProfile project, CancellationToken cancellationToken);

    /// <summary>Lists every journaled record for one task, newest last.</summary>
    Task<IReadOnlyList<ArtifactSessionRecord>> ReadTaskAsync(
        Guid taskId, CancellationToken cancellationToken);

    /// <summary>Conditionally closes an orphaned record; stale rows raise <see cref="ArtifactSessionStaleException"/>.</summary>
    Task AcknowledgeAsync(long recordId, CancellationToken cancellationToken);
}
