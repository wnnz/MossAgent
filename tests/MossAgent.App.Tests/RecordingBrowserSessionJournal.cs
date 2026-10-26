using MossAgent.Application.Persistence;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.App.Tests;

internal sealed class RecordingBrowserSessionJournal : IBrowserSessionJournal
{
    public Exception? ReconcileFailure { get; set; }
    public int ReconcileCalls { get; private set; }
    public int WriteCalls { get; private set; }
    public Func<Guid, CancellationToken, Task<IReadOnlyList<BrowserSessionRecord>>> SessionReads { get; set; } =
        (_, _) => Task.FromResult<IReadOnlyList<BrowserSessionRecord>>([]);
    public Func<Guid, CancellationToken, Task<IReadOnlyList<BrowserActionIntentRecord>>> IntentReads { get; set; } =
        (_, _) => Task.FromResult<IReadOnlyList<BrowserActionIntentRecord>>([]);

    public Task<bool> RecordSessionAsync(Guid taskId, BrowserEngine engine,
        BrowserProfileMode profileMode, CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.FromResult(false);
    }

    public Task RecordDetachedAsync(Guid taskId, CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.CompletedTask;
    }

    public Task RecordDisconnectedAsync(Guid taskId, CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.CompletedTask;
    }

    public Task<Guid?> RecordActionAsync(Guid taskId, BrowserActionKind kind, string? targetHash,
        CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.FromResult<Guid?>(null);
    }

    public Task ResolveActionAsync(Guid intentId, BrowserActionIntentState state,
        CancellationToken cancellationToken)
    {
        WriteCalls++;
        return Task.CompletedTask;
    }

    public Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        ReconcileCalls++;
        if (ReconcileFailure is not null) throw ReconcileFailure;
        return Task.FromResult(0);
    }

    public Task<IReadOnlyList<BrowserSessionRecord>> ReadSessionsAsync(Guid taskId,
        CancellationToken cancellationToken) => SessionReads(taskId, cancellationToken);

    public Task<IReadOnlyList<BrowserActionIntentRecord>> ReadIntentsAsync(Guid taskId,
        CancellationToken cancellationToken) => IntentReads(taskId, cancellationToken);
}
