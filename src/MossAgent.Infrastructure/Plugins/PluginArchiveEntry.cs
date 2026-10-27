using System.IO.Compression;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>A zip entry whose <see cref="NormalizedName"/> passed safety validation. The name
/// keeps '/' separators, has no trailing directory slash and is unique under OrdinalIgnoreCase.</summary>
internal sealed record PluginArchiveEntry(ZipArchiveEntry Entry, string NormalizedName, bool IsDirectory, long Length)
{
    public string[] Segments => NormalizedName.Split('/');
}
