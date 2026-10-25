using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class ObservedArtifactSessionJournal : IArtifactSessionJournal
{
    private long _next;
    public List<(Guid TaskId, string FileName, string FullPath, ArtifactSessionKind Kind, long? ByteCount, long Id)> Records { get; } = [];
    public List<(long Id, ArtifactSessionStage Stage, string? ErrorCode, long? ByteCount)> Marks { get; } = [];
    public Exception? RecordFailure { get; set; }

    public Task<long> RecordAsync(Guid taskId, string fileName, string fullPath, ArtifactSessionKind kind,
        long? byteCount, CancellationToken cancellationToken)
    {
        if (RecordFailure is not null) throw RecordFailure;
        var id = ++_next;
        Records.Add((taskId, fileName, fullPath, kind, byteCount, id));
        return Task.FromResult(id);
    }

    public Task MarkCompletedAsync(long recordId, long byteCount, CancellationToken cancellationToken) =>
        Mark(recordId, ArtifactSessionStage.Completed, null, byteCount);

    public Task MarkRetainedAsync(long recordId, CancellationToken cancellationToken) =>
        Mark(recordId, ArtifactSessionStage.Retained, null, null);

    public Task MarkDiscardedAsync(long recordId, CancellationToken cancellationToken) =>
        Mark(recordId, ArtifactSessionStage.Discarded, null, null);

    public Task MarkCleanupUnknownAsync(long recordId, string errorCode, CancellationToken cancellationToken) =>
        Mark(recordId, ArtifactSessionStage.CleanupUnknown, errorCode, null);

    public Task ReconcileAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<ArtifactSessionRecord>> ReadUnresolvedAsync(
        ProjectProfile project, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ArtifactSessionRecord>>([]);

    public Task<IReadOnlyList<ArtifactSessionRecord>> ReadTaskAsync(
        Guid taskId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ArtifactSessionRecord>>([]);

    public Task AcknowledgeAsync(long recordId, CancellationToken cancellationToken) => Task.CompletedTask;

    private Task Mark(long id, ArtifactSessionStage stage, string? error, long? bytes)
    {
        Marks.Add((id, stage, error, bytes));
        return Task.CompletedTask;
    }
}
