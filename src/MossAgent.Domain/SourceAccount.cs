using System.Diagnostics;
using System.Text.Json.Serialization;

namespace MossAgent.Domain;

public sealed class SourceAccount
{
    public SourceAccount(Guid id, string name, Uri apiEndpoint, string accessToken,
        Guid? proxyId, bool isEnabled, long revision = 0)
    {
        if (id == Guid.Empty || revision < 0 || string.IsNullOrWhiteSpace(name)
            || name.Length > 80 || name.Any(char.IsControl))
            throw new ArgumentException("Invalid source account.");
        if (string.IsNullOrEmpty(accessToken) || accessToken.Length > 4096
            || accessToken.Any(character => character is < '!' or > '~'))
            throw new ArgumentException("Invalid source credential.");
        Id = id;
        Name = name.Trim();
        ApiEndpoint = GitHubApiEndpoint.Parse(apiEndpoint.OriginalString);
        AccessToken = accessToken;
        ProxyId = proxyId;
        IsEnabled = isEnabled;
        Revision = revision;
    }

    public Guid Id { get; }
    public string Name { get; }
    public Uri ApiEndpoint { get; }
    [JsonIgnore, DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public string AccessToken { get; }
    public Guid? ProxyId { get; }
    public bool IsEnabled { get; }
    public long Revision { get; }

    public SourceAccount WithRevision(long revision) =>
        new(Id, Name, ApiEndpoint, AccessToken, ProxyId, IsEnabled, revision);

    public override string ToString() => $"SourceAccount {{ Id = {Id} }}";
}
