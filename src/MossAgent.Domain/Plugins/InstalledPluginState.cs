namespace MossAgent.Domain.Plugins;

/// <summary>Lifecycle state of an installed plugin row. <see cref="Drifted"/> is the trust
/// state: the on-disk payload no longer matches the install-time component hashes, so the
/// row is marked by dispatch-time verification and refuses re-enablement until the plugin
/// is uninstalled and reinstalled.</summary>
public enum InstalledPluginState
{
    Installed,
    Enabled,
    Disabled,
    Drifted
}
