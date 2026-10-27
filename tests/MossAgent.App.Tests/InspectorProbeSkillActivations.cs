using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.App.Tests;

internal sealed class InspectorProbeSkillActivations : ITaskSkillActivationStore
{
    public Func<Guid, CancellationToken, Task<IReadOnlyList<TaskSkillActivation>>> Reads { get; set; } =
        (_, _) => Task.FromResult<IReadOnlyList<TaskSkillActivation>>([]);
    public List<Guid> Queried { get; } = [];

    public Task SaveForTaskAsync(Guid taskId, IReadOnlyList<TaskSkillActivation> rows,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<TaskSkillActivation>> ListForTaskAsync(Guid taskId,
        CancellationToken cancellationToken)
    {
        Queried.Add(taskId);
        return Reads(taskId, cancellationToken);
    }
}
