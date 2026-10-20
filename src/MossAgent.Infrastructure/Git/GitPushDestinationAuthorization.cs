using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.Infrastructure.Git;

public sealed class GitPushDestinationAuthorization(IGitRepositoryAuthorization repositories, GitProcessRunner git) : IGitPushDestinationAuthorization
{
    public async Task<ToolGitRepository> AuthorizeAsync(string directory, string targetBranch,
        ToolExecutionContext context, CancellationToken cancellationToken)
    {
        if (!GitPushNames.IsBranch(targetBranch)) throw new UnauthorizedAccessException("Invalid push branch.");
        var root = GitWorktreePaths.Normalize(directory);
        GitWorktreePaths.RequireDirectory(root);
        if (!context.AuthorizedRoots.Any(authorized => GitWorktreePaths.Within(root, GitWorktreePaths.Normalize(authorized))))
            throw new UnauthorizedAccessException("The push destination requires explicit directory authorization.");
        ToolGitRepository repository;
        if (File.Exists(Path.Combine(root, ".git")) || Directory.Exists(Path.Combine(root, ".git")))
            repository = await repositories.AuthorizeAsync(root, context, cancellationToken).ConfigureAwait(false);
        else
        {
            var expected = new GitWorktreeRepository(root, root, root, null);
            GitWorktreePaths.ValidateRepository(expected);
            var result = await git.RunAsync(root, ["--no-optional-locks", "rev-parse", "--is-bare-repository",
                "--path-format=absolute", "--absolute-git-dir", "--git-common-dir"], cancellationToken).ConfigureAwait(false);
            var fields = result.StandardOutput.TrimEnd('\r', '\n').Split('\n').Select(value => value.TrimEnd('\r')).ToArray();
            if (result.ExitCode != 0 || result.OutputTruncated || fields.Length != 3 || fields[0] != "true"
                || !GitWorktreePaths.Same(GitWorktreePaths.Normalize(fields[1]), root)
                || !GitWorktreePaths.Same(GitWorktreePaths.Normalize(fields[2]), root))
                throw new UnauthorizedAccessException("The local push destination is not an authorized repository.");
            repository = new(root, root, root);
        }
        foreach (var metadata in new[] { repository.GitDirectory, repository.CommonDirectory })
        {
            foreach (var relative in new[] { "objects", "objects/pack", "objects/info", "objects/info/alternates", "refs",
                "refs/heads/" + targetBranch, "packed-refs", "config", "HEAD", "logs/refs/heads/" + targetBranch })
                GitWorktreePaths.RejectLinks(Path.Combine(metadata, relative));
            foreach (var relative in new[] { "objects/info/alternates", "objects/info/http-alternates" })
                if (File.Exists(Path.Combine(metadata, relative)) && new FileInfo(Path.Combine(metadata, relative)).Length > 0)
                    throw new UnauthorizedAccessException("Local push destinations with alternate object stores are not supported.");
            foreach (var relative in new[] { "objects", "refs", "logs" })
                InspectTree(Path.Combine(metadata, relative), cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return repository;
    }

    private static void InspectTree(string root, CancellationToken token)
    {
        if (!Directory.Exists(root)) return;
        var pending = new Stack<string>();
        pending.Push(root);
        var entries = 0;
        while (pending.TryPop(out var directory))
        {
            token.ThrowIfCancellationRequested();
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                token.ThrowIfCancellationRequested();
                if (++entries > 100_000) throw new UnauthorizedAccessException("Local push destination inspection exceeded its bound.");
                GitWorktreePaths.RejectLinks(path);
                if (Directory.Exists(path)) pending.Push(path);
            }
        }
    }
}
