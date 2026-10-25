namespace MossAgent.Tools.Abstractions.Git;

// Bounded ls-remote probe report: Endpoint is the sanitized scheme://host[:port] (or the
// localized local marker), Refs is capped at 64 entries, and Truncated marks that the remote
// advertised more than were returned.
public sealed record GitLsRemoteReport(string Remote, string? Endpoint,
    IReadOnlyList<GitLsRemoteRef> Refs, bool Truncated);
