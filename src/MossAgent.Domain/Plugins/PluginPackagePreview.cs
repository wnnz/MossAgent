namespace MossAgent.Domain.Plugins;

/// <summary>Immutable preview of a local plugin package: manifest identity, inventory and
/// content hash. No installation, enablement or persistence is implied by this record.</summary>
public sealed record PluginPackagePreview(
    string Name,
    string Version,
    string? Description,
    string SourcePath,
    string ContentHash,
    int EntryCount,
    long TotalUncompressedBytes,
    IReadOnlyList<PluginComponentDescriptor> Components,
    PluginOverlaySource OverlaySource,
    IReadOnlyList<PluginMcpServerDeclaration> DeclaredMcpServers);
