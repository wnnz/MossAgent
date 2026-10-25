using System.Text.Json.Serialization;

namespace MossAgent.Tools.Abstractions.Git;

[JsonConverter(typeof(JsonStringEnumConverter<GitRemoteCredentialMethod>))]
public enum GitRemoteCredentialMethod
{
    [JsonStringEnumMemberName("unsupported")] Unsupported,
    [JsonStringEnumMemberName("local")] Local,
    [JsonStringEnumMemberName("ssh")] Ssh,
    [JsonStringEnumMemberName("https")] Https,
    [JsonStringEnumMemberName("embedded")] Embedded
}
