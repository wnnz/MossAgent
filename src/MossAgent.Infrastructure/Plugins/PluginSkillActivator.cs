using System.Text;
using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>Materializes enabled plugins' skill components at dispatch time. Each candidate path
/// is re-validated against the plugin's install directory (rejecting rooted, empty or
/// '..'-carrying component paths and any escape), the file must exist within the 8 KiB per-skill
/// bound, and entries accumulate only while the joined text stays within the 24 K character
/// prompt budget. The rows persisted for the task are exactly the skills in the returned text —
/// unreadable or excluded skills leave no row, and storage failures propagate.</summary>
public sealed class PluginSkillActivator(
    IInstalledPluginStore plugins,
    ITaskSkillActivationStore activations,
    string pluginsRoot,
    TimeProvider? timeProvider = null) : IPluginSkillActivator
{
    internal const int MaximumSkillBytes = 8 * 1024;
    internal const int MaximumPromptCharacters = 24_000;

    private readonly string _root = Path.GetFullPath(pluginsRoot);
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public async Task<string?> BuildSkillInstructionsAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var included = await CollectAsync(cancellationToken);
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
        CollectAsync(CancellationToken cancellationToken)
    {
        var found = new List<(InstalledPlugin Plugin, PluginComponentDescriptor Component, string Content)>();
        foreach (var plugin in await plugins.ListAsync(cancellationToken))
        {
            if (plugin.State != InstalledPluginState.Enabled) continue;
            foreach (var component in plugin.Components)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (component.Kind != PluginComponentKind.Skill) continue;
                var content = await TryReadAsync(plugin, component.RelativePath, cancellationToken);
                if (content is not null) found.Add((plugin, component, content));
            }
        }
        return [.. found
            .OrderBy(item => item.Plugin.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Component.RelativePath, StringComparer.Ordinal)];
    }

    private async Task<string?> TryReadAsync(InstalledPlugin plugin, string relativePath,
        CancellationToken cancellationToken)
    {
        var path = CandidatePath(plugin.InstallPath, relativePath);
        if (path is null) return null;
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > MaximumSkillBytes) return null;
        try
        {
            var content = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
            return content.Length <= MaximumSkillBytes ? content : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private string? CandidatePath(string installPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized) || Path.IsPathFullyQualified(normalized)) return null;
        if (normalized.Split(Path.DirectorySeparatorChar).Any(segment => segment == "..")) return null;
        try
        {
            var rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
            var installRoot = Path.GetFullPath(Path.Combine(_root, installPath));
            if (!installRoot.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) return null;
            var installPrefix = installRoot.EndsWith(Path.DirectorySeparatorChar)
                ? installRoot : installRoot + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(installRoot, normalized));
            return candidate.StartsWith(installPrefix, StringComparison.OrdinalIgnoreCase) ? candidate : null;
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
