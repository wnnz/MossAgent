namespace MossAgent.Infrastructure.Plugins;

/// <summary>Resolves archive-relative component paths against a plugin's install directory.
/// The same containment rules guard install-time backfill and dispatch-time reads: empty,
/// rooted or '..'-carrying relative paths and any escape outside the plugin root are rejected.</summary>
internal static class PluginInstallPaths
{
    /// <summary>Full path of <paramref name="relativePath"/> inside the plugin's install
    /// directory, or null when the path violates containment. <paramref name="pluginsRoot"/>
    /// must already be a full path.</summary>
    public static string? Resolve(string pluginsRoot, string installPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized) || Path.IsPathFullyQualified(normalized)) return null;
        if (normalized.Split(Path.DirectorySeparatorChar).Any(segment => segment == "..")) return null;
        try
        {
            var rootPrefix = pluginsRoot.EndsWith(Path.DirectorySeparatorChar)
                ? pluginsRoot : pluginsRoot + Path.DirectorySeparatorChar;
            var installRoot = Path.GetFullPath(Path.Combine(pluginsRoot, installPath));
            if (!installRoot.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) return null;
            var installPrefix = installRoot.EndsWith(Path.DirectorySeparatorChar)
                ? installRoot : installRoot + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(installRoot, normalized));
            return candidate.StartsWith(installPrefix, StringComparison.OrdinalIgnoreCase) ? candidate : null;
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
