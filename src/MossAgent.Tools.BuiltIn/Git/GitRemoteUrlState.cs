using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.Tools.BuiltIn.Git;

// Verified remote state used for CAS compare and for the sanitized post-write report; never holds
// or echoes raw URLs. SourceBranch is the current HEAD branch used when authorizing local targets.
public sealed record GitRemoteUrlState(ToolGitRepository Repository, string Name, GitRemoteUrlSide Fetch,
    GitRemoteUrlSide Push, bool Vcs, string? SourceBranch)
{
    public GitRemoteEntry Entry() => new(Name, Fetch.Label, Push.Label,
        Vcs ? GitRemoteCredentialMethod.Unsupported : GitRemoteEndpoints.Merge(Fetch.Transport, Push.Transport),
        Fetch.Transport?.Endpoint != Push.Transport?.Endpoint, Push.Count > 1 ? Push.Count : null);
}
