# PR snapshot review chat and investigation drafts

## Design and safety self-check

1. Domain owns an immutable revision/scope/context binding. Application owns the
   source-chat repository port, bounded snapshot builder, opening use case and
   access validation. Infrastructure owns forward schema 20 and transactional
   SQLite persistence. App only calls ports/use cases and reuses ordinary
   conversation, model selection, attachment, streaming, draft and cancellation
   components. The dependency direction is unchanged.
2. Reuse `IWorkspaceRepository`, `IConversationDraftRepository`, `IAgentRunner`
   and current source repository-access checks. Opening creates or restores a
   distinct ordinary projectless task per grant/PR/base/head, not the shared
   repository audit task. Source data is captured only from already explicitly
   loaded bounded inspection pages. Creation never calls a provider or source
   server and never checks out/clones/fetches Git data.
3. The source grant/account must still match at creation, before an existing
   conversation consumes a prompt, before model execution and before presenting
   each model event. Source chats always have empty roots/working directory,
   ReadOnly policy and disabled tools/subagents/browser sessions. The fixed
   snapshot is user-role untrusted data, not system instructions. A 24,576
   UTF-16-code-unit context budget and explicit patch/text clipping bound storage;
   ordinary model-aware budgeting and streaming cancellation still apply.
   Opening has a 30-second deadline. Investigation uses only loaded check names,
   status and conclusions; it follows no log URLs and never sends automatically.
4. Each handwritten C# type is separate; no existing 500-line Workspace/Main
   view model is expanded. Source writes stay in Infrastructure. Draft seeding
   uses the existing compare-and-swap revision contract and refuses any existing
   text or attachment. This is not a distributed transaction with UI draft state;
   concurrent local edits must retain their existing conflict protection.
5. Focused tests must prove SQLite migration/rollback/reopen/revision uniqueness,
   credential/revocation and environment refusal, context bounds/untrusted role,
   selected model/reasoning and actual provider transport, manual sending,
   cancellation/history/restart and no tool or unrelated-draft access. Headless
   keyboard/navigation tests and current build/regression results are recorded
   separately from real-source/native/Release acceptance.

The long-history correction is owned by Agent's existing context builder, with
an optional fixed user-context field on Application's run request. App supplies
the immutable PR binding through that field rather than the oldest chat turn.
The builder reserves its complete user-role message before selecting history,
including metadata and a minimum 256-character latest-turn budget. It never
promotes source text to system instructions or silently clips/omits the snapshot.
Capacity refusal happens before provider transport, uses a dedicated Application
exception and a localized actionable failure-card hint, and requires manual retry.
Schema 21 extends the existing stable failure-category allowlist transactionally
so this diagnostic survives persistence, catalog reload and reopening. No new
permission, HTTP/file access or automatic external side effect is added.
Focused Agent tests cover budget/role/tool metadata and bounded allocation;
production-App tests cover actual serialized requests after long saved history
and refusal/recovery. No large view model is expanded.

## Current status

The production PR page now opens revision-bound ordinary projectless model
conversations and prepares failed-check investigation drafts without sending.
The current candidate is `artifacts/pr-review-chat-context-final-20261018`; its
long-history and persistent visible-error corrections have separate terminal
verification below. The preceding `pr-review-chat-context-20261018` reached
2830/2830 selected passes with unchanged source/runtime paths and hashes, but
its Activity-only capacity error was not sufficient visible-error acceptance.
The preceding `artifacts/pr-review-chat-ready-20261018` reached terminal state:
2819/2819 selected tests passed, with unchanged source/runtime paths and hashes.
`pr-review-chat-20261018` retains the earlier focused/debugging evidence and its
terminal 2819-test expanded run. The ready candidate additionally waits for
ordinary conversation-model preference restoration before synthetic send actions;
this does not change product readiness/approval policy or weaken assertions.
The previous `pr-inline-comments-20261018` remains frozen historical evidence,
not validation of these changes. The overall functional-parity goal remains active.

## Implemented chain and focused evidence

- `PullRequestsViewModel` captures only currently loaded inspection items.
  `SourceReviewChatCreator` validates source access; `SqliteSourceReviewChats`
  transactionally creates/restores the task and version binding. SHA casing does
  not create a duplicate task. Reopening an archived version explicitly restores
  it; changing base/head creates another conversation rather than repinning it.
- The existing catalog navigates to that task, preserving ordinary new-chat and
  other task drafts. `WorkspaceTaskFactory` revalidates access before persisting
  another user prompt. `WorkspaceRunExecutor` resolves the saved binding and
  passes its snapshot as fixed user context, never as a system message. Saved user
  messages stay free of the injected snapshot. The selected provider/model and
  reasoning effort reach the real Responses request builder/stream parser.
- `WorkspaceComposerDraftsViewModel.TrySeedAsync` seeds the correct cached draft
  through its existing background writer. A loaded empty cache is updated along
  with SQLite; unsaved text, whitespace, attachments, failed reads/writes and
  revision conflicts are not overwritten. A newer user edit during a seed save
  wins; the caller cannot report that its original seed was retained.
- `SourceReviewChatPersistenceTests` covers repository reopen, immutable context,
  revision uniqueness, invalid local environments, revoked/changed credentials,
  forward migration, rollback and preservation of preexisting user data.
  `SourceReviewContextBuilderTests` covers context budgets, omitted/clipped data,
  failure filtering and the smaller investigation budget.
- The production-App controlled-HTTP tests cover manual multi-turn sends, saved
  user/assistant history, different heads, cancellation and explicit continuation,
  access refusal before sending, revocation before a reply and between two actual
  SSE deltas. The latter preserves the first authorized delta but neither presents
  nor persists the second. Unsolicited local/source tool calls produce no tool
  execution/audit record or additional source HTTP request.
- Real shared PR-page buttons are activated with Space. Light/dark 526x601 and
  980x740 checks verify target visibility/focus, empty-cache draft seeding,
  preservation of another draft and no implicit provider/source request. Other
  tests refuse successful/unloaded checks and late navigation after leaving.
  Separate draft tests cover cancellation, disposal and CAS conflict behavior.
  These are controlled transport/temporary SQLite/Headless checks, not native UI,
  production TLS, paid model calls or real source-account verification.

## Debugging record

The first App compile exposed an array `Count`/`Length` typo in draft seeding;
it was corrected without changing the draft protection condition. An earlier
infrastructure expectation was also corrected to the repository's earlier
environment-reassignment refusal, rather than pretending the SQLite trigger was
the first failing boundary.

The first runnable App fixture omitted `HttpClient.BaseAddress`. The request
failed before entering its synthetic transport, while a test awaited an unbounded
`Started` signal. Async/exception inspection proved the invalid-request-URI
failure and the already-terminal product run. Only that identified test child
was stopped; its aborted TRX is not passing evidence. The fake client now uses
the configured base URI and Started/cancellation waits have explicit bounds.
Text async/exception evidence remains in the earlier candidate; temporary local
diagnostic tools and the synthetic-process dump are not product dependencies.
Their cleanup command was rejected by the execution policy; these temporary
files remain only under the ignored earlier artifact directory, not in the
product or a global tool installation.

A patch assertion originally compared JSON-escaped text with literal `+new`;
it now decodes the stored JSON patch and checks its exact content. An initial
stream fixture timed out before observing its first delta. Correct SSE metadata,
explicit command preconditions and model-readiness waits were added; the original
timeout remains in `app-review-chat-focused-fourth.trx`. Metadata alone is not
claimed as a proven root cause. Subsequent complete runs, not the earlier timeout
or a single isolated pass, establish the current result.

## Final verification

The final candidate reached terminal state successfully. `build-final.log`
records the complete Debug solution build with zero warnings/errors.
`structural-terminal.json` audits the 467 changed/new C# and 26 AXAML files,
2656 methods/accessors, maximum method length 78, no size/type/whitespace violations.

| Selected group | Passed | Failed/skipped |
| --- | --- | --- |
| Tools (all) | 644 | 0/0 |
| Infrastructure (all) | 597 | 0/0 |
| Providers (all) | 600 | 0/0 |
| Agent (all) | 155 | 0/0 |
| App (PR/source, workspace, drafts/model/history/failure, automation, approval/navigation/preview filters) | 586 | 0/0 |
| Visual (all) | 300 | 0/0 |
| Total selected | 2882 | 0/0 |

`validation-terminal.json` records all six process exit codes as zero and each
TRX as completed. Before/after identities and path sets match for 2285 selected
source/root-configuration files and 1583 files in the six test runtime directories.
The source selection includes Git-listed tracked/untracked `src` and `tests`
files plus the present root engineering/build configuration. It does not freeze
all ignored artifacts, external services, user data or documentation.
Do not combine overlapping focused or earlier runs into this total. This is
controlled transport/temporary SQLite/Headless evidence, not a full App test run,
native input/EXE restart, real GitHub, paid-model, Release or full-product acceptance.

## Long-history context correction

The pre-fix standalone probe used the real Debug Agent context builder with a
512-character snapshot, 40,000-character old user and assistant messages, a
4096-token model context and 1024-token output reserve. Its terminal result in
`artifacts/pr-review-chat-context-20261018/context-before.log` was
`SnapshotRetained:false, LatestPromptRetained:true`. The oldest user-role
snapshot had been evicted by ordinary newest-turn selection.

The corrected probe supplies the snapshot through `PinnedUserContext` instead
of normal history. `context-after.log` records both snapshot and latest prompt
retained. Nine new Agent cases plus the existing builder cases passed 18/18 in
`agent-context-focused.trx`; 27 review-chat/draft cases passed in
`app-context-focused-second.trx`, including actual Responses JSON with an intact
first snapshot after long persisted history and a second manual turn. Capacity
refusal calls no provider, retains the submitted prompt in history and shows the
localized repair action. Refreshing a larger model context does not send; an
explicit retry succeeds with the same binding. That first context candidate
persisted `Unknown` and checked only Activity, which was insufficient; the
visible/persistent correction below supersedes that limitation.

The initial test build missed the nullable project-ID argument on the repository
test call; the following build corrected it. The first App assertion also assumed
Responses text content was an array, but this text-only request correctly uses
a string. Both failed reports are retained, and the corrected assertion compares
the exact decoded user string, not a JSON substring. These were test defects,
not provider failures or a relaxation of snapshot/role assertions.

## Visible and persistent capacity diagnostics

Tracing the real conversation view established that Activity is visible only
while a run is active. Its failed-task card instead binds TaskFailureHint to the
persisted failure category. The added red assertion in
`pr-review-chat-context-ui-20261018/app-visible-hint-red.trx` therefore failed:
expected the specific context repair action, received the generic task failure.
The previous green counts did not establish visible feedback.

The corrected chain uses Domain's stable `ModelContextCapacity = 16`, exact
Application code `model_context_capacity`, schema 21's transactional allowlist
extension, and the existing WorkspaceFailureText/AppTaskFailureNotice path.
Raw exception/source/provider text never becomes the hint. The same category
survives SQLite reads and conversation catalog reload, is isolated to the task,
and clears on manual successful continuation. No large Workspace model or
per-page visual appearance is added. Schema 20 remains the PR-chat binding
migration; schema 21 only extends persisted failure metadata.

The focused terminal reports show 35 Infrastructure cases and 68 App cases
passing, without combining them into the final expanded total. They cover v20
history/pinned/archive preservation, idempotence, repository reopen, rollback
after a deliberately rejected version insert, explicit migration retry,
allowlisted category roundtrips and invalid metadata. App checks exact versus
spoofed codes, safe presentation/observers, the real persisted selected-task
hint after catalog reload, no implicit retry, and shared-card wrapping at
320/640 widths in both themes. The implementation follows ui-ux-pro-max's
error-feedback/near-context and wrapping guidance, not a new visual system.

The first expanded context-ui run reached terminal state with unchanged source
and runtime identities: all groups except one Infrastructure fixture passed.
That v19 rollback fixture deleted only schema marker 20 and retained marker 21,
so its expected 19 historical versions actually numbered 20. The correction
deletes all markers from 20 onward when constructing that historical fixture;
the rollback assertion remains 19 rather than being weakened to 20. The failed
terminal report is retained; the final candidate reruns the selected groups.

## Explicit remaining scope

This starts a manual review conversation over an immutable, bounded initial
snapshot; reopening the same version retains the first snapshot, not newly
loaded pages. Refreshing the PR to a different base/head creates a different
conversation. A model cannot fetch more PR data in this mode; source-only model
read tools remain a separate gap. Historical local messages remain available
after revocation, but revocation blocks new review turns and observed in-flight
output. This is not cloud auto-review, immediate remote revocation, a server-side
atomic snapshot, an inbox/URL workflow, reviews/activity, viewed/pinned PR state,
or confirmed source writes. Native input, real GitHub and Release validation
are not established by controlled fixtures.
