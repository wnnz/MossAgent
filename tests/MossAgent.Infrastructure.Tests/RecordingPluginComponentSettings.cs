using MossAgent.Application.Plugins;

namespace MossAgent.Infrastructure.Tests;

/// <summary>In-memory component-settings probe for activator tests: explicit choices keyed by
/// (plugin, path), a per-plugin list counter so tests can prove overrides are batched once per
/// plugin, and a failure knob proving storage errors propagate instead of being swallowed.</summary>
internal sealed class RecordingPluginComponentSettings : IPluginComponentSettings
{
    private readonly Dictionary<(Guid PluginId, string Path), bool> _choices = [];

    public int ListCalls { get; private set; }
    public Exception? ListFailure { get; set; }

    public void Seed(Guid pluginId, string componentPath, bool enabled) =>
        _choices[(pluginId, componentPath)] = enabled;

    public Task<bool> IsSkillEnabledAsync(Guid pluginId, string componentPath,
        CancellationToken cancellationToken) => Task.FromResult(
            !_choices.TryGetValue((pluginId, componentPath), out var enabled) || enabled);

    public Task SetSkillEnabledAsync(Guid pluginId, string componentPath, bool enabled,
        CancellationToken cancellationToken)
    {
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
