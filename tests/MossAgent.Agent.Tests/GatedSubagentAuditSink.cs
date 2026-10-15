using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent.Tests;

internal sealed class GatedSubagentAuditSink : IToolExecutionAuditSink
{
    public List<ToolExecutionAuditStart> Starts { get; } = [];
    public TaskCompletionSource<ToolExecutionAuditCompletion> CompletionStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool IsCompleted { get; private set; }
    public CancellationToken CompletionToken { get; private set; }

    public Task<Guid> StartAsync(ToolExecutionAuditStart entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Starts.Add(entry);
        return Task.FromResult(Guid.NewGuid());
    }

    public async Task CompleteAsync(Guid executionId, ToolExecutionAuditCompletion completion,
        IReadOnlyList<ToolArtifact> artifacts, CancellationToken cancellationToken)
    {
        CompletionToken = cancellationToken;
        CompletionStarted.TrySetResult(completion);
        await ReleaseCompletion.Task.WaitAsync(cancellationToken);
        IsCompleted = true;
    }
}
