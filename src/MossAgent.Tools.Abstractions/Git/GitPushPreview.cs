namespace MossAgent.Tools.Abstractions.Git;

public sealed record GitPushPreview(string State, string Head, string? Branch, string Remote,
    string TargetBranch, string Destination, IReadOnlyList<string> Remotes, string? RemoteHead,
    GitRemoteCredentialMethod CredentialMethod = GitRemoteCredentialMethod.Unsupported);
