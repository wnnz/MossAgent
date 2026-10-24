namespace MossAgent.Infrastructure.Git;

internal static class GitWorktreeRegistrationSlot
{
    private const int MaximumEntries = 32;
    private const int MaximumRelativePath = 64;

    // Finds the single registration slot under the repository's commondir
    // "worktrees" directory whose gitdir file points back at <target>/.git.
    // Exactly one match is required; zero or multiple matches refuse cleanup.
    public static string Locate(GitWorktreeRepository source, string target)
    {
        var registrations = Registrations(source);
        var expected = Path.Combine(GitWorktreePaths.Normalize(target), ".git");
        var matches = Directory.Exists(registrations)
            ? Directory.EnumerateDirectories(registrations)
                .Where(slot => GitWorktreePaths.Same(ResolveFile(slot, "gitdir"), expected)).ToArray()
            : [];
        return matches.Length == 1 ? matches[0]
            : throw new InvalidOperationException(
                "The stale worktree registration slot cannot be identified unambiguously.");
    }

    // Deletes exactly the verified slot directory after a conservative content
    // check: slots hold only a handful of small metadata files, so unexpected
    // depth or entry counts refuse the delete instead of risking wider damage.
    public static void Delete(GitWorktreeRepository source, string slot)
    {
        var registrations = Registrations(source);
        if (!GitWorktreePaths.Same(Path.GetDirectoryName(slot) ?? string.Empty, registrations))
            throw new UnauthorizedAccessException("Worktree registration slots must be direct entries of the repository worktrees directory.");
        GitWorktreePaths.RejectLinks(slot);
        var entries = Directory.EnumerateFileSystemEntries(slot, "*", SearchOption.AllDirectories).ToArray();
        if (entries.Length > MaximumEntries
            || entries.Any(entry => entry.Length > slot.Length + MaximumRelativePath))
            throw new InvalidOperationException("The worktree registration slot contains unexpected content; refusing to delete it.");
        foreach (var entry in entries) GitWorktreePaths.RejectLinks(entry);
        Directory.Delete(slot, recursive: true);
    }

    public static string ResolveFile(string slot, string name)
    {
        var path = Path.Combine(slot, name);
        if (!File.Exists(path)) return string.Empty;
        var content = File.ReadLines(path).FirstOrDefault()?.Trim().Replace('/', Path.DirectorySeparatorChar) ?? string.Empty;
        return content.Length == 0 ? string.Empty
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.IsPathFullyQualified(content) ? content : Path.Combine(slot, content)));
    }

    private static string Registrations(GitWorktreeRepository source)
    {
        var registrations = Path.Combine(source.CommonDirectory, "worktrees");
        GitWorktreePaths.RejectLinks(registrations);
        return registrations;
    }
}
