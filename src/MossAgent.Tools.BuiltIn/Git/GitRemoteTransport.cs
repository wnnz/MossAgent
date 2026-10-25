using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed record GitRemoteTransport(GitRemoteCredentialMethod Method, string? Endpoint);
