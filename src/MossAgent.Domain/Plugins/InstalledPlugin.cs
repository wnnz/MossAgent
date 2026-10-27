namespace MossAgent.Domain.Plugins;

/// <summary>A plugin package committed to disk and indexed in SQLite. <see cref="InstallPath"/>
/// is relative to the plugin root and immutable for the row's lifetime; the archive payload is
/// never stored in the database.</summary>
public sealed record InstalledPlugin(
    Guid Id,
    string Name,
    string Version,
    string ContentHash,
    string InstallPath,
    InstalledPluginState State,
    string SourcePath,
    DateTimeOffset InstalledAt,
    IReadOnlyList<PluginComponentDescriptor> Components,
    IReadOnlyList<PluginMcpServerDeclaration> DeclaredMcpServers,
    PluginOverlaySource OverlaySource);
