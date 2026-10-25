using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.Tools.BuiltIn.Git;

// One side (fetch or push) of a remote as git remote get-url resolves it. Configured records whether
// the matching remote.<name>.url / .pushurl key exists, which is what an expected null endpoint asserts;
// Method is the per-leg credential method with the remote.<name>.vcs marker applied as unsupported.
public sealed record GitRemoteUrlSide(bool Configured, int Count, GitRemoteTransport? Transport,
    GitRemoteCredentialMethod? Method)
{
    public string? Label => GitRemoteEndpoints.Label(Transport);
}
