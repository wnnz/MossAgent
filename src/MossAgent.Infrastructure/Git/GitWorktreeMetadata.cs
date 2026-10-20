using System.Text;

namespace MossAgent.Infrastructure.Git;

internal static class GitWorktreeMetadata
{
    public static string FindRoot(string directory)
    {
        directory = GitWorktreePaths.Normalize(directory);
        GitWorktreePaths.RequireDirectory(directory);
        for (var current = directory; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            var metadata = Path.Combine(current, ".git");
            GitWorktreePaths.RejectLinks(metadata);
            if (Directory.Exists(metadata) || File.Exists(metadata)) return current;
        }
        throw new InvalidOperationException("The selected directory is not in an ordinary Git repository.");
    }

    public static GitWorktreeRepository Read(string root)
    {
        var metadata = Path.Combine(root, ".git");
        GitWorktreePaths.RejectLinks(metadata);
        var gitDirectory = metadata;
        if (!Directory.Exists(metadata))
        {
            var pointer = ReadPointer(metadata);
            if (!pointer.StartsWith("gitdir: ", StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Git worktree metadata is invalid.");
            gitDirectory = Resolve(root, pointer[8..]);
        }
        GitWorktreePaths.RequireDirectory(gitDirectory);
        var commonPointer = Path.Combine(gitDirectory, "commondir");
        GitWorktreePaths.RejectLinks(commonPointer);
        var commonDirectory = File.Exists(commonPointer) ? Resolve(gitDirectory, ReadPointer(commonPointer)) : gitDirectory;
        var repository = new GitWorktreeRepository(root, commonDirectory, gitDirectory, null);
        GitWorktreePaths.ValidateRepository(repository);
        if (!GitWorktreePaths.Same(commonDirectory, gitDirectory))
        {
            var registrations = Path.Combine(commonDirectory, "worktrees");
            if (!GitWorktreePaths.Same(Path.GetDirectoryName(gitDirectory) ?? string.Empty, registrations)
                || !GitWorktreePaths.Same(Resolve(gitDirectory, ReadPointer(Path.Combine(gitDirectory, "gitdir"))), metadata))
                throw new UnauthorizedAccessException("Git worktree metadata does not point back to the selected directory.");
        }
        return repository;
    }

    private static string Resolve(string directory, string pointer)
    {
        if (string.IsNullOrEmpty(pointer) || pointer.Any(char.IsControl)
            || Path.IsPathRooted(pointer) && !Path.IsPathFullyQualified(pointer))
            throw new UnauthorizedAccessException("Git metadata contains an ambiguous path.");
        var path = GitWorktreePaths.Normalize(Path.IsPathFullyQualified(pointer) ? pointer : Path.Combine(directory, pointer));
        GitWorktreePaths.RejectLinks(path);
        return path;
    }

    private static string ReadPointer(string path)
    {
        GitWorktreePaths.RejectLinks(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is 0 or > 4096) throw new UnauthorizedAccessException("Git metadata exceeds the supported path limit.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        var buffer = new char[4097];
        var length = reader.ReadBlock(buffer, 0, buffer.Length);
        if (length == buffer.Length) throw new UnauthorizedAccessException("Git metadata exceeds the supported path limit.");
        return new string(buffer, 0, length).TrimEnd('\r', '\n');
    }
}
