namespace MossAgent.App.ViewModels;

internal static class GitPushOutcomeClassifier
{
    // Deterministic failures are raised before `git push` actually runs: pipeline
    // validation/approval rejection plus the tool's own pre-push snapshot checks.
    // Everything else (tool_timeout, tool_cancelled, git_push_failed, …) may have
    // already updated the remote, so the outcome needs a fresh preview to judge.
    public static bool IsDeterministic(string? errorCode) => errorCode is
        "tool_not_found" or "audit_unavailable" or "tool_schema_invalid" or "invalid_arguments"
        or "capability_denied" or "tool_scope_denied" or "workspace_denied" or "path_denied"
        or "approval_denied" or "approval_failed"
        or "git_state_changed" or "git_push_no_remote" or "git_push_no_head" or "git_push_branch"
        or "git_push_transport" or "git_push_multiple_urls" or "git_push_preview_incomplete";
}
