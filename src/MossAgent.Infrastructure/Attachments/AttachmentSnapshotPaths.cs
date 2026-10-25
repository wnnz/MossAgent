namespace MossAgent.Infrastructure.Attachments;

internal static class AttachmentSnapshotPaths
{
    public static void RejectLinks(string path)
    {
        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            FileSystemInfo entry = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (entry.LinkTarget is not null || entry.Exists && (entry.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Attachment snapshots cannot traverse links or reparse points.");
        }
    }

    public static bool TryProbeCandidate(AttachmentReclaimCandidate candidate, DateTimeOffset cutoffUtc)
    {
        try
        {
            var info = new FileInfo(candidate.Path);
            if (!info.Exists || info.LinkTarget is not null) return false;
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            if (info.Length != candidate.Length || info.LastWriteTimeUtc != candidate.LastWriteUtc) return false;
            if (info.LastWriteTimeUtc >= cutoffUtc) return false;
            RejectLinks(candidate.Path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    public static void DeleteByHandle(string path)
    {
        RejectLinks(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096,
            FileOptions.DeleteOnClose);
    }
}
