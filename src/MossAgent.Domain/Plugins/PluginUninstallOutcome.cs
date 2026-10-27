namespace MossAgent.Domain.Plugins;

/// <summary>Result of removing an installed plugin: the deleted row plus whether the
/// extracted payload directory is gone. When <see cref="PayloadRemoved"/> is false the row
/// is still deleted; the directory stays on disk as an orphan that startup reconciliation
/// reports without deleting.</summary>
public sealed record PluginUninstallOutcome(InstalledPlugin Plugin, bool PayloadRemoved);
