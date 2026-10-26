using MossAgent.Application.Persistence;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser.Tests;

internal sealed class RecordingBrowserSessionJournal : IBrowserSessionJournal
{
    public List<(Guid TaskId, BrowserEngine Engine, BrowserProfileMode ProfileMode)> Sessions { get; } = [];
    public List<(Guid TaskId, BrowserActionKind Kind, string? TargetHash)> Actions { get; } = [];
    public List<(Guid IntentId, BrowserActionIntentState State)> Resolutions { get; } = [];
    public List<string> Sequence { get; } = [];
    public List<Guid> Detached { get; } = [];
    public List<Guid> Disconnected { get; } = [];
    public int ReconcileCalls { get; private set; }
    public bool SessionResult { get; set; } = true;
    public Exception? SessionFailure { get; set; }
    public bool HasAttachedSession { get; set; } = true;
    public Exception? ActionFailure { get; set; }

    public Task<bool> RecordSessionAsync(
        Guid taskId, BrowserEngine engine, BrowserProfileMode profileMode,
        CancellationToken cancellationToken)
    {
        if (SessionFailure is not null)
        {
            throw SessionFailure;
        }

        if (SessionResult)
        {
            Sessions.Add((taskId, engine, profileMode));
            Sequence.Add("session");
        }

        return Task.FromResult(SessionResult);
    }

    public Task RecordDetachedAsync(Guid taskId, CancellationToken cancellationToken)
    {
        Detached.Add(taskId);
        Sequence.Add("detached");
        return Task.CompletedTask;
    }

    public Task RecordDisconnectedAsync(Guid taskId, CancellationToken cancellationToken)
    {
        Disconnected.Add(taskId);
        Sequence.Add("disconnected");
        return Task.CompletedTask;
    }

    public Task<Guid?> RecordActionAsync(
        Guid taskId, BrowserActionKind kind, string? targetHash,
        CancellationToken cancellationToken)
    {
        if (ActionFailure is not null)
        {
            throw ActionFailure;
        }

        Actions.Add((taskId, kind, targetHash));
        Sequence.Add("action");
        return Task.FromResult(HasAttachedSession ? Guid.NewGuid() : (Guid?)null);
    }

    public Task ResolveActionAsync(
        Guid intentId, BrowserActionIntentState state, CancellationToken cancellationToken)
    {
        Resolutions.Add((intentId, state));
        Sequence.Add("resolve");
        return Task.CompletedTask;
    }

    public Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        ReconcileCalls++;
        return Task.FromResult(0);
    }

    public Task<IReadOnlyList<BrowserSessionRecord>> ReadSessionsAsync(
        Guid taskId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BrowserSessionRecord>>([]);

    public Task<IReadOnlyList<BrowserActionIntentRecord>> ReadIntentsAsync(
        Guid taskId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BrowserActionIntentRecord>>([]);
}
