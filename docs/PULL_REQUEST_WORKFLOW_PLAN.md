# Pull request workflow: scope and implementation plan

## Reference and current state

Official reference inspected on 2026-10-18:
[Code review](https://learn.chatgpt.com/docs/code-review). It describes source
account connection, personal review inboxes, opening a PR URL, summary/activity,
changed files with inline comments, checks, pinned reviews, revision-specific
viewed markers, related stacks, review chat and explicit feedback submission.
Commenting in a PR and submitting a review are remote writes; chatting about a
PR does not post a comment, approve or merge it. Failing-check context prepares a
draft instead of silently sending a message. Local Git review is a separate flow.

Current `src`/`tests` inventory includes independently persisted GitHub token
connections and bounded identity reauthentication through shared settings. See
`SOURCE_ACCOUNT_CONNECTION_ACCEPTANCE.md`; this is not repository authorization
or a complete PR workflow. Schema 19 now adds explicit repository read grants,
source-only task scope, standard PR read tools and a configured-repository page
with open-PR pagination, description and commit IDs. See
`SOURCE_PR_READING_ACCEPTANCE.md` for that frozen slice. The subsequent
`SOURCE_PR_INSPECTION_ACCEPTANCE.md` adds version-checked file patches, discussion
and inline-comment pages, check runs and legacy statuses. The current
`SOURCE_PR_INLINE_COMMENTS_ACCEPTANCE.md` adds verified current-head inline cards,
exact-side line/range validation and keyboard location on the two loaded pages;
historical or unsupported locations remain explicit rather than being guessed.
Schema 20 adds a separate ordinary projectless chat per grant/PR/base/head over
the first bounded, explicitly loaded snapshot, plus draft-only failed-check
investigation through the existing composer cache. Opening does not call a model;
manual sends reuse the production model/history/cancellation chain with all tools
disabled. Agent reserves the full untrusted user snapshot separately from
evictable history. Capacity refusal is persisted by schema 21 and displayed in
the shared task-failure card with repair steps; it never silently drops the
snapshot or retries. Revocation blocks new turns and later observed output, not local history.
See `SOURCE_PR_REVIEW_CHAT_ACCEPTANCE.md` for the preceding snapshot validation.
Schema 22 adds a separate explicitly opened ReadOnlySource conversation per
grant/PR/base/head/mode. Only get/files/comments/checks may query its bound PR and
commits through the existing pipeline; lists, local roots, browser, MCP, subagents
and writes stay unavailable. Source and tool results are untrusted observations,
not instructions, and opening never sends or fetches more pages. Mode/draft/history
isolation, post-HTTP access/revision checks and controlled model-tool-source-model
evidence are recorded in `SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md`. The other
workflows below and native/live acceptance remain open.
The subsequent explicit URL form resolves a chosen account and existing grant,
reads only normalized PR detail through the common pipeline, and reuses inspection
and review chats without a silent list query, model send or authorization. Common
web page suffixes identify the current PR, not a historical snapshot. Candidate
validation, strict origin/port mapping and limits are in `SOURCE_PR_URL_ACCEPTANCE.md`.
`TOOL_SCOPE_ACCEPTANCE.md` adds
immutable tool allowlists to the executor and main/child Agent chains, not PR
account access. Existing Git push is not PR support. This document preserves
the complete implementation/acceptance plan, not a claim of functional parity. GitLab,
cloud reviews, OAuth/connector compatibility and plugin-ecosystem compatibility
remain explicit gaps until their own running chains exist; they are not silently
removed from the product objective.

## Required design self-check

1. Domain owns source-account identities and PR/revision values; Application owns
   persistence/client ports and workflow coordination. Infrastructure implements
   SQLite and GitHub HTTP. Tools.BuiltIn implements separate shared-contract read
   and mutation tools; App owns view models/shared controls and calls application
   operations, never HTTP, SQL, filesystem or a GitHub CLI directly. Agent consumes
   ports and bounded review context without referring to concrete clients.
2. Reuse the standard schema/capability/approval/deadline/cancellation/output/
   artifact/audit pipeline and proxy configuration. The AI-provider HTTP factory
   takes an `AiProvider`, so do not disguise a source account as an AI provider to
   reuse it. Add a source-client factory/port sharing proxy semantics instead.
3. Source-account access is an explicit grant to a server/repository, not a grant
   to a filesystem directory or an arbitrary URL. Add and enforce a task-owned
   allowed-tool/source scope before review-chat tools are enabled. Plain projectless
   chats keep `ToolsEnabled=false`; do not invent a project, reuse another task's
   context or enable all tools merely to implement a global PR page. Read operations
   need the configured account/scope; writes require ordinary policy approval and
   current identity/revision revalidation. Each operation owns cancellation and a
   bounded deadline, and waits for actual HTTP/body cleanup.
4. Keep one handwritten type per C# file, files below 500 lines and methods below
   80 lines. Split identity validation, client transport, pagination, parsing,
   per-action tools, connection editing, inbox, details, diff/comments and review
   submission. Reuse shared controls/styles and existing preview/diff components.
5. Deterministic HTTP/SQLite/pipeline/Headless integration must prove settings to
   actual requests and visible results. Include denial, revoked scope/account,
   stale revision, cancellation, concurrency, oversized/malformed responses,
   redirects/credential suppression and restart/no-replay negatives. Separate
   simulated transport, loopback HTTP, real-source-account and native-window proof.

## Implementation phases

### 1. Connections, source authorization and runtime boundary

- Persist separately named source accounts with provider/server identity, an
  explicit credential reference/value and proxy selection. Use a forward migration
  after current schema version 17; preserve all existing user data. Maintain the
  project's explicit plaintext-SQLite secret-storage decision without exposing
  credentials in records' `ToString`, logs, diagnostics, lists, exports or tool
  arguments/results. Connection failure cannot silently switch account or proxy.
- Offer an explicit GitHub token connection and validate its identity/permissions
  with a real bounded request; a status refresh must actually reauthenticate.
  Token support is not OAuth/ChatGPT-connector parity. Do not read the user's Git
  credential manager or existing CLI/browser account implicitly. Implement OAuth
  separately where application registration/provider requirements are available.
- Bind credentials to the configured HTTPS API authority and path prefix. Disable
  automatic redirects/cookies and never follow an API-provided pagination/content
  URL using an authenticated client. Resolve only validated relative endpoints;
  generate bounded pagination locally. GitHub.com has known API/web authorities;
  enterprise servers require explicit configuration and validation. Never disable
  production TLS checks. Tests inject a controlled handler or local test transport.
- Retain direct/default/explicit proxy choices and unavailable-proxy failures;
  prevent deletion of a proxy still referenced by a source account. Never silently
  downgrade to direct access when a selected proxy disappears.
- Define an account/repository/revision review scope distinct from plain chat.
  Every tool call, including an unsolicited model call, must enforce its exact
  allowlist. A review-only task must reject file reads/writes, shell, screenshots,
  unrelated MCP/browser actions and another source account, even under FullAccess.
  Account deletion, permission changes and task switching invalidate pending work.

### 2. Inbox, PR details, diff, feedback, checks and chat

- Fetch authenticated personal inboxes (requested from me/team, authored by me),
  configured-repository lists and a validated explicitly entered PR URL. Preserve
  account/repository/filter/page selection; expose pagination, empty, loading,
  rate-limit, authentication, permission and retry states instead of fake rows.
- Show description, author/state/draft, base/head revisions, activity, issue and
  inline review comments, reviews and checks. Parse fixed bounded data shapes;
  treat all source text as untrusted data. Remote Markdown links/images/scripts
  cannot authorize a request, tool, filesystem access or navigation automatically.
- Display changed files/patches, additions/deletions and comments bound to valid
  diff lines and the displayed revision. Report missing/truncated/binary/oversized
  patches explicitly; do not present an incomplete patch as a complete review.
  Persist pin/viewed state scoped to account/repository/PR/head commit; changing
  revision clears or marks old viewed state. Resolve related stacks only from
  bounded source evidence, not inferred dependency claims.
- Create an associated review chat with the selected configured model, explicit
  instructions and bounded PR context. No automatic checkout, clone, fetch, commit
  or push occurs when opening a PR. Attaching local coding work requires a real
  project and the existing authorization/worktree flows. Review context never
  grants local roots or shares unrelated task data.
- Preparing a failing-check investigation puts a clearly identified summary in
  the selected chat's draft without sending or overwriting an existing draft.
  Do not follow third-party check/log URLs or include raw CI logs by default;
  those may contain credentials and require their own explicit access boundary.
  Manual chat review does not impersonate Codex cloud automatic reviews.

### 3. Creation, comments, review decisions and follow-up

- A create-PR dialog previews connection/repository, base/head branch, title/body
  and draft status before confirmation. Do not stage, commit, push or configure
  tracking automatically. Confirm branches exist on the source service; report
  missing publication with a separate explicit Git push workflow. Detect existing
  PRs and show the server's result rather than blindly retrying a write.
- Issue comments, inline comments and a submitted review are separate operations.
  Review submission explicitly chooses Comment, Approve or Request changes and
  previews the decision/body/line comments and head revision. Draft findings and
  chat messages never post automatically. A merged/closed/unavailable PR or a
  changed head requires refreshed inspection and confirmation.
- Bind review comments to the displayed commit and valid file/line/side context.
  Revalidate after approval, and pass a commit ID where the provider API supports
  it. Do not claim a check-then-write operation eliminates a server-side race;
  creation by a moving head ref and endpoints without conditional writes retain
  explicit limits. Verify returned identities/revisions and expose conflicts.
- Cancellation/timeout/network failure may follow a successful remote write.
  Clear confirmation state, preserve drafts and offer inspection before an
  explicit retry; do not automatically resubmit. Restart restores interrupted
  state/history, never replays incomplete comments, reviews or PR creation.
- Refresh actual inbox/details/checks and display the returned remote result.
  Keep local conversation input, another account/task's selection and concurrent
  runs intact. Merge/delete/reopen are separately authorized source operations,
  not accidental side effects of approval or navigation.

## Acceptance ledger to implement

| Requirement | Required evidence | Current status |
| --- | --- | --- |
| Connection/proxy persistence and reauthentication | Version-17 forward-migration fixture, restart, actual identity requests and proxy failures | Token connection settings, schema 18, revision checks and bounded identity requests implemented; SQLite reopen/controlled HTTP/Headless validation in SOURCE_ACCOUNT_CONNECTION_ACCEPTANCE.md; schema 19 read grants are separate from server permission verification; native/live account and OAuth remain open |
| Source-only scope enforcement | Direct/unsolicited tool negatives for filesystem/process/screen/MCP/other accounts; denial and revocation before/after approval | Exact account/repository/revision read grants and pre/post-approval/after-response validation implemented. Snapshot chats still disable all tools. A separate opt-in ReadOnlySource chat exposes only get/files/comments/checks for its exact PR/base/head, never local roots, lists, browser, MCP or subagents. Access changes reject later output/provider requests; source writes remain open. Current acceptance: SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md |
| Inbox/URL/details/changes/comments/checks | Production composition through bounded HTTP responses to shared controls, pagination and error paths | Configured-repository lists/details, version-checked patches, comments, checks/statuses and validated current inline diff cards/location implemented; same-side ranges target the end line, only loaded pages participate. Explicit account-bound PR URL opening now reuses the detail/inspection/chat chain; validation and mapping limits: SOURCE_PR_URL_ACCEPTANCE.md. Inbox/filter and reviews/activity remain open |
| Review chat and draft-only check context | Selected model sees only pinned PR data; no source write/local grant; existing draft survives | Immutable first-loaded snapshot chat and empty-draft-only investigation remain unchanged. Schema 22 adds a separate read-only query chat with model-tool-source-model roundtrips, visible/persisted results, pinned context and manual cancellation recovery. Opening never sends/queries; same-mode reopening preserves its first snapshot. Evidence and native/live limits: SOURCE_PR_ON_DEMAND_READING_ACCEPTANCE.md; full PR parity remains open |
| Pin/viewed/related-stack revision behavior | Persistent reopening, changed-head and incomplete-response tests | Not implemented |
| Confirmed PR creation and feedback | Pipeline approval to exact captured HTTP mutation, response/audit/UI refresh; no implicit Git action or retry | Not implemented |
| Concurrency/cancel/restart and safety | Delayed transport/cleanup, stale result rejection, secret-free diagnostics and no replay | Token settings/identity slice has focused coverage; complete PR operation lifecycle not implemented |
| Keyboard/themes/native and live source | Focus/bounds/shared-control checks plus separate explicitly authorized native/account runs | Shared connection settings have Headless keyboard/theme coverage; native/live source not verified |

The existing Git push regression is frozen and terminal in `artifacts/git-push-20261018`.
New PR source changes require a separate
candidate; do not relabel earlier Git passes as PR or current full-product proof.
