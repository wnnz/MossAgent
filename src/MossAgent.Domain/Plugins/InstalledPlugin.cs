namespace MossAgent.Domain.Plugins;

/// <summary>A plugin package committed to disk and indexed in SQLite. <see cref="InstallPath"/>
/// is relative to the plugin root and immutable for the row's lifetime; the archive payload is
/// never stored in the database. <see cref="TrustRevision"/> is a monotonically increasing
/// counter owned by the store: it starts at 1 when a row is inserted or replaced and bumps on
/// every real state transition, so it identifies the trust generation the row's state was
/// reached under.</summary>
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
    PluginOverlaySource OverlaySource,
    long TrustRevision);
