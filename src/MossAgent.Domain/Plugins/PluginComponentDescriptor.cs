namespace MossAgent.Domain.Plugins;

/// <summary>One inventoried package component. <see cref="RelativePath"/> is the normalized
/// archive-relative name for file-backed components and the declaring manifest for MCP servers.</summary>
public sealed record PluginComponentDescriptor(PluginComponentKind Kind, string Name, string RelativePath);
