using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.Tests;

internal sealed class ToolProgressAuditSink : IToolExecutionAuditSink
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ToolExecutionAuditCompletion? Completion { get; private set; }
    public IReadOnlyList<ToolArtifact> Artifacts { get; private set; } = [];

    public Task<Guid> StartAsync(ToolExecutionAuditStart entry, CancellationToken token) => Task.FromResult(Guid.NewGuid());

    public async Task CompleteAsync(Guid executionId, ToolExecutionAuditCompletion completion,
        IReadOnlyList<ToolArtifact> artifacts, CancellationToken token)
    {
        Completion = completion;
        Artifacts = artifacts;
        Entered.TrySetResult();
        await Release.Task.WaitAsync(token);
    }
}
