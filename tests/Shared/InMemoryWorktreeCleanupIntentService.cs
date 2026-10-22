using System.Collections.Concurrent;
using MossAgent.Application.Git;
using MossAgent.Domain;

namespace MossAgent.Testing;

public sealed class InMemoryWorktreeCleanupIntentService : IWorktreeCleanupIntentService
{
    private readonly ConcurrentDictionary<Guid, WorktreeCleanupIntent> _items = new();

    public string InstanceId { get; } = Guid.NewGuid().ToString("N");
    public WorktreeCleanupIntent? Last => _items.Values.SingleOrDefault();

    public Task BeginAsync(Guid projectId, Guid taskId, string targetPath,
        string quarantinePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow;
        _items[taskId] = new(taskId, projectId, targetPath, quarantinePath,
            WorktreeCleanupStage.Preparing, WorktreeCleanupState.Running,
            InstanceId, null, now, now);
        return Task.CompletedTask;
    }

    public Task MarkQuarantinedAsync(Guid taskId, string quarantinePath,
        CancellationToken cancellationToken) =>
        Transition(taskId, quarantinePath, WorktreeCleanupStage.Quarantined,
            cancellationToken);

    public Task MarkRegistrationDeletedAsync(Guid taskId, string quarantinePath,
        CancellationToken cancellationToken) =>
        Transition(taskId, quarantinePath, WorktreeCleanupStage.RegistrationDeleted,
            cancellationToken);

    public Task MarkCompletedAsync(Guid taskId, string quarantinePath,
        CancellationToken cancellationToken) =>
        SetState(taskId, quarantinePath, WorktreeCleanupState.Completed, null,
            cancellationToken);

    public Task MarkFailedAsync(Guid taskId, string quarantinePath, string errorCode,
        CancellationToken cancellationToken) =>
        SetState(taskId, quarantinePath, WorktreeCleanupState.Failed, errorCode,
            cancellationToken);

    public Task<IReadOnlyList<WorktreeCleanupIntent>> ReadIncompleteAsync(
        Guid projectId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<WorktreeCleanupIntent> items = _items.Values
            .Where(item => item.ProjectId == projectId
                && item.State is WorktreeCleanupState.Running or WorktreeCleanupState.Failed)
            .OrderBy(item => item.UpdatedAt)
            .ToArray();
        return Task.FromResult(items);
    }

    public Task AcknowledgeAsync(WorktreeCleanupIntent intent,
        CancellationToken cancellationToken) =>
        SetState(intent.TaskId, intent.QuarantinePath, WorktreeCleanupState.Acknowledged,
            intent.ErrorCode, cancellationToken);

    private Task Transition(Guid taskId, string quarantinePath,
        WorktreeCleanupStage stage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_items.TryGetValue(taskId, out var item)
            || item.QuarantinePath != quarantinePath)
            throw new InvalidOperationException();
        _items[taskId] = item with { Stage = stage, UpdatedAt = DateTimeOffset.UtcNow };
        return Task.CompletedTask;
    }

    private Task SetState(Guid taskId, string quarantinePath,
        WorktreeCleanupState state, string? errorCode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_items.TryGetValue(taskId, out var item)
            || item.QuarantinePath != quarantinePath)
            throw new InvalidOperationException();
        _items[taskId] = item with
        {
            State = state,
            ErrorCode = errorCode,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        return Task.CompletedTask;
    }
}
