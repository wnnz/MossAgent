namespace MossAgent.Application.Plugins;

/// <summary>Stable machine-readable rejection codes carried by <see cref="PluginPackageException"/>.
/// The set is closed: preview never silently degrades an unsafe package into a success.</summary>
public static class PluginPackageErrorCode
{
    public const string Unreadable = "plugin_package_unreadable";
    public const string EntryInvalid = "plugin_package_entry_invalid";
    public const string TooLarge = "plugin_package_too_large";
    public const string ManifestMissing = "plugin_manifest_missing";
    public const string ManifestInvalid = "plugin_manifest_invalid";
    public const string McpInvalid = "plugin_mcp_invalid";
}
