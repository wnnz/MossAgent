namespace MossAgent.Infrastructure.Plugins;

/// <summary>Bounded-reading limits for local plugin package previews. Every limit is
/// enforced fail-closed before content is trusted.</summary>
internal static class PluginArchiveLimits
{
    public const int MaximumEntries = 512;
    public const long MaximumTotalUncompressedBytes = 32L * 1024 * 1024;
    public const long MaximumFileBytes = 4L * 1024 * 1024;
    public const int MaximumEntryNameLength = 260;
    public const int ManifestBytes = 64 * 1024;
    public const int McpManifestBytes = 32 * 1024;
    public const int CompatibilityManifestBytes = 64 * 1024;
    public const int JsonMaximumDepth = 32;
}
