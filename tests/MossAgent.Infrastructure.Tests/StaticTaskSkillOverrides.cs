using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Tests;

/// <summary>In-memory task-override probe for activator tests: explicit choices keyed by
/// (task, plugin, path), a list counter so tests can prove the task layer is read once per
/// dispatch, and a failure knob proving read errors propagate instead of being swallowed.</summary>
internal sealed class StaticTaskSkillOverrides : ITaskSkillOverrides
{
    private readonly Dictionary<(Guid TaskId, Guid PluginId, string Path), bool> _choices = [];

    public int ListCalls { get; private set; }
    public Exception? ListFailure { get; set; }

    public void Seed(Guid taskId, Guid pluginId, string componentPath, bool enabled) =>
        _choices[(taskId, pluginId, componentPath)] = enabled;

    public Task<IReadOnlyList<TaskSkillOverride>> ListForTaskAsync(Guid taskId,
        CancellationToken cancellationToken)
    {
        ListCalls++;
        if (ListFailure is not null)
            return Task.FromException<IReadOnlyList<TaskSkillOverride>>(ListFailure);
        return Task.FromResult<IReadOnlyList<TaskSkillOverride>>([.. _choices
            .Where(pair => pair.Key.TaskId == taskId)
            .Select(pair => new TaskSkillOverride(pair.Key.TaskId, pair.Key.PluginId,
                pair.Key.Path, pair.Value))]);
    }

    public Task SetSkillEnabledAsync(Guid taskId, Guid pluginId, string componentPath, bool enabled,
        CancellationToken cancellationToken)
    {
        _choices[(taskId, pluginId, componentPath)] = enabled;
        return Task.CompletedTask;
    }

    public Task ClearForTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        foreach (var key in _choices.Keys.Where(key => key.TaskId == taskId).ToList())
            _choices.Remove(key);
        return Task.CompletedTask;
    }
}
