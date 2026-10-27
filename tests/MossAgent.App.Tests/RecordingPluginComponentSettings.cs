using MossAgent.Application.Plugins;

namespace MossAgent.App.Tests;

/// <summary>In-memory component-settings probe for pane tests: records every write in order,
/// replays seeded or written choices on reads, and carries list/write failure knobs so tests can
/// assert error surfacing and row restoration.</summary>
internal sealed class RecordingPluginComponentSettings : IPluginComponentSettings
{
    private readonly Dictionary<(Guid PluginId, string Path), bool> _choices = [];

    public int ListCalls { get; private set; }
    public List<(Guid PluginId, string Path, bool Enabled)> Writes { get; } = [];
    public Exception? ListFailure { get; set; }
    public Exception? WriteFailure { get; set; }

    public void Seed(Guid pluginId, string componentPath, bool enabled) =>
        _choices[(pluginId, componentPath)] = enabled;

    public Task<bool> IsSkillEnabledAsync(Guid pluginId, string componentPath,
        CancellationToken cancellationToken) => Task.FromResult(
            !_choices.TryGetValue((pluginId, componentPath), out var enabled) || enabled);

    public Task SetSkillEnabledAsync(Guid pluginId, string componentPath, bool enabled,
        CancellationToken cancellationToken)
    {
        if (WriteFailure is not null) return Task.FromException(WriteFailure);
        Writes.Add((pluginId, componentPath, enabled));
        _choices[(pluginId, componentPath)] = enabled;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, bool>> ListOverridesAsync(Guid pluginId,
        CancellationToken cancellationToken)
    {
        ListCalls++;
        if (ListFailure is not null)
            return Task.FromException<IReadOnlyDictionary<string, bool>>(ListFailure);
        return Task.FromResult<IReadOnlyDictionary<string, bool>>(_choices
            .Where(pair => pair.Key.PluginId == pluginId)
            .ToDictionary(pair => pair.Key.Path, pair => pair.Value, StringComparer.Ordinal));
    }
}
