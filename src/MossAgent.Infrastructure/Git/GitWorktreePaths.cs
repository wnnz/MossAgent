using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Infrastructure.Git;

internal static class GitWorktreePaths
{
    private static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string Normalize(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.Any(char.IsControl)
            || OperatingSystem.IsWindows() && (path.Replace('/', '\\').StartsWith(@"\\?\", StringComparison.Ordinal)
                || path.Replace('/', '\\').StartsWith(@"\\.\", StringComparison.Ordinal)
                || path[(Path.GetPathRoot(path)?.Length ?? 0)..].Contains(':')))
            throw new UnauthorizedAccessException("Worktree paths must be unambiguous absolute filesystem paths.");
        if (OperatingSystem.IsWindows() && path[(Path.GetPathRoot(path)?.Length ?? 0)..]
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Any(IsAmbiguousWindowsSegment))
            throw new UnauthorizedAccessException("Ambiguous worktree path components are not allowed.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static bool IsAmbiguousWindowsSegment(string segment)
    {
        if (segment is "." or "..") return false;
        if (segment.EndsWith(' ') || segment.EndsWith('.') || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return true;
        var stem = segment.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL"
            || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))
                && "123456789¹²³".Contains(stem[3]);
    }

    public static bool Same(string first, string second) => first.Equals(second, Comparison);

    public static bool Within(string candidate, string root)
    {
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return Same(candidate, root) || candidate.StartsWith(prefix, Comparison);
    }

    public static string ManagedTarget(AppDataPaths paths, string path, bool mustExist)
    {
        var root = Normalize(paths.WorktreesPath);
        var target = Normalize(path);
        if (!Same(Path.GetDirectoryName(target) ?? string.Empty, root)
            || !Guid.TryParseExact(Path.GetFileName(target), "N", out _))
            throw new UnauthorizedAccessException("Only direct managed task worktree directories are allowed.");
        RejectLinks(root);
        RejectLinks(target);
        if (mustExist) RequireDirectory(target);
        return target;
    }

    public static void RequireDirectory(string path)
    {
        RejectLinks(path);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("A required worktree directory is unavailable.");
    }

    public static void RejectLinks(string path)
    {
        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            FileSystemInfo entry = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (entry.LinkTarget is not null || entry.Exists && (entry.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Worktree paths and Git metadata cannot traverse links or reparse points.");
        }
    }

    public static void ValidateRepository(GitWorktreeRepository repository)
    {
        RequireDirectory(repository.Root);
        RequireDirectory(repository.CommonDirectory);
        RequireDirectory(repository.GitDirectory);
        foreach (var path in new[] { Path.Combine(repository.Root, ".git"), Path.Combine(repository.CommonDirectory, "worktrees"),
            Path.Combine(repository.CommonDirectory, "config"), Path.Combine(repository.GitDirectory, "config.worktree"),
            Path.Combine(repository.GitDirectory, "commondir"), Path.Combine(repository.GitDirectory, "gitdir"),
            Path.Combine(repository.GitDirectory, "HEAD"), Path.Combine(repository.GitDirectory, "index") })
            RejectLinks(path);
    }
}
