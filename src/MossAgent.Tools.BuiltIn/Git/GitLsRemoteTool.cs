using System.Text.Json;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Processes;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

// Bounded read-only probe of a configured remote and the first step of real remote verification:
// the remote must exist in git remote (reusing the remote_set_url state read, including the
// remote.<name>.vcs helper marker), its fetch URL classifies on the effective endpoint, and a
// local target is authorized through the same push-destination port. git ls-remote then runs
// non-interactively with every protocol disabled except the classified one and with askpass,
// terminal prompts and interactive credential helpers suppressed; ssh additionally pins
// BatchMode so an unknown host key fails instead of prompting. stdout is captured on a separate
// buffer so ref lines parse on a clean surface; malformed or truncated output fails closed and
// no raw process output is ever echoed because it may carry embedded credentials.
public sealed class GitLsRemoteTool(GitRemoteUrlReader urls, GitRemoteUrlTarget targets, GitCommandRunner git)
    : IAgentTool, IToolRequestPrevalidator
{
    private const int QueryLimit = 1024 * 1024;
    private const int RefCaptureLimit = 256 * 1024;
    private const int MaximumRefs = 64;
    private const int MaximumRefName = 1024;

    private static readonly IReadOnlyDictionary<string, string?> ProbeEnvironment =
        new Dictionary<string, string?> { ["GIT_TERMINAL_PROMPT"] = "0", ["GCM_INTERACTIVE"] = "never" };

    public ToolDescriptor Descriptor { get; } = new("git.ls_remote", ToolExecutionText.Get("GitLsRemoteDescription"),
        """{"type":"object","additionalProperties":false,"required":["remote"],"properties":{"repository":{"type":"string","minLength":1,"maxLength":32768},"remote":{"type":"string","minLength":1,"maxLength":256},"ref":{"type":"string","minLength":1,"maxLength":1024}}}""",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead | ToolCapability.Process | ToolCapability.Network);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        urls.Validate(request, context);
        GitLsRemotePatterns.Normalize(request.Arguments);
    }

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var pattern = GitLsRemotePatterns.Normalize(request.Arguments);
            var state = await urls.ReadAsync(request, context, cancellationToken).ConfigureAwait(false);
            var transport = await EndpointAsync(state, context, cancellationToken).ConfigureAwait(false);
            var scheme = transport.Method is GitRemoteCredentialMethod.Local ? "file" : Scheme(transport.Endpoint);
            if (scheme is null) throw Transport();
            var capture = new BoundedTextBuffer(Math.Max(context.MaximumOutputCharacters, RefCaptureLimit));
            var result = await git.RunAsync(state.Repository.Root, Arguments(state, scheme, pattern), context,
                cancellationToken, standardOutput: capture, environment: ProbeEnvironment).ConfigureAwait(false);
            if (!result.IsSuccess) throw Failed();
            var (refs, truncated) = Parse(capture);
            return ToolResult.Success(ToolExecutionText.Get("GitLsRemoteSucceeded"),
                JsonSerializer.Serialize(new GitLsRemoteReport(state.Name,
                    GitRemoteEndpoints.Label(transport), refs, truncated), JsonSerializerOptions.Web));
        }
        catch (GitRemotesException error) { return ToolResult.Failure(error.Message, error.Code); }
        catch (TimeoutException) { return ToolResult.Failure(ToolExecutionText.Get("ExecutionTimedOut"), "tool_timeout"); }
    }

    // The vcs helper marker or an unclassifiable/absent fetch URL is unsupported; a local target
    // re-reads the effective URL and authorizes the resolved path before any process runs.
    private async Task<GitRemoteTransport> EndpointAsync(GitRemoteUrlState state, ToolExecutionContext context, CancellationToken token)
    {
        var transport = state.Vcs ? null : state.Fetch.Transport;
        if (transport is null || transport.Method is GitRemoteCredentialMethod.Unsupported) throw Transport();
        if (transport.Method is GitRemoteCredentialMethod.Local)
        {
            var url = await FetchUrlAsync(state.Repository, state.Name, context, token).ConfigureAwait(false);
            if (GitRemoteEndpoints.Classify(url).Method is not GitRemoteCredentialMethod.Local) throw Transport();
            await targets.AuthorizeAsync(url, state.Repository.Root, state.SourceBranch ?? "HEAD", context, token).ConfigureAwait(false);
        }

        return transport;
    }

    private async Task<string> FetchUrlAsync(ToolGitRepository repository, string name,
        ToolExecutionContext context, CancellationToken token)
    {
        var result = await git.RunAsync(repository.Root, repository.Command(
            ["--no-optional-locks", "-c", "core.fsmonitor=false", "remote", "get-url", "--", name]),
            context with { MaximumOutputCharacters = QueryLimit }, token).ConfigureAwait(false);
        var url = result.IsSuccess ? (result.Content ?? string.Empty).TrimEnd('\r', '\n') : null;
        if (url is null || url.Length == 0 || url.Contains('\n')
            || result.IsSuccess && result.OutputCharacterCount >= QueryLimit) throw Transport();
        return url;
    }

    private static IReadOnlyList<string> Arguments(GitRemoteUrlState state, string scheme, string? pattern)
    {
        var arguments = new List<string>
        {
            "--no-optional-locks", "-c", "core.fsmonitor=false",
            "-c", "protocol.allow=never", "-c", $"protocol.{scheme}.allow=always", "-c", "core.askPass="
        };
        if (scheme == "ssh") arguments.AddRange(["-c", "core.sshCommand=ssh -o BatchMode=yes -o ConnectTimeout=10"]);
        arguments.AddRange(["ls-remote", "--heads", "--tags", "--", state.Name]);
        if (pattern is not null) arguments.Add(pattern);
        return state.Repository.Command([.. arguments]);
    }

    // Each emitted line must be <40-or-64-hex>\t<refs/...>; anything else, a missing trailing
    // newline, or a capture cut short before 64 refs means the stream cannot be trusted and the
    // probe fails closed. Lines past the 64th are still drained but dropped.
    private static (IReadOnlyList<GitLsRemoteRef> Refs, bool Truncated) Parse(BoundedTextBuffer capture)
    {
        var text = capture.ToString();
        if (!capture.IsTruncated && text.Length > 0 && !text.EndsWith('\n')) throw Incomplete();
        var refs = new List<GitLsRemoteRef>(MaximumRefs);
        var lines = text.Split('\n');
        var truncated = capture.IsTruncated;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            if (line.Length == 0 && index == lines.Length - 1) break;
            if (refs.Count == MaximumRefs) { truncated = true; break; }
            var tab = line.IndexOf('\t');
            var name = tab < 0 ? string.Empty : line[(tab + 1)..];
            if (tab is not (40 or 64) || !line[..tab].All(char.IsAsciiHexDigit)
                || name.Length is 0 or > MaximumRefName
                || !name.StartsWith("refs/", StringComparison.Ordinal)
                || name.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
                throw Incomplete();
            refs.Add(new(name, line[..tab]));
        }

        return (refs, truncated);
    }

    private static string? Scheme(string? endpoint) => endpoint is null ? null
        : endpoint.IndexOf("://", StringComparison.Ordinal) is var separator && separator > 0
            ? endpoint[..separator] : null;

    private static GitRemotesException Failed() => new("git_remote_probe_failed", "GitRemoteProbeFailed");
    private static GitRemotesException Transport() => new("git_remote_probe_transport", "GitRemoteProbeTransport");
    private static GitRemotesException Incomplete() => new("git_remote_probe_incomplete", "GitRemoteProbeIncomplete");
}
