using System.Text.RegularExpressions;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitPushEndpoints(IGitPushDestinationAuthorization destinations)
{
    public async Task<GitPushEndpoint> ReadAsync(string url, string repository, string targetBranch,
        ToolExecutionContext context, CancellationToken token)
    {
        if (url.Length is 0 or > 32768 || url.Any(char.IsControl)) throw Unsupported();
        if (Path.IsPathRooted(url)) return await LocalAsync(url, targetBranch, context, token).ConfigureAwait(false);
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile)
            {
                if (uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0) throw Unsupported();
                return await LocalAsync(uri.LocalPath, targetBranch, context, token).ConfigureAwait(false);
            }
            if (uri.Scheme is "http" or "https" or "ssh" or "git" && uri.Host.Length > 0)
                return new(uri.Scheme, uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped), url,
                    GitRemoteEndpoints.Credential(uri));
            if (url.Contains("://", StringComparison.Ordinal) || url.Contains("::", StringComparison.Ordinal)) throw Unsupported();
        }
        var scp = Regex.Match(url, @"^(?:[^@\s/:]+@)?(?<host>\[[0-9A-Fa-f:]+\]|[^/@\s:]+):.+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (scp.Success)
        {
            var host = scp.Groups["host"].Value;
            if (Uri.CheckHostName(host.Trim('[', ']')) == UriHostNameType.Unknown) throw Unsupported();
            return new("ssh", "ssh://" + host, url, GitRemoteCredentialMethod.Ssh);
        }
        if (url.Contains(':')) throw Unsupported();
        return await LocalAsync(Path.GetFullPath(url, repository), targetBranch, context, token).ConfigureAwait(false);
    }

    private async Task<GitPushEndpoint> LocalAsync(string path, string branch, ToolExecutionContext context, CancellationToken token)
    {
        var target = await destinations.AuthorizeAsync(path, branch, context, token).ConfigureAwait(false);
        return new("file", Resources.ToolExecutionText.Get("GitPushLocalDestination"),
            string.Join('\0', target.Root, target.GitDirectory, target.CommonDirectory), GitRemoteCredentialMethod.Local);
    }

    private static GitPushException Unsupported() => new("git_push_transport", "GitPushTransportUnsupported");
}
