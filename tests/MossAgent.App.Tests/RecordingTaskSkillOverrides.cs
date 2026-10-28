using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.App.Tests;

/// <summary>In-memory per-task skill-override probe for picker tests: records every write and
/// clear in order, replays seeded or written rows on reads, and carries list/write failure
/// knobs so tests can assert rollback and error surfacing.</summary>
internal sealed class RecordingTaskSkillOverrides : ITaskSkillOverrides
{
    private readonly Dictionary<(Guid TaskId, Guid PluginId, string Path), bool> _choices = [];

    public int ListCalls { get; private set; }
    public List<(Guid TaskId, Guid PluginId, string Path, bool Enabled)> Writes { get; } = [];
    public List<Guid> ClearedTasks { get; } = [];
    public Exception? ListFailure { get; set; }
    public Exception? WriteFailure { get; set; }
    public Exception? ClearFailure { get; set; }

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
            .OrderBy(pair => pair.Key.PluginId).ThenBy(pair => pair.Key.Path, StringComparer.Ordinal)
            .Select(pair => new TaskSkillOverride(pair.Key.TaskId, pair.Key.PluginId,
                pair.Key.Path, pair.Value))]);
    }

    public Task SetSkillEnabledAsync(Guid taskId, Guid pluginId, string componentPath, bool enabled,
        CancellationToken cancellationToken)
    {
        if (WriteFailure is not null) return Task.FromException(WriteFailure);
        Writes.Add((taskId, pluginId, componentPath, enabled));
        _choices[(taskId, pluginId, componentPath)] = enabled;
        return Task.CompletedTask;
    }

    public Task ClearForTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        if (ClearFailure is not null) return Task.FromException(ClearFailure);
        ClearedTasks.Add(taskId);
        foreach (var key in _choices.Keys.Where(key => key.TaskId == taskId).ToList())
            _choices.Remove(key);
        return Task.CompletedTask;
    }
}
