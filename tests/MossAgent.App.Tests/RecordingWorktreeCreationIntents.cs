using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class RecordingWorktreeCreationIntents : IWorktreeCreationIntents
{
    private readonly Dictionary<Guid, IReadOnlyList<WorktreeCreationIntent>> _results = [];

    public bool SupportsCreationIntents { get; set; } = true;
    public Exception? ReadFailure { get; set; }
    public Exception? AcknowledgeFailure { get; set; }
    public TaskCompletionSource? ReadGate { get; set; }
    public TaskCompletionSource<Guid> ReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public List<Guid> ReadCalls { get; } = [];
    public List<Guid> Acknowledged { get; } = [];
    public List<CancellationToken> ReadTokens { get; } = [];
    public int ReconcileCalls { get; private set; }

    public void SetResults(Guid projectId, params WorktreeCreationIntent[] intents) => _results[projectId] = intents;

    public Task ReconcileAsync(CancellationToken cancellationToken)
    {
        ReconcileCalls++;
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<WorktreeCreationIntent>> ReadIncompleteAsync(
        ProjectProfile project, CancellationToken cancellationToken)
    {
        ReadCalls.Add(project.Id);
        ReadTokens.Add(cancellationToken);
        if (ReadGate is { } gate)
        {
            ReadEntered.TrySetResult(project.Id);
            await gate.Task.WaitAsync(cancellationToken);
        }
        if (ReadFailure is not null) throw ReadFailure;
        return _results.TryGetValue(project.Id, out var items) ? items : [];
    }

    public Task AcknowledgeAsync(Guid taskId, CancellationToken cancellationToken)
    {
        if (AcknowledgeFailure is not null) throw AcknowledgeFailure;
        Acknowledged.Add(taskId);
        return Task.CompletedTask;
    }
}
