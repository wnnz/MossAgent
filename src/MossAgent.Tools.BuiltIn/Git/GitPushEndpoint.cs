using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed record GitPushEndpoint(string Protocol, string Label, string Identity, GitRemoteCredentialMethod CredentialMethod);
