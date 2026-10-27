using MossAgent.Domain.Plugins;

namespace MossAgent.Application.Plugins;

/// <summary>Persists per-task skill activation snapshots: the exact skill rows that were
/// injected into a task's system prompt. Saving replaces the task's prior snapshot wholesale so
/// the stored set always equals what the last activation injected.</summary>
public interface ITaskSkillActivationStore
{
    Task SaveForTaskAsync(Guid taskId, IReadOnlyList<TaskSkillActivation> rows, CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskSkillActivation>> ListForTaskAsync(Guid taskId, CancellationToken cancellationToken);
}
