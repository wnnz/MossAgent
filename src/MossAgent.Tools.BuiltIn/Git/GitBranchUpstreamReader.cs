using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

// Reads the verified upstream state behind git.branch_upstream: the target branch defaults to the
// current HEAD branch (detached HEAD without an explicit branch is refused), an explicit branch must
// exist under refs/heads, and the remote must be one of the configured git remote names.
public sealed class GitBranchUpstreamReader(AuthorizedPathResolver paths, IGitRepositoryAuthorization repositories,
    GitCommandRunner git)
{
    private const int QueryLimit = 1024 * 1024;
    private const int MaximumRemotes = 128;
    private const string HeadsPrefix = "refs/heads/";

    public void Validate(ToolRequest request, ToolExecutionContext context)
    {
        Directory(request, context);
        var arguments = request.Arguments;
        if (arguments.GetOptionalString("remote") is { } remote && !GitPushNames.IsRemote(remote)
            || arguments.GetOptionalString("branch") is { } branch && !GitPushNames.IsBranch(branch)
            || arguments.GetOptionalString("mergeRef") is { } merge && !IsMergeRef(merge))
            throw new ArgumentException(ToolExecutionText.Get("GitUpstreamSelectionInvalid"));
    }

    public async Task<GitBranchUpstreamState> ReadAsync(ToolRequest request, ToolExecutionContext context, CancellationToken token)
    {
        var repository = await repositories.AuthorizeAsync(Directory(request, context), context, token).ConfigureAwait(false);
        var names = (await QueryAsync(repository, ["remote"], context, token).ConfigureAwait(false))
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (names.Length > MaximumRemotes || names.Any(name => !GitPushNames.IsRemote(name))) throw Incomplete();
        var remote = request.Arguments.GetRequiredString("remote");
        if (!names.Contains(remote, StringComparer.Ordinal)) throw new GitRemotesException("git_remote_missing", "GitRemoteMissing");
        var requested = request.Arguments.GetOptionalString("branch");
        var branch = requested ?? await HeadAsync(repository, context, token).ConfigureAwait(false);
        if (requested is not null) await RequireBranchAsync(repository, branch, context, token).ConfigureAwait(false);
        var config = await QueryAsync(repository, ["config", "--null", "--list"], context, token).ConfigureAwait(false);
        return new(repository, branch, Config(config, $"branch.{branch}.remote"), Config(config, $"branch.{branch}.merge"));
    }

    private async Task<string> HeadAsync(ToolGitRepository repository, ToolExecutionContext context, CancellationToken token)
    {
        var head = (await QueryAsync(repository, ["symbolic-ref", "--quiet", "HEAD"], context, token, true).ConfigureAwait(false)).TrimEnd('\r', '\n');
        if (head.Length == 0) throw new GitRemotesException("git_upstream_detached", "GitUpstreamDetached");
        if (!head.StartsWith(HeadsPrefix, StringComparison.Ordinal)) throw Incomplete();
        return head[HeadsPrefix.Length..];
    }

    // rev-parse --verify --quiet prints the commit for an existing branch and exits 1 silently for a
    // missing one, so accepted empty output means the ref is absent rather than the read failing.
    private async Task RequireBranchAsync(ToolGitRepository repository, string branch, ToolExecutionContext context, CancellationToken token)
    {
        var head = (await QueryAsync(repository, ["rev-parse", "--verify", "--quiet", HeadsPrefix + branch], context, token, true).ConfigureAwait(false)).TrimEnd('\r', '\n');
        if (head.Length is not (40 or 64) || !head.All(char.IsAsciiHexDigit))
            throw new GitRemotesException("git_branch_missing", "GitBranchMissing");
    }

    private string Directory(ToolRequest request, ToolExecutionContext context) =>
        paths.Resolve(request.Arguments.GetOptionalString("repository") ?? context.WorkingDirectory, context);

    private static bool IsMergeRef(string value) => value.StartsWith(HeadsPrefix, StringComparison.Ordinal)
        && GitPushNames.IsBranch(value[HeadsPrefix.Length..]);

    private static string? Config(string config, string name) =>
        config.Split('\0').LastOrDefault(entry => entry.StartsWith(name + "\n", StringComparison.Ordinal))?[(name.Length + 1)..];

    private async Task<string> QueryAsync(ToolGitRepository repository, string[] arguments, ToolExecutionContext context,
        CancellationToken token, bool acceptMissing = false)
    {
        var result = await git.RunAsync(repository.Root, repository.Command(["--no-optional-locks", "-c", "core.fsmonitor=false", .. arguments]),
            context with { MaximumOutputCharacters = QueryLimit }, token, acceptMissing).ConfigureAwait(false);
        if (!result.IsSuccess || result.OutputCharacterCount >= QueryLimit) throw Incomplete();
        return result.Content ?? string.Empty;
    }

    private static GitRemotesException Incomplete() => new("git_upstream_read_failed", "GitUpstreamReadFailed");
}
