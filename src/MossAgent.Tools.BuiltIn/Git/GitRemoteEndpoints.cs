using System.Text.RegularExpressions;
using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.Tools.BuiltIn.Git;

public static class GitRemoteEndpoints
{
    private static readonly Regex ScpLike = new(@"^(?:[^@\s/:]+@)?(?<host>\[[0-9A-Fa-f:]+\]|[^/@\s:]+):.+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    // Pure URL-shape classification: never throws and never echoes userinfo; Endpoint is scheme://host[:port].
    public static GitRemoteTransport Classify(string? url)
    {
        if (url is null || url.Length is 0 or > 32768 || url.Any(char.IsControl))
            return new(GitRemoteCredentialMethod.Unsupported, null);
        if (Path.IsPathRooted(url)) return new(GitRemoteCredentialMethod.Local, null);
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile)
                return uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0
                    ? new(GitRemoteCredentialMethod.Unsupported, null) : new(GitRemoteCredentialMethod.Local, null);
            if (uri.Scheme is "http" or "https" or "ssh" or "git" && uri.Host.Length > 0)
                return new(Credential(uri), uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped));
            if (url.Contains("://", StringComparison.Ordinal) || url.Contains("::", StringComparison.Ordinal))
                return new(GitRemoteCredentialMethod.Unsupported, null);
        }
        var scp = ScpLike.Match(url);
        if (scp.Success)
        {
            var host = scp.Groups["host"].Value;
            return Uri.CheckHostName(host.Trim('[', ']')) == UriHostNameType.Unknown
                ? new(GitRemoteCredentialMethod.Unsupported, null) : new(GitRemoteCredentialMethod.Ssh, "ssh://" + host);
        }
        if (url.Contains(':')) return new(GitRemoteCredentialMethod.Unsupported, null);
        return new(GitRemoteCredentialMethod.Local, null);
    }

    // HTTP(S) userinfo is a credential; ssh/git userinfo is a login name unless a ':' hides a password.
    public static GitRemoteCredentialMethod Credential(Uri uri) => uri.Scheme is "http" or "https"
        ? uri.UserInfo.Length > 0 ? GitRemoteCredentialMethod.Embedded : GitRemoteCredentialMethod.Https
        : uri.UserInfo.Contains(':') ? GitRemoteCredentialMethod.Embedded : GitRemoteCredentialMethod.Ssh;
}
