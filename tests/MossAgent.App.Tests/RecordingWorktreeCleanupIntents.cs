using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class RecordingWorktreeCleanupIntents : IWorktreeCleanupIntents
{
    private readonly Dictionary<Guid, IReadOnlyList<WorktreeCleanupIntent>> _results = [];

    public bool SupportsCleanupIntents { get; set; } = true;
    public Exception? AcknowledgeFailure { get; set; }
    public List<Guid> ReadCalls { get; } = [];
    public List<Guid> Acknowledged { get; } = [];

    public void SetResults(Guid projectId, params WorktreeCleanupIntent[] intents) => _results[projectId] = intents;

    public Task ReconcileAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<WorktreeCleanupIntent>> ReadIncompleteAsync(
        ProjectProfile project, CancellationToken cancellationToken)
    {
        ReadCalls.Add(project.Id);
        return Task.FromResult<IReadOnlyList<WorktreeCleanupIntent>>(
            _results.TryGetValue(project.Id, out var items) ? items : []);
    }

    public Task AcknowledgeAsync(Guid taskId, CancellationToken cancellationToken)
    {
        if (AcknowledgeFailure is not null) throw AcknowledgeFailure;
        Acknowledged.Add(taskId);
        return Task.CompletedTask;
    }
}
