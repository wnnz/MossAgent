using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Tests;

/// <summary>Captures every saved snapshot so activator tests can assert that exactly the
/// injected skills were persisted for the task.</summary>
internal sealed class RecordingTaskSkillActivationStore : ITaskSkillActivationStore
{
    public List<(Guid TaskId, IReadOnlyList<TaskSkillActivation> Rows)> Saves { get; } = [];

    public Task SaveForTaskAsync(Guid taskId, IReadOnlyList<TaskSkillActivation> rows,
        CancellationToken cancellationToken)
    {
        Saves.Add((taskId, rows));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TaskSkillActivation>> ListForTaskAsync(Guid taskId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Saves.LastOrDefault(save => save.TaskId == taskId).Rows
            ?? (IReadOnlyList<TaskSkillActivation>)[]);
}
