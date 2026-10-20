# Explicit task tool scopes

## Purpose and required self-check

PR review requires a source-only task boundary, not ordinary projectless chat
with unrestricted tools. `PULL_REQUEST_WORKFLOW_PLAN.md` retains the complete PR
requirements; this slice implements only the immutable allowed-tool foundation.

1. Tools.Abstractions owns the context scope/value check. Tools.BuiltIn enforces
   it in the shared executor. Agent filters advertised tools/delegation and passes
   the same restriction into child execution. No infrastructure or UI dependency
   is introduced, and no static global permissions are added.
2. Reuse `ToolExecutionContext`, the existing catalog/registry and ordinary tool
   pipeline. No alternate execution path, direct tool call or fake project.
3. Scope validation follows schema/capability validation and precedes workspace
   inspection, tool prevalidation and approval. Rejection completes the ordinary
   audit with `tool_scope_denied`. Allowed calls still obey policy, approvals,
   path/workspace guards, deadlines, cancellation, output limits and audit. This
   static immutable scope does not replace live account/repository revocation
   checks, which remain to implement for the source workflow.
4. Changes retain one type per C# file and the existing file/method size limits.
   Scope names are frozen with the registry's ordinal-ignore-case semantics;
   context copies share immutable data, never a caller's mutable HashSet.
5. Focused tests cover allowed/denied calls, all major capability families,
   audit/approval ordering, immutable copies, schema/policy/projectless precedence,
   descriptor filtering and parent/child intersection. A synthetic adversarial
   model issues an unadvertised call through the production executor; it receives
   a denial without invoking the underlying tool. This uses no source account,
   native window or paid model.

## Semantics

- `AllowedToolNames=null` preserves existing behavior; an empty set denies all.
  An allowed name is not a filesystem, network-target, source-account or approval
  grant. `ToolsEnabled=false` still denies tools regardless of the list.
- Registry aliases are case-insensitive, but caller-provided collection comparers
  cannot alter membership semantics. Changing the input collection cannot broaden
  or narrow an already-captured operation's immutable scope. Live revocation needs
  the existing task guard/cancellation pattern and future source-authorization port.
- Main model descriptors refresh per turn without exposing excluded tools or
  delegation profiles. A model-supplied unadvertised name is not trusted: the real
  executor still checks it and records a denied result.
- The child gateway captures the parent restriction, removes recursive delegation
  state, intersects any child list and cannot reenable a disabled parent. Ordinary
  tool schema/approval/output handling is not replaced by a gateway-local executor.
- Existing unscoped tasks retain their behavior. No production UI enables review
  tasks yet; plain projectless chat, source accounts and their credentials are
  unchanged. Account-specific authorization, HTTP and the PR UI/action chain remain
  unimplemented and cannot be inferred from passing scope tests.

## Current validation

Candidate: `artifacts/pr-review-scope-20261018`, separate from frozen Git push.
Focused tool/workspace checks pass 18 cases; descriptor/gateway/adversarial-model
checks pass 21 cases after correcting the test to inspect the existing failed
tool-feedback JSON shape instead of assuming it contains an ErrorCode property.
The original one-failure test report remains. Focused counts overlap the following
regression and must not be added to its total.

Session 61763 is terminal with exit 0 and three Completed reports: 624 Tools,
145 Agent and 334 related App cases, totaling 1,103/1,103 without failures/skips.
The App scope includes projectless chat, subagents, approval and Git push. The
complete solution build in session 71551 passes with zero warnings/errors;
`solution-build.log` retains its output. The 326-file C# type/size/syntax audit
and `git -c core.autocrlf=false diff --check` pass. No native/source-account/API
acceptance is implied, and this is not all solution tests.

These changes follow the separate frozen Git push candidate's 3,607-case result;
that earlier total is neither rerun for this slice nor added to 1,103. Future
account/client/UI work requires its own validation. Do not rebuild or overwrite
the earlier candidate. The overall product goal remains active; no development
commit, push, release, user-window operation or real credential/API use occurs.
