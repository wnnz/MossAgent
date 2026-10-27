using System.IO.Compression;
using MossAgent.Application.Plugins;

namespace MossAgent.Infrastructure.Plugins;

/// <summary>Fail-closed archive hygiene: rejects unsafe names, link/reparse entries and
/// decompression bombs before any manifest is trusted. Directory entries count toward the
/// entry limit but not toward file-size limits.</summary>
internal static class PluginArchiveEntryNormalizer
{
    private const int SymlinkFileType = 0xA000;
    private const int UnixFileTypeMask = 0xF000;
    private const int ReparsePointAttribute = 0x400;

    public static IReadOnlyList<PluginArchiveEntry> Normalize(IReadOnlyList<ZipArchiveEntry> entries)
    {
        if (entries.Count > PluginArchiveLimits.MaximumEntries)
            throw TooLarge($"entry count {entries.Count}");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<PluginArchiveEntry>(entries.Count);
        var totalBytes = 0L;
        foreach (var entry in entries)
        {
            var isDirectory = entry.FullName.EndsWith('/');
            var name = NormalizeName(entry.FullName);
            CheckAttributes(entry);
            if (!seen.Add(name)) throw Invalid($"duplicate entry name '{name}'");
            if (!isDirectory) totalBytes = AccumulateFile(entry, totalBytes);
            normalized.Add(new PluginArchiveEntry(entry, name, isDirectory, entry.Length));
        }
        return normalized;
    }

    private static long AccumulateFile(ZipArchiveEntry entry, long totalBytes)
    {
        if (entry.Length > PluginArchiveLimits.MaximumFileBytes)
            throw TooLarge($"entry '{entry.FullName}' exceeds the single-file limit");
        totalBytes += entry.Length;
        if (totalBytes > PluginArchiveLimits.MaximumTotalUncompressedBytes)
            throw TooLarge("archive exceeds the total uncompressed size limit");
        return totalBytes;
    }

    private static string NormalizeName(string fullName)
    {
        if (fullName.Length > PluginArchiveLimits.MaximumEntryNameLength)
            throw TooLarge($"entry name exceeds {PluginArchiveLimits.MaximumEntryNameLength} characters");
        var body = fullName.EndsWith('/') ? fullName[..^1] : fullName;
        var segments = body.Split('/');
        foreach (var segment in segments)
            if (segment.Length == 0 || segment is "." or ".."
                || segment.Contains('\\') || segment.Contains(':'))
                throw Invalid($"unsafe entry name '{fullName}'");
        return body;
    }

    private static void CheckAttributes(ZipArchiveEntry entry)
    {
        var attributes = entry.ExternalAttributes;
        if (((attributes >> 16) & UnixFileTypeMask) == SymlinkFileType)
            throw Invalid($"link entry '{entry.FullName}'");
        if ((attributes & ReparsePointAttribute) != 0)
            throw Invalid($"reparse entry '{entry.FullName}'");
    }

    private static PluginPackageException Invalid(string detail) => new(PluginPackageErrorCode.EntryInvalid, detail);

    private static PluginPackageException TooLarge(string detail) => new(PluginPackageErrorCode.TooLarge, detail);
}
