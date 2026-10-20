using System.Security.Cryptography;
using System.Text;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Files;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitCommitStateReader(AuthorizedPathResolver paths, IGitRepositoryAuthorization repositories, GitCommandRunner git)
{
    private const int QueryLimit = 8 * 1024 * 1024;

    public void Validate(ToolRequest request, ToolExecutionContext context) => Directory(request, context);

    public async Task<GitCommitSnapshot> CaptureAsync(ToolRequest request, ToolExecutionContext context, CancellationToken token)
    {
        var directory = Directory(request, context);
        var repository = await repositories.AuthorizeAsync(directory, context, token).ConfigureAwait(false);
        var first = await ReadStateAsync(repository, context, token).ConfigureAwait(false);
        var files = await QueryAsync(repository, ["diff", "--cached", "--name-only", "-z", "--no-ext-diff", "--no-textconv"], context, token).ConfigureAwait(false);
        if (files.Length > 0 && !files.EndsWith('\0')) throw new GitCommitException("git_preview_incomplete", "GitCommitReadFailed");
        var current = await repositories.AuthorizeAsync(directory, context, token).ConfigureAwait(false);
        var second = await ReadStateAsync(current, context, token).ConfigureAwait(false);
        if (first.State != second.State) throw new GitCommitException("git_state_changed", "GitCommitStateChanged");
        return new(repository, first with { StagedFiles = files.Count(character => character == '\0') });
    }

    private string Directory(ToolRequest request, ToolExecutionContext context) =>
        paths.Resolve(request.Arguments.GetOptionalString("repository") ?? context.WorkingDirectory, context);

    private async Task<GitCommitPreview> ReadStateAsync(ToolGitRepository repository, ToolExecutionContext context, CancellationToken token)
    {
        var head = (await QueryAsync(repository, ["rev-parse", "--verify", "--quiet", "HEAD"], context, token, true).ConfigureAwait(false)).TrimEnd('\r', '\n');
        var branch = (await QueryAsync(repository, ["symbolic-ref", "--quiet", "HEAD"], context, token, true).ConfigureAwait(false)).TrimEnd('\r', '\n');
        if (head.Length > 0 && (head.Length is not (40 or 64) || !head.All(char.IsAsciiHexDigit))
            || branch.Length > 0 && !branch.StartsWith("refs/heads/", StringComparison.Ordinal)
            || head.Length == 0 && branch.Length == 0)
            throw new GitCommitException("git_preview_incomplete", "GitCommitReadFailed");
        var index = await QueryAsync(repository, ["ls-files", "--stage", "-z"], context, token).ConfigureAwait(false);
        foreach (var entry in index.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = entry.IndexOf('\t');
            if (separator < 2 || entry[separator - 1] != '0') throw new GitCommitException("git_unmerged", "GitCommitConflicts");
        }
        var state = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\0',
            repository.Root, repository.GitDirectory, repository.CommonDirectory, head, branch, index))));
        return new(state, 0, head.Length == 0 ? null : head, branch.Length == 0 ? null : branch);
    }

    private async Task<string> QueryAsync(ToolGitRepository repository, string[] arguments, ToolExecutionContext context,
        CancellationToken token, bool acceptMissing = false)
    {
        var result = await git.RunAsync(repository.Root, repository.Command(["--no-optional-locks", "-c", "core.fsmonitor=false", .. arguments]),
            context with { MaximumOutputCharacters = QueryLimit }, token, acceptMissing).ConfigureAwait(false);
        if (!result.IsSuccess || result.OutputCharacterCount >= QueryLimit)
            throw new GitCommitException("git_preview_incomplete", "GitCommitReadFailed");
        return result.Content ?? string.Empty;
    }
}
