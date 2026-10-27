using MossAgent.Domain.Plugins;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>Parsed fields of the portable root manifest plus the resolved overlay source.</summary>
internal sealed record PluginManifestData(string Name, string Version, string? Description, PluginOverlaySource OverlaySource);
