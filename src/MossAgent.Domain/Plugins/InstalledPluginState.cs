namespace MossAgent.Domain.Plugins;

/// <summary>Lifecycle state of an installed plugin row. This slice only produces
/// <see cref="Installed"/>; enablement and disablement land with a later slice.</summary>
public enum InstalledPluginState
{
    Installed,
    Enabled,
    Disabled
}
