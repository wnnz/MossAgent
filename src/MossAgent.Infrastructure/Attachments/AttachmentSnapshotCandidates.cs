using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Infrastructure.Attachments;

internal static class AttachmentSnapshotCandidates
{
    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public static IReadOnlyList<AttachmentReclaimCandidate> Enumerate(AppDataPaths paths, string? scopeHash = null)
    {
        var result = new List<AttachmentReclaimCandidate>();
        if (scopeHash is not null)
        {
            CollectDraftScope(Path.Combine(DraftsRoot(paths), scopeHash), scopeHash, result);
            return result;
        }
        var artifacts = Path.GetFullPath(paths.ArtifactsPath);
        foreach (var taskDirectory in Directories(artifacts))
        {
            if (!Guid.TryParseExact(Path.GetFileName(taskDirectory), "N", out var taskId)) continue;
            foreach (var container in Directories(Path.Combine(taskDirectory, "attachments")))
            {
                if (!Guid.TryParseExact(Path.GetFileName(container), "N", out var containerId)) continue;
                foreach (var file in Files(container))
                    if (ClassifyMessage(file, taskId, containerId, container) is { } candidate) result.Add(candidate);
            }
        }
        var draftsRoot = DraftsRoot(paths);
        foreach (var scopeDirectory in Directories(draftsRoot))
        {
            var hash = Path.GetFileName(scopeDirectory);
            if (hash.Length == 64 && hash.All(Uri.IsHexDigit)) CollectDraftScope(scopeDirectory, hash, result);
        }
        return result;
    }

    private static void CollectDraftScope(string scopeDirectory, string scopeHash,
        List<AttachmentReclaimCandidate> result)
    {
        if (!Directory.Exists(scopeDirectory)) return;
        foreach (var file in Files(scopeDirectory))
            if (ClassifyDraft(file, scopeHash, scopeDirectory) is { } candidate) result.Add(candidate);
    }

    private static string DraftsRoot(AppDataPaths paths) =>
        Path.GetFullPath(Path.Combine(paths.Root, "draft-attachments"));

    private static AttachmentReclaimCandidate? ClassifyMessage(FileInfo file, Guid taskId, Guid containerId,
        string directory)
    {
        if (TrySnapshotName(file.Name, out var attachmentId))
            return new(file.FullName, AttachmentCandidateKind.Message, taskId, containerId, attachmentId, null,
                directory, file.Length, file.LastWriteTimeUtc);
        if (IsTemporaryName(file.Name))
            return new(file.FullName, AttachmentCandidateKind.Temporary, taskId, containerId, null, null,
                directory, file.Length, file.LastWriteTimeUtc);
        return null;
    }

    private static AttachmentReclaimCandidate? ClassifyDraft(FileInfo file, string scopeHash, string directory)
    {
        if (TrySnapshotName(file.Name, out var attachmentId))
            return new(file.FullName, AttachmentCandidateKind.Draft, null, null, attachmentId, scopeHash,
                directory, file.Length, file.LastWriteTimeUtc);
        if (IsTemporaryName(file.Name))
            return new(file.FullName, AttachmentCandidateKind.Temporary, null, null, null, scopeHash,
                directory, file.Length, file.LastWriteTimeUtc);
        return null;
    }

    private static bool TrySnapshotName(string name, out Guid attachmentId)
    {
        attachmentId = default;
        var extension = Path.GetExtension(name);
        if (!extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return false;
        return Guid.TryParseExact(Path.GetFileNameWithoutExtension(name), "N", out attachmentId);
    }

    private static bool IsTemporaryName(string name)
    {
        if (!name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return false;
        var remainder = name[..^4];
        var separator = remainder.LastIndexOf('.');
        if (separator < 0) return false;
        var stamped = remainder[..separator];
        if (!Guid.TryParseExact(remainder[(separator + 1)..], "N", out _)) return false;
        return TrySnapshotName(stamped, out _);
    }

    private static IEnumerable<string> Directories(string root)
    {
        try { return Directory.EnumerateDirectories(root, "*", Options).ToArray(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }

    private static IEnumerable<FileInfo> Files(string directory)
    {
        try
        {
            return new DirectoryInfo(directory).EnumerateFiles("*", Options)
                .Where(file => file.LinkTarget is null)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }
}
