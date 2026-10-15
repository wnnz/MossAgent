using MossAgent.Tools.Abstractions;

namespace MossAgent.App.Tests;

internal sealed class BrowserToolbarAuditSink : IToolExecutionAuditSink
{
    public List<ToolExecutionAuditStart> Starts { get; } = [];
    public List<ToolExecutionAuditCompletion> Completions { get; } = [];

    public Task<Guid> StartAsync(ToolExecutionAuditStart entry, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Starts.Add(entry);
        return Task.FromResult(Guid.NewGuid());
    }

    public Task CompleteAsync(Guid id, ToolExecutionAuditCompletion completion,
        IReadOnlyList<ToolArtifact> artifacts, CancellationToken token)
    {
        Completions.Add(completion);
        return Task.CompletedTask;
    }
}
