using System.Text;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>Computes the immutable install directory name for a package:
/// {sanitized-name}-{sanitized-version}-{hash-prefix}. Every non-ASCII-alphanumeric character
/// becomes '-', so the result is always a single safe path segment and identical content
/// always lands on the same directory.</summary>
internal static class PluginInstallDirectory
{
    private const int SegmentLimit = 40;
    private const int HashPrefix = 8;

    public static string Name(string pluginName, string version, string contentHash)
    {
        var hash = contentHash.Length <= HashPrefix ? contentHash : contentHash[..HashPrefix];
        return $"{Sanitize(pluginName)}-{Sanitize(version)}-{hash}";
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(Math.Min(value.Length, SegmentLimit));
        foreach (var character in value)
        {
            if (builder.Length >= SegmentLimit) break;
            builder.Append(char.IsAsciiLetterOrDigit(character) ? character : '-');
        }
        var sanitized = builder.ToString().Trim('-');
        return sanitized.Length == 0 ? "plugin" : sanitized;
    }
}
