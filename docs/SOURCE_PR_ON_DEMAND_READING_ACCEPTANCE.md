# Version-bound, opt-in PR model reads

## Design and safety self-check

1. Domain owns immutable chat mode and PR revision. Application keeps the existing
   source access and revision-checked inspection use cases. Infrastructure owns
   forward schema 22, preserving old chats as Snapshot. App only selects a mode
   and builds task context; Agent consumes context-aware tool metadata through
   its existing catalog port, never concrete tools or HTTP.
2. Reuse the source read tools, standard executor, source account/proxy clients,
   task audit, history, cancellation and pinned snapshot budget. A distinct
   ReadOnlySource conversation per grant/PR/base/head/mode avoids changing the
   permissions of an existing snapshot chat or another running conversation.
3. Only source.pr_get/files/comments/checks are advertised in connected mode.
   Exact number and base/head constraints are checked before and after approval
   and inside execution. Source use cases recheck account/grant after HTTP and
   inspection reads compare remote revisions before/after. Lists, arbitrary URLs,
   writes, filesystem, shell, browser, MCP and subagents remain unavailable.
   Every query uses task cancellation, a 30-second tool deadline, bounded pages
   and output, and ordinary audit/artifact handling. No opening sends a model
   request or fetches additional source data; only explicit send starts execution.
4. Keep one type per C# file and existing size limits. Add shared wrapper actions
   and localized permission disclosure, not per-page appearance or UI SQL/HTTP.
5. Prove schema preservation/rollback and mode isolation; safe catalog projection;
   exact request scope, denial, approval, revocation, output limits, timeout and
   cancellation; real model-tool-source-model serialization, visible tool results,
   multiple turns, pinned context, reload and manual recovery. Headless shared
   controls and synthetic transports are distinct from native/live acceptance.

## Implementation and evidence

The implementation adds an explicit "打开只读查询对话" action with localized
permission disclosure and accessible name/help. It uses shared controls and a
shared scroll-content gutter, not a per-view appearance override. Schema 22
includes mode in the review-chat key, preserves existing conversations as
Snapshot, and retains the no-project/no-worktree/ReadOnly persistence boundary.
Opening/reopening neither sends nor fetches source pages; modes keep independent
drafts/history and reopening keeps the first pinned snapshot.

Context-aware metadata exposes exactly four source tools only to ReadOnlySource
tasks. Their existing pipeline owns schema, scope, approval, deadline,
cancellation, output limiting and audit. Get verifies the returned revision;
files/comments/checks verify remote base/head before and after the page. Oversized
bound Get JSON is refused without partial Content; legacy UI Get keeps its
existing pipeline truncation behavior. Fixed localized errors explain revision,
scope and output failures to the model without exposing exception/HTTP text.

Agent tool execution now starts on a worker instead of inheriting the caller's
UI synchronization context. Bounded progress, task cancellation and awaited
cleanup retain the same operation identity. If access changes while progress is
being consumed, the review guard can reject the event and cancel the in-flight
operation before the reader's post-HTTP denial completes. The audit then records
either source_scope_denied or tool_cancelled, while the conversation is Failed
with a source-scope hint. Both paths must finish audit, discard content and refuse
the next provider request; the tests assert those effects, not a race-specific
choice of audit code.

### Focused evidence

`artifacts/pr-source-reads-20261018` preserves all intermediate build/test reports:

- Solution builds third/fourth completed with zero warnings/errors. Before the
  worker change, all six model-to-source roundtrip variants failed a direct
  UI-thread assertion in app-ui-thread-red.trx; afterward the original 18 source
  workflow/safety cases passed in app-focused-second.trx.
- The expanded app-focused-fourth.trx passed 36 cases: explicit no-send opening,
  six Responses-model/tool/source/model roundtrips, real tool output JSON, pinned
  user context, audit, visible tool cards, reload, multiple turns, mode isolation,
  PR/SHA/other-tool refusal, before-send/during-HTTP revocation and credential
  changes, before/after-page head changes, cancellation and explicit continuation,
  and four shared-control keyboard/theme/viewport cases.
- tools-focused-third.trx passed 42 source/PR cases including approval-time
  revocation and bound/legacy Get output behavior; agent-focused.trx passed all
  155 Agent tests. infrastructure-focused.trx passed 14 review/context migration
  cases, including v21 preservation, two-mode identity, invalid mode and atomic
  migration rollback/retry. These runs overlap and are not a full-suite total.
- The disclosure geometry test originally selected an unarranged nested
  scrollbar; those exploratory failures remain recorded, not used as product
  proof. After selecting the owning viewer's template scrollbar, the unmodified
  page still failed all four app-disclosure-corrected-red.trx cases: text occupied
  the overlay scrollbar lane. The shared content gutter fixes that boundary;
  app-disclosure-corrected-green.trx records the post-fix check.

These tests instantiate production App composition, temporary SQLite, real
provider request/response serialization and source HTTP handlers. The model uses
synthetic Responses SSE and source responses are controlled; no paid provider or
real GitHub account was used. They do not establish live service compatibility,
native Windows interaction or Release acceptance.

### Frozen candidate

`artifacts/pr-source-reads-final-20261018` contains the final build, structural
audit and eight-project unfiltered regression runner. It captures selected
src/tests/root configuration hashes and every file in the eight Debug test
runtime directories before and after testing. Source/tests/build configuration
are not changed during that run. Read validation-terminal.json for the actual
terminal result; a started run is not a pass. The preceding snapshot/context
candidate remains frozen at artifacts/pr-review-chat-context-final-20261018 and
does not validate this slice.

The eight-project run above reached terminal: 7,906 passed and two App structural
assertions failed, with no skips. Selected source/configuration (2,297 files) and
Debug runtime (2,035 files) had zero path/hash changes. Keep its Failed result:

- SurfaceVariants_DeclareSharedCardAndFieldStyles assumed one literal selector
  per style and did not recognize the shared comma-separated modal/content-gutter
  rule. The follow-up assertion splits selector alternatives and checks both
  variants; no padding/style is duplicated or removed.
- SettingsOverlay_UsesSharedNavigationComponent still expected seven buttons
  despite the earlier source-account settings integration adding the eighth.
  The follow-up checks the exact unique 0..7 command destinations and the
  source-account label, while retaining all shared-component/property checks.

Only these two test contracts change after that frozen run. Production behavior
is unchanged. artifacts/pr-source-reads-verified-20261019 is the separate candidate
for focused contracts, a new solution build/structural audit and another complete
eight-project run. Its own validation-terminal.json, not the preceding results,
must establish whether the follow-up candidate passes.

### Verified follow-up result

artifacts/pr-source-reads-verified-20261019/validation-terminal.json is terminal
Passed. The new solution build has zero warnings/errors; focused contracts/source
tests passed 57/57 before the frozen run. The unfiltered eight-project regression
passed 7,908/7,908, with no failures, skips or aborts:

| Project | Passed |
| --- | ---: |
| Tools | 666 |
| Infrastructure | 602 |
| Providers | 600 |
| Agent | 155 |
| App | 5,351 |
| Visual | 300 |
| Browser | 88 |
| Windows | 146 |

The source/configuration identity set (2,297 files) and Debug runtime set (2,035
files) both have zero path/hash differences before/after. The structural audit
covered 484 changed/new C# files, 27 markup files and 2,738 methods, with one type
per C# file, no size violations and a maximum method length of 78 lines.
Thirty-six Headless frames were saved, including the four new-entry
light/dark/526x601/980x740 cases. The four new-entry frames were visually inspected;
permission text stays outside the overlay scrollbar lane and keyboard focus is
visible. This does not claim every saved frame has received a new visual audit.

The preceding 7,906-pass/two-failure run remains Failed and unchanged. The new
result validates only this current implementation and its tested workflows, not
full PR parity, live GitHub/provider/native behavior, Release or overall product
completion. No commit, push, release or user-window operation was performed.

## Remaining scope

This slice does not implement PR writes, inbox/URL opening, reviews/activity,
OAuth, GitLab, cloud auto-review or CI-log downloads. Even matching base/head
checks are not server-side atomic snapshot isolation. A changed revision requires
explicitly refreshing the PR and opening its matching conversation; no automatic
repinning or retry is allowed. The full functional-parity goal remains active.
