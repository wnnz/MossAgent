using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitRemoteStateReader(AuthorizedPathResolver paths, IGitRepositoryAuthorization repositories, GitCommandRunner git)
{
    private const int QueryLimit = 1024 * 1024;
    private const int MaximumRemotes = 128;
    private const int MaximumHelpers = 64;
    private const int MaximumHelperValue = 256;
    private const string HelperPrefix = "credential.helper\n";

    public void Validate(ToolRequest request, ToolExecutionContext context) => Directory(request, context);

    public async Task<GitRemotesReport> ReadAsync(ToolRequest request, ToolExecutionContext context, CancellationToken token)
    {
        var repository = await repositories.AuthorizeAsync(Directory(request, context), context, token).ConfigureAwait(false);
        var branch = (await QueryAsync(repository, ["symbolic-ref", "--quiet", "HEAD"], context, token, true).ConfigureAwait(false)).TrimEnd('\r', '\n');
        if (branch.Length > 0 && !branch.StartsWith("refs/heads/", StringComparison.Ordinal)) throw Incomplete();
        var source = branch.Length == 0 ? null : branch["refs/heads/".Length..];
        var config = await QueryAsync(repository, ["config", "--null", "--list"], context, token).ConfigureAwait(false);
        var names = (await QueryAsync(repository, ["remote"], context, token).ConfigureAwait(false))
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (names.Length > MaximumRemotes || names.Any(name => !GitPushNames.IsRemote(name))) throw Incomplete();
        var remotes = new List<GitRemoteEntry>(names.Length);
        foreach (var name in names)
            remotes.Add(await EntryAsync(repository, name, config, context, token).ConfigureAwait(false));
        var upstream = source is null ? null : new GitRemotesUpstream(source, Config(config, $"branch.{source}.remote"),
            Config(config, $"branch.{source}.merge"), Config(config, $"branch.{source}.pushremote"));
        var helpers = Helpers(config);
        return new(remotes, upstream, helpers.Entries, helpers.Truncated);
    }

    private string Directory(ToolRequest request, ToolExecutionContext context) =>
        paths.Resolve(request.Arguments.GetOptionalString("repository") ?? context.WorkingDirectory, context);

    private async Task<GitRemoteEntry> EntryAsync(ToolGitRepository repository, string name, string config,
        ToolExecutionContext context, CancellationToken token)
    {
        var fetchUrls = await TryQueryAsync(repository, ["remote", "get-url", "--all", "--", name], context, token).ConfigureAwait(false);
        var pushUrls = await TryQueryAsync(repository, ["remote", "get-url", "--push", "--all", "--", name], context, token).ConfigureAwait(false);
        var fetch = fetchUrls.Count > 0 ? GitRemoteEndpoints.Classify(fetchUrls[0]) : null;
        var push = pushUrls.Count > 0 ? GitRemoteEndpoints.Classify(pushUrls[0]) : null;
        var method = Config(config, $"remote.{name}.vcs") is not null ? GitRemoteCredentialMethod.Unsupported : Merge(fetch, push);
        return new(name, Label(fetch), Label(push), method, fetch?.Endpoint != push?.Endpoint,
            pushUrls.Count > 1 ? pushUrls.Count : null);
    }

    private static GitRemoteCredentialMethod Merge(GitRemoteTransport? fetch, GitRemoteTransport? push)
    {
        if (fetch?.Method is GitRemoteCredentialMethod.Embedded || push?.Method is GitRemoteCredentialMethod.Embedded)
            return GitRemoteCredentialMethod.Embedded;
        if (fetch is null && push is null
            || fetch?.Method is GitRemoteCredentialMethod.Unsupported || push?.Method is GitRemoteCredentialMethod.Unsupported)
            return GitRemoteCredentialMethod.Unsupported;
        return (push ?? fetch)!.Method;
    }

    private static string? Label(GitRemoteTransport? transport) => transport is null ? null
        : transport.Method is GitRemoteCredentialMethod.Local ? ToolExecutionText.Get("GitRemoteLocalEndpoint") : transport.Endpoint;

    private static (List<GitCredentialHelperEntry> Entries, bool Truncated) Helpers(string config)
    {
        var values = config.Split('\0').Where(entry => entry.StartsWith(HelperPrefix, StringComparison.Ordinal)).ToList();
        var entries = values.Take(MaximumHelpers).Select(entry => entry[HelperPrefix.Length..])
            .Select(value => new GitCredentialHelperEntry(
                value.Any(char.IsControl) ? null : value.Length > MaximumHelperValue ? value[..MaximumHelperValue] : value,
                value.StartsWith('!'))).ToList();
        return (entries, values.Count > MaximumHelpers);
    }

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

    private static string? Config(string config, string name) =>
        config.Split('\0').LastOrDefault(entry => entry.StartsWith(name + "\n", StringComparison.Ordinal))?[(name.Length + 1)..];

    private static GitRemotesException Incomplete() => new("git_remotes_read_failed", "GitRemotesReadFailed");
}
