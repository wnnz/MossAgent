using System.Security.Cryptography;
using System.Text;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Files;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitPushStateReader(AuthorizedPathResolver paths, IGitRepositoryAuthorization repositories,
    GitCommandRunner git, GitPushEndpoints endpoints)
{
    private const int QueryLimit = 1024 * 1024;

    public void Validate(ToolRequest request, ToolExecutionContext context)
    {
        Directory(request, context);
        if (request.Arguments.GetOptionalString("remote") is { } remote && !GitPushNames.IsRemote(remote)
            || request.Arguments.GetOptionalString("targetBranch") is { } branch && !GitPushNames.IsBranch(branch))
            throw new ArgumentException(Resources.ToolExecutionText.Get("GitPushSelectionInvalid"));
    }

    public async Task<GitPushSnapshot> CaptureAsync(ToolRequest request, ToolExecutionContext context, CancellationToken token)
    {
        var directory = Directory(request, context);
        var first = await ReadAsync(await repositories.AuthorizeAsync(directory, context, token).ConfigureAwait(false), request, context, token).ConfigureAwait(false);
        var second = await ReadAsync(await repositories.AuthorizeAsync(directory, context, token).ConfigureAwait(false), request, context, token).ConfigureAwait(false);
        if (first.Preview.State != second.Preview.State) throw new GitPushException("git_state_changed", "GitPushStateChanged");
        return second;
    }

    private string Directory(ToolRequest request, ToolExecutionContext context) =>
        paths.Resolve(request.Arguments.GetOptionalString("repository") ?? context.WorkingDirectory, context);

    private async Task<GitPushSnapshot> ReadAsync(ToolGitRepository repository, ToolRequest request, ToolExecutionContext context, CancellationToken token)
    {
        var head = (await QueryAsync(repository, ["rev-parse", "--verify", "--quiet", "HEAD"], context, token, true).ConfigureAwait(false)).TrimEnd('\r', '\n');
        if (head.Length is not (40 or 64) || !head.All(char.IsAsciiHexDigit)) throw new GitPushException("git_push_no_head", "GitPushNoHead");
        var branch = (await QueryAsync(repository, ["symbolic-ref", "--quiet", "HEAD"], context, token, true).ConfigureAwait(false)).TrimEnd('\r', '\n');
        if (branch.Length > 0 && !branch.StartsWith("refs/heads/", StringComparison.Ordinal)) throw Incomplete();
        var source = branch.Length == 0 ? null : branch["refs/heads/".Length..];
        var config = await QueryAsync(repository, ["config", "--null", "--list"], context, token).ConfigureAwait(false);
        var names = (await QueryAsync(repository, ["remote"], context, token).ConfigureAwait(false)).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (names.Length == 0) throw new GitPushException("git_push_no_remote", "GitPushNoRemote");
        if (names.Length > 128 || names.Any(name => !GitPushNames.IsRemote(name))) throw Incomplete();
        var selected = request.Arguments.GetOptionalString("remote") ?? Config(config, $"branch.{source}.pushremote")
            ?? Config(config, "remote.pushdefault") ?? Config(config, $"branch.{source}.remote")
            ?? (names.Contains("origin", StringComparer.Ordinal) ? "origin" : names[0]);
        if (!names.Contains(selected, StringComparer.Ordinal)) throw new GitPushException("git_push_no_remote", "GitPushNoRemote");
        var tracking = selected == Config(config, $"branch.{source}.remote") ? Config(config, $"branch.{source}.merge") : null;
        var target = request.Arguments.GetOptionalString("targetBranch") ?? (tracking?.StartsWith("refs/heads/", StringComparison.Ordinal) == true ? tracking[11..] : source);
        if (target is null || !GitPushNames.IsBranch(target)) throw new GitPushException("git_push_branch", "GitPushBranchRequired");
        if (Config(config, $"remote.{selected}.vcs") is not null) throw new GitPushException("git_push_transport", "GitPushTransportUnsupported");
        // A successful push refreshes refs/remotes/<selected>/<target> locally, so the remote-tracking
        // ref is the offline proof used after non-deterministic push failures; missing refs read empty.
        var remoteHead = (await QueryAsync(repository, ["rev-parse", "--verify", "--quiet", $"refs/remotes/{selected}/{target}"],
            context, token, true).ConfigureAwait(false)).TrimEnd('\r', '\n');
        if (remoteHead.Length > 0 && (remoteHead.Length is not (40 or 64) || !remoteHead.All(char.IsAsciiHexDigit))) throw Incomplete();
        var urls = (await QueryAsync(repository, ["remote", "get-url", "--push", "--all", "--", selected], context, token).ConfigureAwait(false))
            .TrimEnd('\r', '\n').Split('\n').Select(value => value.TrimEnd('\r')).ToArray();
        if (urls.Length != 1) throw new GitPushException("git_push_multiple_urls", "GitPushMultipleUrls");
        var endpoint = await endpoints.ReadAsync(urls[0], repository.Root, target, context, token).ConfigureAwait(false);
        var state = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\0', repository.Root,
            repository.GitDirectory, repository.CommonDirectory, head, branch, config, selected, target, urls[0], endpoint.Identity, remoteHead))));
        return new(repository, new(state, head, source, selected, target, endpoint.Label, names, remoteHead.Length == 0 ? null : remoteHead), endpoint.Protocol);
    }

    private async Task<string> QueryAsync(ToolGitRepository repository, string[] arguments, ToolExecutionContext context, CancellationToken token, bool acceptMissing = false)
    {
        var result = await git.RunAsync(repository.Root, repository.Command(["--no-optional-locks", "-c", "core.fsmonitor=false", .. arguments]),
            context with { MaximumOutputCharacters = QueryLimit }, token, acceptMissing).ConfigureAwait(false);
        if (!result.IsSuccess || result.OutputCharacterCount >= QueryLimit) throw Incomplete();
        return result.Content ?? string.Empty;
    }

    private static string? Config(string config, string name) => config.Split('\0').LastOrDefault(entry => entry.StartsWith(name + "\n", StringComparison.Ordinal))?[(name.Length + 1)..];
    private static GitPushException Incomplete() => new("git_push_preview_incomplete", "GitPushReadFailed");
}
