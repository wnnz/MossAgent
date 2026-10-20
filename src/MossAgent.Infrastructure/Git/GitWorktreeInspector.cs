using MossAgent.Domain;

namespace MossAgent.Infrastructure.Git;

internal sealed class GitWorktreeInspector(GitProcessRunner git)
{
    private async Task<GitWorktreeRepository> ReadAsync(GitWorktreeRepository expected, bool requireHead, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var arguments = new List<string> { $"--git-dir={expected.GitDirectory}", $"--work-tree={expected.Root}",
            "--no-optional-locks", "-c", "core.quotepath=false", "rev-parse",
            "--path-format=absolute", "--show-toplevel", "--git-common-dir", "--git-dir" };
        if (requireHead) arguments.AddRange(["--verify", "HEAD"]);
        var result = await git.RunAsync(expected.Root, arguments, cancellationToken).ConfigureAwait(false);
        var values = result.StandardOutput.TrimEnd('\r', '\n').Split('\n').Select(value => value.TrimEnd('\r')).ToArray();
        if (result.ExitCode != 0 || result.OutputTruncated || values.Length != (requireHead ? 4 : 3)
            || requireHead && !IsCommit(values[3]))
            throw new InvalidOperationException("Git repository identity is unavailable or incomplete.");
        var repository = new GitWorktreeRepository(GitWorktreePaths.Normalize(values[0]),
            GitWorktreePaths.Normalize(values[1]), GitWorktreePaths.Normalize(values[2]), requireHead ? values[3] : null);
        GitWorktreePaths.ValidateRepository(repository);
        if (!GitWorktreePaths.Same(repository.Root, expected.Root)
            || !GitWorktreePaths.Same(repository.GitDirectory, expected.GitDirectory)
            || !GitWorktreePaths.Same(repository.CommonDirectory, expected.CommonDirectory))
            throw new UnauthorizedAccessException("Git repository identity changed during inspection.");
        return repository;
    }

    public async Task<GitWorktreeRepository> ReadProjectAsync(ProjectProfile project, bool requireHead, CancellationToken cancellationToken)
    {
        var primary = GitWorktreePaths.Normalize(project.PrimaryDirectory);
        var root = GitWorktreeMetadata.FindRoot(primary);
        if (!IsAuthorized(project, root))
            throw new UnauthorizedAccessException("The complete Git repository must be explicitly authorized before managing worktrees.");
        var repository = GitWorktreeMetadata.Read(root);
        if (GitWorktreePaths.Same(repository.GitDirectory, repository.CommonDirectory)
            && !GitWorktreePaths.Within(repository.CommonDirectory, repository.Root)
            && !IsAuthorized(project, repository.CommonDirectory))
            throw new UnauthorizedAccessException("Standalone external Git metadata must be explicitly authorized before managing worktrees.");
        return await ReadAsync(repository, requireHead, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsAuthorized(ProjectProfile project, string path) =>
        project.AuthorizedDirectories.Any(root => GitWorktreePaths.Within(path, GitWorktreePaths.Normalize(root)));

    public async Task ValidateTargetAsync(GitWorktreeRepository source, string target, CancellationToken cancellationToken)
    {
        GitWorktreePaths.RejectLinks(Path.Combine(target, ".git"));
        if (!File.Exists(Path.Combine(target, ".git")))
            throw new UnauthorizedAccessException("The managed directory is not a linked Git worktree.");
        var expected = GitWorktreeMetadata.Read(target);
        var result = await git.RunAsync(source.Root, source.Command("worktree", "list", "--porcelain", "-z"), cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0 || result.OutputTruncated || !ContainsRegistration(result.StandardOutput, target))
            throw new UnauthorizedAccessException("The managed directory is not registered to the selected Git repository.");
        var repository = await ReadAsync(expected, false, cancellationToken).ConfigureAwait(false);
        var registrations = Path.Combine(source.CommonDirectory, "worktrees");
        if (!GitWorktreePaths.Same(repository.Root, target) || !GitWorktreePaths.Same(repository.CommonDirectory, source.CommonDirectory)
            || GitWorktreePaths.Same(repository.GitDirectory, registrations) || !GitWorktreePaths.Within(repository.GitDirectory, registrations))
            throw new UnauthorizedAccessException("The managed worktree Git identity does not match its registration.");
    }

    private static bool ContainsRegistration(string output, string target)
    {
        if (!output.EndsWith("\0\0", StringComparison.Ordinal)) return false;
        var found = false;
        foreach (var record in output.Split("\0\0", StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = record.Split('\0');
            if (fields.Length < 2 || !fields[0].StartsWith("worktree ", StringComparison.Ordinal)
                || !fields[1].StartsWith("HEAD ", StringComparison.Ordinal) || !IsCommit(fields[1][5..])
                || fields.Skip(1).Any(field => field.StartsWith("worktree ", StringComparison.Ordinal))) return false;
            found |= GitWorktreePaths.Same(GitWorktreePaths.Normalize(fields[0][9..]), target);
        }
        return found;
    }

    private static bool IsCommit(string value) => value.Length is 40 or 64 && value.All(char.IsAsciiHexDigit);
}
