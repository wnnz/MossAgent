using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Browser.Tests;

internal sealed class RecordingArtifactSessionJournal : IArtifactSessionJournal
{
    private long _next;

    public List<(Guid TaskId, string FileName, string FullPath, ArtifactSessionKind Kind, long? ByteCount, long Id)> Records { get; } = [];
    public List<(long Id, ArtifactSessionStage Stage, string? ErrorCode)> Marks { get; } = [];
    public Exception? RecordFailure { get; set; }
    public Exception? MarkFailure { get; set; }

    public Task<long> RecordAsync(Guid taskId, string fileName, string fullPath, ArtifactSessionKind kind,
        long? byteCount, CancellationToken cancellationToken)
    {
        if (RecordFailure is not null) throw RecordFailure;
        var id = ++_next;
        Records.Add((taskId, fileName, fullPath, kind, byteCount, id));
        return Task.FromResult(id);
    }

    public Task MarkCompletedAsync(long recordId, long byteCount, CancellationToken cancellationToken) =>
        Mark(recordId, ArtifactSessionStage.Completed, null);

    public Task MarkRetainedAsync(long recordId, CancellationToken cancellationToken) =>
        Mark(recordId, ArtifactSessionStage.Retained, null);

    public Task MarkDiscardedAsync(long recordId, CancellationToken cancellationToken) =>
        Mark(recordId, ArtifactSessionStage.Discarded, null);

    public Task MarkCleanupUnknownAsync(long recordId, string errorCode, CancellationToken cancellationToken) =>
        Mark(recordId, ArtifactSessionStage.CleanupUnknown, errorCode);

    public Task ReconcileAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<ArtifactSessionRecord>> ReadUnresolvedAsync(
        ProjectProfile project, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ArtifactSessionRecord>>([]);

    public Task<IReadOnlyList<ArtifactSessionRecord>> ReadTaskAsync(
        Guid taskId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ArtifactSessionRecord>>([]);

    public Task AcknowledgeAsync(long recordId, CancellationToken cancellationToken) => Task.CompletedTask;

    private Task Mark(long id, ArtifactSessionStage stage, string? error)
    {
        if (MarkFailure is not null) throw MarkFailure;
        Marks.Add((id, stage, error));
        return Task.CompletedTask;
    }
}
