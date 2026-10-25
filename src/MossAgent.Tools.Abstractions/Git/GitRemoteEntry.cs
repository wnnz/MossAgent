using System.Text.Json.Serialization;

namespace MossAgent.Tools.Abstractions.Git;

public sealed record GitRemoteEntry(string Name, string? FetchEndpoint, string? PushEndpoint,
    GitRemoteCredentialMethod CredentialMethod, bool TransportsDiffer,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? PushUrlCount);
