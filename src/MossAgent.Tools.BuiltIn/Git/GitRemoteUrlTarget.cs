using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.Tools.BuiltIn.Git;

// Validates a new remote URL shape before it is written: unsupported transports (custom helpers,
// unparseable or control-bearing values) are refused outright; local shapes (rooted path, file URI,
// relative path) are authorized through the same push-destination port as git.push, so a remote cannot
// be pointed at an unauthorized local repository; embedded userinfo is allowed since the approval UI
// shows the raw URL. The URL itself is never echoed into errors.
public sealed class GitRemoteUrlTarget(IGitPushDestinationAuthorization destinations)
{
    public async Task AuthorizeAsync(string url, string repositoryRoot, string branch,
        ToolExecutionContext context, CancellationToken token)
    {
        var method = GitRemoteEndpoints.Classify(url).Method;
        if (method is not GitRemoteCredentialMethod.Local)
        {
            if (method is GitRemoteCredentialMethod.Unsupported) throw Transport();
            return;
        }
        var path = Path.IsPathRooted(url) ? url
            : Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile ? uri.LocalPath
            : Path.GetFullPath(url, repositoryRoot);
        await destinations.AuthorizeAsync(path, branch, context, token).ConfigureAwait(false);
    }

    private static GitRemotesException Transport() => new("git_remote_transport", "GitRemoteTransportUnsupported");
}
