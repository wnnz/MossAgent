namespace MossAgent.Domain.Plugins;

/// <summary>Which compatibility overlay a package manifest selects. An object-valued
/// <c>extensions.com.openai</c> replaces the entire legacy file; they are never merged.</summary>
public enum PluginOverlaySource
{
    None,
    CompatibilityFile,
    ExtensionsObject
}
