using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

// Reads the verified remote state behind git.remote_set_url: the remote must exist in git remote, every
// leg is resolved through get-url (so url.*.insteadOf rewrites classify on the effective endpoint) and
// the remote.<name>.vcs helper marker pins the method to unsupported. Read-compare-write is sequential
// in one process; a concurrent external change between the compare and the write is not atomic, so the
// result is re-read after writing and reported instead of assumed.
public sealed class GitRemoteUrlReader(AuthorizedPathResolver paths, IGitRepositoryAuthorization repositories,
    GitCommandRunner git)
{
    private const int QueryLimit = 1024 * 1024;
    private const int MaximumRemotes = 128;

    public void Validate(ToolRequest request, ToolExecutionContext context)
    {
        Directory(request, context);
        var arguments = request.Arguments;
        if (arguments.GetOptionalString("remote") is { } remote && !GitPushNames.IsRemote(remote)
            || InvalidUrl(arguments.GetOptionalString("url")) || InvalidUrl(arguments.GetOptionalString("pushUrl")))
            throw new ArgumentException(ToolExecutionText.Get("GitRemoteSelectionInvalid"));
        if (arguments.TryGetProperty("url", out _) && !arguments.TryGetProperty("expectedFetch", out _)
            || arguments.TryGetProperty("pushUrl", out _) && !arguments.TryGetProperty("expectedPush", out _))
            throw new ArgumentException(ToolExecutionText.Get("GitRemoteExpectationMissing"));
        GitRemoteUrlExpectation.Parse(arguments, "expectedFetch");
        GitRemoteUrlExpectation.Parse(arguments, "expectedPush");
    }

    public async Task<GitRemoteUrlState> ReadAsync(ToolRequest request, ToolExecutionContext context, CancellationToken token)
    {
        var repository = await repositories.AuthorizeAsync(Directory(request, context), context, token).ConfigureAwait(false);
        var names = (await QueryAsync(repository, ["remote"], context, token).ConfigureAwait(false))
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (names.Length > MaximumRemotes || names.Any(name => !GitPushNames.IsRemote(name))) throw Incomplete();
        var name = request.Arguments.GetRequiredString("remote");
        if (!names.Contains(name, StringComparer.Ordinal)) throw new GitRemotesException("git_remote_missing", "GitRemoteMissing");
        var branch = (await QueryAsync(repository, ["symbolic-ref", "--quiet", "HEAD"], context, token, true).ConfigureAwait(false)).TrimEnd('\r', '\n');
        if (branch.Length > 0 && !branch.StartsWith("refs/heads/", StringComparison.Ordinal)) throw Incomplete();
        var config = await QueryAsync(repository, ["config", "--null", "--list"], context, token).ConfigureAwait(false);
        var vcs = Config(config, $"remote.{name}.vcs") is not null;
        var fetch = await SideAsync(repository, name, config, $"remote.{name}.url", false, vcs, context, token).ConfigureAwait(false);
        var push = await SideAsync(repository, name, config, $"remote.{name}.pushurl", true, vcs, context, token).ConfigureAwait(false);
        return new(repository, name, fetch, push, vcs, branch.Length == 0 ? null : branch["refs/heads/".Length..]);
    }

    private async Task<GitRemoteUrlSide> SideAsync(ToolGitRepository repository, string name, string config, string key,
        bool push, bool vcs, ToolExecutionContext context, CancellationToken token)
    {
        var urls = await TryQueryAsync(repository,
            ["remote", "get-url", .. push ? new[] { "--push" } : Array.Empty<string>(), "--all", "--", name], context, token).ConfigureAwait(false);
        var transport = urls.Count > 0 ? GitRemoteEndpoints.Classify(urls[0]) : null;
        var method = vcs ? GitRemoteCredentialMethod.Unsupported : transport?.Method;
        return new(HasConfig(config, key), urls.Count, transport, method);
    }

    private string Directory(ToolRequest request, ToolExecutionContext context) =>
        paths.Resolve(request.Arguments.GetOptionalString("repository") ?? context.WorkingDirectory, context);

    private static bool InvalidUrl(string? url) => url is not null && url.Any(char.IsControl);

    private static bool HasConfig(string config, string name) =>
        config.Split('\0').Any(entry => entry.StartsWith(name + "\n", StringComparison.Ordinal));

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

    private async Task<IReadOnlyList<string>> TryQueryAsync(ToolGitRepository repository, string[] arguments,
        ToolExecutionContext context, CancellationToken token)
    {
        var result = await git.RunAsync(repository.Root, repository.Command(["--no-optional-locks", "-c", "core.fsmonitor=false", .. arguments]),
            context with { MaximumOutputCharacters = QueryLimit }, token).ConfigureAwait(false);
        if (!result.IsSuccess) return [];
        if (result.OutputCharacterCount >= QueryLimit) throw Incomplete();
        return (result.Content ?? string.Empty).Split('\n').Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 0).ToArray();
    }

    private static GitRemotesException Incomplete() => new("git_remotes_read_failed", "GitRemotesReadFailed");
}
