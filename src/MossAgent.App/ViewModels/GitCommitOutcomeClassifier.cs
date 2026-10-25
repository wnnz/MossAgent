namespace MossAgent.App.ViewModels;

internal static class GitCommitOutcomeClassifier
{
    // Deterministic failures are raised before `git commit` actually runs: pipeline
    // validation/approval rejection plus the tool's own pre-commit snapshot checks.
    // Everything else (tool_timeout, tool_cancelled, git_failed, tool_failed, …) may
    // have already created the commit, so the outcome needs a fresh preview to judge.
    public static bool IsDeterministic(string? errorCode) => errorCode is
        "tool_not_found" or "audit_unavailable" or "tool_schema_invalid" or "invalid_arguments"
        or "capability_denied" or "tool_scope_denied" or "workspace_denied" or "path_denied"
        or "approval_denied" or "approval_failed"
        or "git_state_changed" or "git_nothing_staged" or "git_unmerged" or "git_preview_incomplete";
}
