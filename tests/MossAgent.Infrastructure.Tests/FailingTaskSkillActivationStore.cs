using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Tests;

/// <summary>Fails every snapshot save so activator tests can prove storage errors propagate
/// instead of being swallowed into a successful-looking dispatch.</summary>
internal sealed class FailingTaskSkillActivationStore(Exception error) : ITaskSkillActivationStore
{
    public Task SaveForTaskAsync(Guid taskId, IReadOnlyList<TaskSkillActivation> rows,
        CancellationToken cancellationToken) => Task.FromException(error);

    public Task<IReadOnlyList<TaskSkillActivation>> ListForTaskAsync(Guid taskId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TaskSkillActivation>>([]);
}
