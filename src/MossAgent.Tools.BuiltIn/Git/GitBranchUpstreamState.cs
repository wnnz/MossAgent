using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.Tools.BuiltIn.Git;

// Verified upstream state for one local branch: Branch is the resolved refs/heads name, Remote and
// Merge are the current branch.<branch>.remote / .merge values (null when unset, asserted by CAS).
public sealed record GitBranchUpstreamState(ToolGitRepository Repository, string Branch, string? Remote, string? Merge);
