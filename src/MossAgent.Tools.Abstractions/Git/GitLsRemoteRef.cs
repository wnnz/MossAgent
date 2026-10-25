namespace MossAgent.Tools.Abstractions.Git;

// One advertised remote ref as returned by git.ls_remote; Ref is always under refs/ and Oid is
// the 40- or 64-hex object id the remote claims it points at.
public sealed record GitLsRemoteRef(string Ref, string Oid);
