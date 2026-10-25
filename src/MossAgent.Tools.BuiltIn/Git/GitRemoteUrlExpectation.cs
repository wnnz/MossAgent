using System.Text.Json;
using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.Tools.BuiltIn.Git;

// Semantic CAS expectation for one remote leg: Endpoint is the sanitized label git.remotes emitted
// (scheme://host[:port] or the shared local marker); null asserts the leg has no configured URL.
// Method is compared only when a URL is configured, since an absent leg has no transport to classify.
public sealed record GitRemoteUrlExpectation(string? Endpoint, GitRemoteCredentialMethod Method)
{
    public bool Matches(GitRemoteUrlSide side) =>
        Endpoint is null ? !side.Configured : side.Label == Endpoint && side.Method == Method;

    public static GitRemoteUrlExpectation? Parse(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var element) || element.ValueKind is not JsonValueKind.Object)
            return null;
        var method = element.GetProperty("method").GetString() switch
        {
            "local" => GitRemoteCredentialMethod.Local,
            "ssh" => GitRemoteCredentialMethod.Ssh,
            "https" => GitRemoteCredentialMethod.Https,
            "embedded" => GitRemoteCredentialMethod.Embedded,
            "unsupported" => GitRemoteCredentialMethod.Unsupported,
            _ => throw new ArgumentException("远端期望的凭据方式无效。")
        };
        var endpoint = element.TryGetProperty("endpoint", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
        return new(endpoint, method);
    }
}
