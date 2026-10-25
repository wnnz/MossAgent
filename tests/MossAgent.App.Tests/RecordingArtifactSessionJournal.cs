using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.Tests;

internal sealed class RecordingArtifactSessionJournal : IArtifactSessionJournal
{
    private readonly Dictionary<Guid, IReadOnlyList<ArtifactSessionRecord>> _results = [];
    private long _next;

    public Exception? ReadFailure { get; set; }
    public Exception? AcknowledgeFailure { get; set; }
    public Exception? ReconcileFailure { get; set; }
    public List<Guid> ReadCalls { get; } = [];
    public List<long> Acknowledged { get; } = [];
    public int ReconcileCalls { get; private set; }

    public void SetResults(Guid projectId, params ArtifactSessionRecord[] records) => _results[projectId] = records;

    public Task<long> RecordAsync(Guid taskId, string fileName, string fullPath, ArtifactSessionKind kind,
        long? byteCount, CancellationToken cancellationToken) => Task.FromResult(++_next);

    public Task MarkCompletedAsync(long recordId, long byteCount, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task MarkRetainedAsync(long recordId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task MarkDiscardedAsync(long recordId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task MarkCleanupUnknownAsync(long recordId, string errorCode, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ReconcileAsync(CancellationToken cancellationToken)
    {
        ReconcileCalls++;
        if (ReconcileFailure is not null) throw ReconcileFailure;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ArtifactSessionRecord>> ReadUnresolvedAsync(
        ProjectProfile project, CancellationToken cancellationToken)
    {
        ReadCalls.Add(project.Id);
        if (ReadFailure is not null) throw ReadFailure;
        return Task.FromResult<IReadOnlyList<ArtifactSessionRecord>>(
            _results.TryGetValue(project.Id, out var items) ? items : []);
    }

    public Task<IReadOnlyList<ArtifactSessionRecord>> ReadTaskAsync(
        Guid taskId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ArtifactSessionRecord>>([]);

    public Task AcknowledgeAsync(long recordId, CancellationToken cancellationToken)
    {
        if (AcknowledgeFailure is not null) throw AcknowledgeFailure;
        Acknowledged.Add(recordId);
        return Task.CompletedTask;
    }
}
