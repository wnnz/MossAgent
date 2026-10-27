using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>Deterministic component inventory: skills are exactly skills/&lt;name&gt;/SKILL.md,
/// each declared MCP transport is a component, and every other entry is an asset. The three
/// manifest files are package plumbing, not components. Ordering is Skill, McpServer, Asset,
/// then name (OrdinalIgnoreCase) with a path tiebreak.</summary>
internal static class PluginComponentInventory
{
    private static readonly HashSet<string> ManifestNames = new(StringComparer.OrdinalIgnoreCase)
        { "plugin.json", "mcp.json", ".codex-plugin/plugin.json" };

    public static IReadOnlyList<PluginComponentDescriptor> Build(
        IReadOnlyList<PluginArchiveEntry> entries, IReadOnlyList<PluginMcpServerDeclaration> servers)
    {
        var components = new List<PluginComponentDescriptor>();
        foreach (var entry in entries)
        {
            if (TryDescribeSkill(entry, out var skill)) components.Add(skill);
            else if (!ManifestNames.Contains(entry.NormalizedName))
                components.Add(new PluginComponentDescriptor(PluginComponentKind.Asset,
                    entry.Segments[^1], entry.NormalizedName));
        }
        components.AddRange(servers.Select(server =>
            new PluginComponentDescriptor(PluginComponentKind.McpServer, server.Name, "mcp.json")));
        return components
            .OrderBy(component => component.Kind)
            .ThenBy(component => component.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(component => component.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryDescribeSkill(PluginArchiveEntry entry, out PluginComponentDescriptor skill)
    {
        skill = null!;
        var segments = entry.Segments;
        if (entry.IsDirectory || segments.Length != 3
            || !segments[0].Equals("skills", StringComparison.OrdinalIgnoreCase)
            || !segments[2].Equals("SKILL.md", StringComparison.OrdinalIgnoreCase)) return false;
        skill = new PluginComponentDescriptor(PluginComponentKind.Skill, segments[1], entry.NormalizedName);
        return true;
    }
}
