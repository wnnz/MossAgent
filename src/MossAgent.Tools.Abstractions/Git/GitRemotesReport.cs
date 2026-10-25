using System.Text.Json.Serialization;

namespace MossAgent.Tools.Abstractions.Git;

public sealed record GitRemotesReport(IReadOnlyList<GitRemoteEntry> Remotes, GitRemotesUpstream? Upstream,
    IReadOnlyList<GitCredentialHelperEntry> CredentialHelpers,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool CredentialHelpersTruncated = false);
