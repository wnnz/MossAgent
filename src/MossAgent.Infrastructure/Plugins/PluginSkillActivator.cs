using System.Security.Cryptography;
using System.Text;
using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>Materializes enabled plugins' skill components at dispatch time. Each candidate path
/// is re-validated against the plugin's install directory (rejecting rooted, empty or
/// '..'-carrying component paths and any escape), the file must exist within the 8 KiB per-skill
/// bound, and entries accumulate only while the joined text stays within the 24 K character
/// prompt budget. Before any text is decoded, the file bytes are hashed and compared to the
/// install-time baseline stored for that component path — a missing baseline row, a mismatch,
/// a missing or unreadable component file, or a path that fails containment marks the plugin
/// <see cref="InstalledPluginState.Drifted"/> via
/// <see cref="IInstalledPluginStore.SetStateAsync"/>. Drift is judged per plugin: components
/// verified before the failing one are discarded too, so nothing from a drifted plugin is
/// injected for this dispatch; other plugins are unaffected. When
/// <paramref name="componentSettings"/> is supplied, a skill component the user explicitly
/// disabled is skipped before any file read — disabled skills were never injected, so they
/// leave no activation row; the overrides and the baseline map are each read once per plugin
/// and read failures propagate like plugin-list failures. When
/// <paramref name="taskOverrides"/> is supplied, the dispatching task's own choices are read
/// once and outrank the global layer: the effective state is task row → global row →
/// default-enabled, so a task can re-include a globally disabled skill as well as skip a
/// globally enabled one; task-enabled skills face the same baseline drift verification as
/// every other component. The rows persisted for the task are
/// exactly the skills in the returned text — unreadable, excluded or unverified skills leave
/// no row, and storage failures propagate.</summary>
public sealed class PluginSkillActivator(
    IInstalledPluginStore plugins,
    ITaskSkillActivationStore activations,
    string pluginsRoot,
    TimeProvider? timeProvider = null,
    IPluginComponentSettings? componentSettings = null,
    ITaskSkillOverrides? taskOverrides = null) : IPluginSkillActivator
{
    internal const int MaximumSkillBytes = 8 * 1024;
    internal const int MaximumPromptCharacters = 24_000;

    private readonly string _root = Path.GetFullPath(pluginsRoot);
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public async Task<string?> BuildSkillInstructionsAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var included = await CollectAsync(taskId, cancellationToken);
        var entries = new List<string>();
        var rows = new List<TaskSkillActivation>();
        var total = 0;
        foreach (var (plugin, component, content) in included)
        {
            var entry = $"### {plugin.Name} v{plugin.Version} — {component.RelativePath}\n{content}";
            var extra = entry.Length + (entries.Count == 0 ? 0 : 1);
            if (total + extra > MaximumPromptCharacters) continue;
            entries.Add(entry);
            total += extra;
            rows.Add(new TaskSkillActivation(Guid.NewGuid(), taskId, plugin.Id, plugin.Name,
                plugin.Version, plugin.ContentHash, component.RelativePath, content, _time.GetUtcNow()));
        }
        await activations.SaveForTaskAsync(taskId, rows, cancellationToken);
        return entries.Count == 0 ? null : string.Join('\n', entries);
    }

    private async Task<IReadOnlyList<(InstalledPlugin Plugin, PluginComponentDescriptor Component, string Content)>>
        CollectAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var taskChoices = await ReadTaskChoicesAsync(taskId, cancellationToken);
        var found = new List<(InstalledPlugin Plugin, PluginComponentDescriptor Component, string Content)>();
        foreach (var plugin in await plugins.ListAsync(cancellationToken))
        {
            if (plugin.State != InstalledPluginState.Enabled) continue;
            var candidates = new List<(InstalledPlugin Plugin, PluginComponentDescriptor Component, string Content)>();
            var drifted = false;
            IReadOnlyDictionary<string, bool>? overrides = null;
            IReadOnlyDictionary<string, string>? hashes = null;
            Dictionary<string, bool>? taskChoice = null;
            taskChoices?.TryGetValue(plugin.Id, out taskChoice);
            foreach (var component in plugin.Components)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (component.Kind != PluginComponentKind.Skill) continue;
                var enabled = true;
                if (taskChoice is not null && taskChoice.TryGetValue(component.RelativePath, out var taskEnabled))
                    enabled = taskEnabled;
                else if (componentSettings is not null)
                {
                    overrides ??= await componentSettings.ListOverridesAsync(plugin.Id, cancellationToken);
                    enabled = !overrides.TryGetValue(component.RelativePath, out var stored) || stored;
                }
                if (!enabled) continue;
                hashes ??= await plugins.ListPayloadHashesAsync(plugin.Id, cancellationToken);
                var (bytes, untrusted) = await TryReadBytesAsync(plugin, component.RelativePath, cancellationToken);
                if (untrusted || (bytes is not null && !Verified(bytes, component.RelativePath, hashes)))
                {
                    await plugins.SetStateAsync(plugin.Id, InstalledPluginState.Drifted, cancellationToken);
                    drifted = true;
                    break;
                }
                if (bytes is null) continue;
                var content = Encoding.UTF8.GetString(bytes);
                if (content.Length <= MaximumSkillBytes) candidates.Add((plugin, component, content));
            }
            if (!drifted) found.AddRange(candidates);
        }
        return [.. found
            .OrderBy(item => item.Plugin.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Component.RelativePath, StringComparer.Ordinal)];
    }

    /// <summary>Reads the dispatching task's own choices once and groups them per plugin with
    /// path-case-insensitive keys. The task layer outranks the plugin-wide settings: a stored
    /// row — enabled or disabled — is the effective state for that component; absent rows fall
    /// through to the global layer and then the default-enabled state.</summary>
    private async Task<Dictionary<Guid, Dictionary<string, bool>>?> ReadTaskChoicesAsync(
        Guid taskId, CancellationToken cancellationToken)
    {
        if (taskOverrides is null) return null;
        var rows = await taskOverrides.ListForTaskAsync(taskId, cancellationToken);
        if (rows.Count == 0) return null;
        var map = new Dictionary<Guid, Dictionary<string, bool>>();
        foreach (var row in rows)
        {
            if (!map.TryGetValue(row.PluginId, out var choices))
                choices = map[row.PluginId] = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            choices[row.ComponentPath] = row.Enabled;
        }
        return map;
    }

    private static bool Verified(byte[] bytes, string relativePath,
        IReadOnlyDictionary<string, string> hashes) =>
        hashes.TryGetValue(relativePath, out var expected)
        && Convert.ToHexStringLower(SHA256.HashData(bytes)).Equals(expected, StringComparison.Ordinal);

    /// <summary>Reads the component bytes. The second tuple element reports trust, not just
    /// readability: a path that fails containment, a missing file, or a read failure means the
    /// payload can no longer be proven intact and is treated as drift. A file that exists but
    /// exceeds the per-skill byte bound keeps the pre-baseline semantics — it is skipped for
    /// this dispatch without condemning the whole plugin.</summary>
    private async Task<(byte[]? Bytes, bool Untrusted)> TryReadBytesAsync(InstalledPlugin plugin,
        string relativePath, CancellationToken cancellationToken)
    {
        var path = PluginInstallPaths.Resolve(_root, plugin.InstallPath, relativePath);
        if (path is null) return (null, true);
        var info = new FileInfo(path);
        if (!info.Exists) return (null, true);
        if (info.Length > MaximumSkillBytes) return (null, false);
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            return bytes.Length <= MaximumSkillBytes ? (bytes, false) : (null, false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (null, true);
        }
    }
}
