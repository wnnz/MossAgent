namespace MossAgent.Infrastructure.Plugins;

internal static class PluginEntryLookup
{
    public static PluginArchiveEntry? FindFile(IReadOnlyList<PluginArchiveEntry> entries, string normalizedName) =>
        entries.FirstOrDefault(entry => !entry.IsDirectory
            && entry.NormalizedName.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));
}
