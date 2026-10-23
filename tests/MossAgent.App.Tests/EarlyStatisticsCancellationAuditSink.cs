using MossAgent.Tools.Abstractions;

namespace MossAgent.App.Tests;

internal sealed class EarlyStatisticsCancellationAuditSink(IToolExecutionAuditSink inner) : IToolExecutionAuditSink
{
    private int _statistics;
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<Guid> StartAsync(ToolExecutionAuditStart entry, CancellationToken cancellationToken)
    {
        var id = await inner.StartAsync(entry, cancellationToken);
        if (entry.ToolName != "filesystem.change_statistics" || Interlocked.Increment(ref _statistics) != 1) return id;
        Started.TrySetResult();
        try { await Task.Delay(Timeout.Infinite, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        return id;
    }

    public Task CompleteAsync(Guid executionId, ToolExecutionAuditCompletion completion,
        IReadOnlyList<ToolArtifact> artifacts, CancellationToken cancellationToken) =>
        inner.CompleteAsync(executionId, completion, artifacts, cancellationToken);
}
