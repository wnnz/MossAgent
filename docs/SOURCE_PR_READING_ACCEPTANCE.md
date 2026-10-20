# Source repository authorization and PR reading

This document records the frozen initial list/detail candidate. The current
files/comments/checks extension and its separate validation are described in
`SOURCE_PR_INSPECTION_ACCEPTANCE.md`; the historical counters below do not
validate that extension.

## Implemented scope (2026-10-18)

The navigation rail now opens **Pull Request**, independently of project/chat
selection. Connect an enabled GitHub source account in settings, refresh accounts
and grants, choose **Authorize repository**, enter owner/name, then explicitly
fetch open PRs. The page provides local pagination, selected-PR description and
base/head commit IDs, cancellation and repository revocation. It uses shared
dialogs, controls, responsive columns and localized feedback in both themes.
Editing a source account invalidates its existing grants; revoke/re-authorize
instead of silently applying an old grant to new credentials. Granting is local
consent, not proof that GitHub permits the operation; service authorization errors
remain visible.

This is a first read workflow, not full PR parity: personal inboxes, PR URLs,
closed filters, diff/files, comments/reviews/checks/activity, pinned/viewed revision
state, review chat, draft check investigation, confirmed remote writes, OAuth,
GitLab, cloud review and native/live-account acceptance remain open. PR reads do
not checkout/clone/fetch, run shell, post feedback or invoke a model.

## Design self-check

1. Domain owns immutable source scope and bounded result shapes. Application owns
   grant/session/client ports and read coordination. Infrastructure implements
   SQLite/HTTP; Tools.BuiltIn depends only on the reader abstraction, and App
   calls repositories/standard executor rather than SQL, HTTP or tools directly.
2. Reuse source HTTP/proxy configuration, shared schema/capability/approval/
   timeout/output/audit execution, and shared UI components. New source tools are
   deliberately hidden from model descriptors until a distinct review-chat
   runtime is implemented; plain projectless chats remain tools-disabled.
3. Grants bind account ID, credential revision and exact canonical owner/repo.
   A source-scoped context rejects filesystem/process/screen/browser/MCP tools
   even under FullAccess or an absent allowlist. Async preflight validates before
   approval and again before execution. Reads revalidate after HTTP; revoked or
   changed scope cannot publish a completed response observed after revocation.
   This does not claim atomic exclusion of a concurrent revocation and an already
   dispatched network read. Every HTTP operation owns a 30-second deadline,
   cancellation and awaited response/client cleanup, 2-MiB decompressed body cap,
   bounded JSON depth and field/record counts. The page uses an outer 40-second
   operation deadline. No automatic retries, cookies, redirects, pagination URL
   following or TLS bypass are introduced.
4. Schema 19 migrates forward and retains user accounts/tasks. Read-operation
   sessions create a separate audit-owned task identity with no local roots and
   no ordinary conversation entry. Reopening does not dispatch requests. Explicit
   grant revocations retain ID tombstones so that old IDs cannot be recreated.
   New classes keep one type per file and files under the repository hard limit.
   SQLite entry points and source-operation setup run off the UI dispatcher;
   only final observable updates return to the UI. Request-capture tests assert
   that HTTP dispatch does not execute on the UI thread.
5. Deterministic tests exercise the production client, parser, repositories,
   reader, source tools and App composition. Negatives cover cross-repository
   response substitution, wrong PR number, credential reflection, malformed/large
   bodies, auth/permission/rate-limit/redirect errors, stale/revoked grants,
   in-flight account changes, cancellation, denied unrelated tools, pre/post-
   approval revalidation, output limits and schema injection. Headless tests use
   keyboard activation of real shared buttons and theme/viewport renders.

## Evidence boundaries

Candidate: `artifacts/pr-reading-20261018`. Original failed/initial reports are
retained separately from final reports. The first HTTP test expected a public
endpoint while its fixture intentionally configured an enterprise API prefix;
the expectation was corrected to the configured authority/path. The initial UI
test used a raised Click event instead of actual Button keyboard activation;
keyboard events now exercise command dispatch. Neither is relabeled as a
production-network or native-window defect.

The broader regression also exposed an unintended extra timestamp check on old
non-source tools, causing late-completion restore tests to stop before their
committed inverse snapshots. The extra deadline check now applies only to tools
using the new async prevalidator; old execution timing is unchanged. The new rail
entry now follows automations so that adding PR does not move that established
navigation target. The old tests were retained unchanged; both repairs have a
focused rerun in `tools-repair.trx` and `app-repair.trx`. Final reports below are
separate from these earlier failed/repair candidates.

The tests use synthetic credentials, controlled HTTP handlers, temporary SQLite
and Headless UI. They do not establish real GitHub token permissions, production
TLS, real-window/high-DPI operation or EXE restart. Previously frozen source
account/Git candidates are not new PR evidence or additive totals. Final counters
and current validation reports are recorded alongside this candidate.

## Terminal validation

- Complete solution Debug build: zero warnings/errors (`build-terminal-second.log`).
- Tools 635, Infrastructure 519, Agent 146, related App 146 and visual-component
  300 tests: **1,746 passed, zero failures/skips**. These are the five
  `*-terminal.trx` reports, not totals added to earlier or overlapping runs.
- Agent architecture tests check handwritten single-type/file limits; a focused
  Roslyn method-size audit also checks 50 methods in the new coordinator and
  affected execution/persistence/MainWindow owners. Whitespace validation passes.
- Four compact/ordinary light/dark grant-dialog Headless frames are retained in
  `ui-terminal`; compact light and ordinary dark frames were visually inspected
  during this slice. These are not native-window screenshots or full PR-page
  visual acceptance.
- `validation-terminal.json` records numeric counters and source/runtime identity
  checks. Source hashes cover selected C#/AXAML/project/resource files at both
  ends of the terminal run. Runtime hashes are observed during the run and
  rechecked afterward; they are not a pre-run runtime snapshot or a restorable
  source/runtime archive.

The overall functional-parity goal remains active. No commit, push or release
was requested or performed in this slice.
