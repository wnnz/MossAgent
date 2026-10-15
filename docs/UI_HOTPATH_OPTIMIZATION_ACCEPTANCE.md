# UI hot-path optimization acceptance

## Scope: 2026-10-11, chat presentation

- Live tool results and persisted tool messages use `ToolConversationPayload.FromEvent`
  instead of maintaining two copies of the raw-field mapping. Success, failure,
  detail text, dialogue and error codes retain their existing representation.
- User bubbles and tool activity rows no longer build invisible regular-response
  Markdown blocks. Their raw content, summary/details, attachment metadata and
  observable text changes remain available. Assistant/system messages and subagent
  answers keep their existing Markdown/code/table rendering and streaming updates.
- Table detection checks for a candidate pipe before allocating cell lists, and
  validates separator cells before splitting headers. The unreachable empty-header
  array check is removed; the splitter always emits at least one cell, including
  an empty header. A pipe on either the header or separator still supports one
  column, and code fences, escaped pipes, inline-code pipes and short rows retain
  their existing semantics.
- These are synchronous App-owned presentation operations. No Application port,
  tool execution, authorization/approval, cancellation, timeout, output budget,
  audit, database migration, visual control/style or package change is introduced.
  Debug remains JIT and publishing remains Windows x64 single-file, without AOT
  or source-generated JSON. No commit, push or publish is performed.

### Focused verification

- Before the production refactor, 50 characterization/rendering/serialization
  cases pass, including 22 new table and live-versus-persisted mapping cases.
- After the refactor, 233 targeted parsing, response rendering, tool progress,
  conversation/history/cancellation and subagent flow cases pass. Six additional
  cases verify that user/tool rows have no hidden blocks while regular streaming
  and subagent Markdown remain intact. The response suite covers both themes
  and narrow/wide layouts; this is headless coverage, not new native screenshots.
- The solution build passes with zero warnings/errors. Scoped whitespace checks
  pass. All eight changed handwritten C# files contain exactly one type and stay
  within the file/method hard limits; the longest file is 209 lines. The existing
  event dispatcher shrinks from 75 to 72 lines; all other methods are at most 38.
- The complete serial solution run passes 4498/4498 cases across eight assemblies,
  with zero failed/skipped cases. After extracting a test-only assertion helper,
  the solution is rebuilt and the 233 targeted cases are rerun against that build.
  Final scoped formatting and repository `git diff --check` pass. The index stays
  empty; status changes from the saved baseline consist only of the three new
  focused test files, with all pre-existing worktree changes preserved.

### Allocation measurement

A repository-external .NET 10 Release harness links the original or refactored
parser with the same production block types. For each 100-line input it warms up
256 parses, then measures 1000 calls with `GC.GetAllocatedBytesForCurrentThread`.
The input is allocated before measurement; output block counts remain identical.
An additional deterministic 5000-input mixed Markdown corpus produces identical
serialized-block SHA256 checksums before and after the change.

| 100-line parser input | Before bytes/call | After bytes/call | Reduction |
| --- | ---: | ---: | ---: |
| Ordinary text without pipes | 115632 | 33264 | 71.23% |
| Non-table text containing pipes | 113240 | 72056 | 36.37% |
| Valid table with 100 data rows | 61168 | 61168 | 0% |

This measures parser allocation only, not full-application memory, latency or
the separate savings from omitting hidden message blocks. No native app window,
provider credential, paid API request or user database is used by the harness.
Snapshots, benchmark source/results, audit output, logs and TRX stay outside Git:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-chat-optimization-20261011-082302`.

This optimization slice does not complete the broader product/fidelity goal.

## Scope: 2026-10-10

- `SubagentDialogueProgress.Apply` writes directly into one final array instead
  of copying history into a temporary list, growing that list on append, then
  copying it again into the returned array.
- `WorkspaceSidebarProjectList.Refresh` builds manual-rank dictionaries only
  for manual sorting, not for activity sorting where they are unused.
- Project subscription insertion uses `HashSet.Add` to detect new rows rather
  than checking membership and then inserting with a second lookup. The existing
  removal and disposal paths remain unchanged.
- No shared abstraction, cache, package, native control, visual style, database
  migration or public contract is added. Debug remains JIT; publishing remains
  Windows x64 single-file without AOT. No commit, push or publish occurs.

## Design and safety boundaries

- App owns these synchronous, in-memory presentation operations. Existing
  Application ports, repository calls and module dependencies remain unchanged.
- Every progress event still receives its own array; previous history, completed
  replies and tool messages are never modified. Only a trailing partial message
  with the same case-sensitive role is replaced. Different roles append.
- Activity/creation/name/identity tie-breakers, saved manual ordering, unranked
  project fallback and source collection order remain unchanged for valid
  preferences. Inactive manual ranks no longer need to be enumerated.
- Project row instances retain selection, expansion and search state. Reordering
  continues to use moves instead of resets. Removed rows and disposed lists no
  longer influence the visible list, as before.
- Task ownership, cancellation, authorization, approval, timeouts, output budgets,
  audit and persistence remain upstream/downstream; no execution path changes.
- All four changed handwritten C# files contain exactly one type and stay below
  500 lines. The longest changed-file method is 26 lines. No appearance changes
  require a new screenshot comparison; this is not native visual acceptance.

## Deterministic regression verification

- Seventeen new characterization cases pass against the original production
  implementations before refactoring. Together with three existing dialogue
  cases, that first run passes 20/20.
- Dialogue cases cover null/empty history, the full partial/same-role matrix,
  immutable previous snapshots, independent branches, array/list/read-only inputs
  and retention of message references.
- Sidebar cases cover inactive saved ranks, activity moves, manual fallback,
  tied name/identity ordering, unchanged-list notifications and subscription
  cleanup after removal/disposal. Existing workspace/drag/preference tests retain
  cancellation, rejected reentry and failed-save coverage.
- After refactoring, 114 targeted dialogue, presenter, workspace, sidebar,
  preference, drag and search cases pass.
- Focused whitespace verification and the four-file Roslyn size/type audit pass.
- The solution build succeeds with zero warnings and errors. The subsequent full
  run passes 4285/4285 cases across eight assemblies, with zero failed or skipped
  cases. No paid provider calls are required by these checks.
- Both repository-default and focused `git diff --check` pass. The index remains
  empty; all pre-existing worktree changes remain uncommitted.

## Allocation measurement

A repository-external .NET 10 Release harness links the original or refactored
dialogue helper and the same production message record. Each scenario warms up
2000 calls, then measures 10000 calls with `GC.GetAllocatedBytesForCurrentThread`.
Histories and incoming messages are allocated outside the measurement. Both
versions produce the same count checksum and preserve the incoming last message.

| Operation | Previous messages | Before bytes/call | After bytes/call |
| --- | ---: | ---: | ---: |
| Append | 0 | 120 | 32 |
| Append | 16 | 624 | 160 |
| Replace trailing partial | 16 | 336 | 152 |
| Append | 128 | 4208 | 1056 |
| Replace trailing partial | 128 | 2128 | 1048 |

This sample reduces dialogue-helper allocation by approximately 51-75%. It does
not measure full application memory or establish a general latency improvement:
the longest-history refactored sample takes longer in this short run. No live
provider request, credential, user database or native app window is used.

Original source snapshots, harnesses, CSV, logs and TRX files stay outside Git:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-hotpath-optimization-20261010`.

This finishes only this optimization slice, not the full product/fidelity goal.

## 2026-10-12: Inspector layout notification cleanup

- The two layout-dimension setters now share finite/nonnegative validation,
  the existing 0.5-DIP tolerance, accepted-value storage and margin comparison.
  Accepted measurements are still retained while the card is hidden or the
  viewport is wide, so showing the card uses its latest dimensions.
- Presentation notifications compare the previous visible card, panel state
  and computed inset. Toggling the floating card no longer republishes the
  unchanged panel label. Restoring another task with identical presentation
  no longer republishes the tab, title, panel, visibility or inset.
- A task's complete restored state is assigned before any notification. The
  existing per-project/per-task preferences, archive cleanup, property-changing
  behavior of the panel setter, dispatcher ownership and disposal guards remain.
  No cache, global state, new port, style or database migration is introduced.

### Required self-check

1. State presentation remains in App's existing inspector view model; dependency
   direction and the shared visual-control boundary are unchanged.
2. Existing catalog events, command bindings and observable properties are reused.
3. The change performs no tool execution, file access, process launch or provider
   request; authorization, approval, timeout and output limits are unaffected.
4. The production file has 148 physical lines, one type and a longest method of
   21 lines. The new test file has 217 lines, one type and a longest body of 22 lines.
5. Deterministic tests cover real changes, unchanged snapshots, hidden/wide
   measurements, both overlap-boundary directions, invalid/subpixel inputs,
   complete state restoration and disposal. Existing inspector ownership,
   rejected navigation, archival and background-run tests also pass.

### Verification and scope

- The 21 new cases run against the original implementation first: 8 pass and
  13 fail on redundant notifications. After refactoring, all 21 pass; the focused
  inspector/ownership/safety/main-window group passes 61/61.
- Each of four same-region resize cases performs 1,000 two-operation cycles:
  2,000 accepted width changes publish zero inset notifications, without changing
  the inset. This is an event-count regression, not an application-wide speed,
  frame-rate, allocation or memory benchmark.
- Architecture constraints pass 7/7; the solution build has zero warnings/errors.
  Scoped whitespace verification and Roslyn file/type/body-size auditing pass.
- This optimization slice does not change appearance or run a fresh native
  screenshot comparison. Preceding toolbar changes are retained and covered by
  the application regression; their native evidence and remaining visual gaps
  are recorded in `CONVERSATION_HEADER_COMPONENT_ACCEPTANCE.md`.
- The prior sidebar full-run failures remain recorded in
  `SIDEBAR_REFRESH_OPTIMIZATION_ACCEPTANCE.md`; this slice neither changes nor
  relaxes the intermittent zero-allocation guards.
- Test data is synthetic and isolated; no paid/live provider, user credentials
  or production database is used. Debug remains JIT, release remains Windows x64
  single-file, and AOT/JSON source generation are not reintroduced. No staging,
  commit, push or publish is performed; the broader product goal remains active.

Evidence is outside Git at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-inspector-notifications-20261012-122119`:
original source/document snapshots, `baseline.trx`, `focused.trx`,
`architecture.trx`, `solution-build.log`, `whitespace.log` and `source-audit.log`.

### Initial complete App run

- The first full App run passes 3,797/3,798, with one failed menu integration test;
  this run is not classified as passing. Its existing locator assumes the flyout
  card directly contains `AppContentControl`, whereas the preceding toolbar change
  adds a layout `StackPanel` around the business slot and expansion action.
- `ConversationHeadingLayoutTests` now locates the unique shared business-slot
  control and asserts that its content is the header's actual menu. Label, target,
  locked-command rejection, keyboard close, tab removal and retention of the
  unarchived task are still checked. No production behavior or assertion is relaxed.
- `app-full.trx`, `ConversationHeadingLayoutTests.before.cs` and
  `heading-source-audit.log` preserve the failed run, original test and final audit.

### Final verification

- Expanded header, inspector, icon, shell and visual-component coverage passes
  370/370, including the updated menu integration test and all 21 new cases.
- The second complete App run passes 3,797/3,798; the menu test now passes. Its
  sole failure is the unchanged `WorkspaceConversationFlowTests` failure-response
  case: `Directory.Delete` at line 44 cannot remove its temporary root because
  another process holds it. The failing process is not identified or repaired;
  this complete run also remains classified as failed.
- An independent replay of both conversation-flow cases, all 21 new inspector
  cases and the menu-close case passes 24/24. No assertion is weakened, cleanup
  delay added or unrelated conversation-flow source changed. Neither replay nor
  the 370-case group is presented as a passing complete App run.
- Final solution build has zero warnings/errors. Final solution-scoped whitespace
  verification passes; changed-file diff checks and source-size audits pass. The
  updated heading test remains one type, 246 lines, with a longest body of 47 lines.
- Other complete test assemblies are not rerun in this App-only slice, except
  the seven architecture constraints. No new native screenshot or release publish
  is performed, and the index remains empty.

Additional evidence: `header-inspector-broad.trx`, `app-full-final.trx`,
`final-replay.trx`, `solution-final-build.log`, `whitespace-final.log` and
`diff-check.log`, in the same repository-external evidence directory.

## 2026-10-12 Inspector tab notification cleanup

The existing App-owned inspector state remains the single source of truth.
Direct tab selection and per-task restoration now share changed-value checks
instead of invalidating every title and visibility flag on every transition.
`home` and `sources` retain their common title/source pane without redundant
notifications. No cache, new service, persistence shape, appearance variant,
execution port or authorization policy is introduced.

- `InspectorTabNotificationTests` covers all 36 ordered tab pairs through direct
  selection and task restoration, plus six invalid values: 78 deterministic cases.
  It checks the exact notification multiset and the complete published tab state
  during every callback, including no-op transitions and the home/source alias.
  The existing layout suite retains disposal, dimensions and context-card checks.
- The original implementation fails 60/78 new cases; its 18 no-op/invalid cases
  pass. After the focused production change, all 78 new cases and the existing
  header, toolbar, inspector and menu workflow coverage pass 145/145.
- A changed tab previously publishes eight properties. It now publishes two to
  five, depending on the transition; equal tabs still publish none. These are
  deterministic event counts, not full-app timing, allocation or memory results.
- The production file remains one type, 149 lines, with a longest body of 21
  lines; the new test is one type, 121 lines, with a longest body of 20 lines.
  Changed C#/AXAML syntax and size audits pass, as does scoped whitespace
  verification. The solution builds with zero warnings/errors.
- Tests use isolated synthetic data and make no provider requests. This
  notification-only change does not add a fresh native tab-switch screenshot;
  the preceding final-offset toolbar capture and context-toggle observations
  are recorded separately in `CONVERSATION_HEADER_COMPONENT_ACCEPTANCE.md`.
  Debug stays JIT and release stays Windows x64 single-file, without AOT or
  JSON source generation. No files are staged, committed, pushed or published.

Evidence remains outside Git at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-inspector-tabs-20261012-131604`:
original source/document snapshots, `tabs-red.trx`, `tabs-focused.trx`,
`solution-build.log`, `whitespace.log` and `source-audit.log`.

### Final App and architecture verification

- The final complete App run passes 3,877/3,878 with no skipped cases. All 78
  new tab-notification cases and both preceding activity-color cases pass.
  The sole failure is again the unchanged failure-response case in
  `WorkspaceConversationFlowTests`: temporary-root deletion at line 44 raises
  `IOException` because another process holds the directory. This complete run
  is classified as failed, not as a full pass.
- An independent replay of both conversation-flow cases passes 2/2. The process
  holding the directory has not been identified or repaired; no unrelated
  cleanup source, retry policy or assertion is changed to hide the failure.
- Architecture constraints pass 7/7, including repository-wide handwritten
  C# type-count/file-size checks. Changed-file Roslyn body-size and syntax checks
  and tracked/untracked-path whitespace checks pass. No allocation guard is
  relaxed. Other complete test assemblies are not rerun in this App-only slice.
- Final toolbar screenshots use the restored minus-one-pixel implementation;
  the ordinary-permission synthetic native host exits normally with `QA_EXIT: 0`.
  This does not establish native dark/narrow/DPI coverage or full-product parity.
  The complete product goal remains active and the Git index remains empty.

Additional evidence: `app-full.trx`, `architecture.trx`,
`conversation-replay.trx` and `diff-check.log`, in the same external directory.

The later Git-status lifecycle work, its independently diagnosed UI wait-chain
repair and a complete 5,425/5,425 final test run are recorded separately in
`REPOSITORY_STATUS_LIFETIME_ACCEPTANCE.md`. That later success does not replace
the failed runs or prove the earlier directory holder's identity.

## 2026-10-13: Bounded preview projection cleanup

### Design and safety self-check

- App owns in-memory code-preview/search projection. Both use the existing
  parser's bounded line reader; no new service, port, package or dependency is
  introduced. Authorized snapshot readers and tool execution remain upstream.
- The reader scans only the first 131,072 UTF-16 code units and materializes at most
  2,000 lines. Limits apply before CRLF normalization, as before. Lone carriage
  returns, empty/trailing lines, literal case-insensitive matching, diff gutters
  and the existing localized truncation row retain their previous semantics.
- Search uses one indexed pass instead of tuple/filter/projection iterators.
  Its match list is lazy, so unsuccessful queries do not allocate a temporary
  list. Content reset clears the query and refreshes results once, removing the
  duplicate refresh when a previous query was nonempty. Closing an open search
  still publishes its own visibility change.
- There is no I/O, authorization, approval, timeout, cancellation, output-budget,
  persistence or secret-handling change. Snapshots remain instance-owned; no
  cross-tab cache or mutable global state is added. JIT debugging and Windows x64
  single-file publishing without AOT remain unchanged.
- The production files have 81 and 92 lines. Each changed/new handwritten C# file
  contains one type, and changed methods remain below the 80-line hard limit.
  Tests cover both successful projection and hidden-line/character rejection,
  content replacement, notification counts and independent tab state.

### Focused verification and allocation evidence

- The pre-change run passes 36/41 cases and fails five: one duplicated result
  refresh and four allocation guards. The post-change run passes 41/41, including
  all 34 new cases. Plain/diff parsing, newline boundaries, literal search,
  wrapping and unavailable/disposed preview behavior retain coverage.
- Measurements use `GC.GetAllocatedBytesForCurrentThread` after warm-up. The
  fixture has 2,000 visible `shown` lines followed by 100,000 hidden newlines and
  a hidden suffix, all within the character limit. Fixture allocation is outside
  the measured region. These are projection microbenchmarks, not whole-App
  memory, startup or latency measurements.

| Operation | Before bytes | After bytes |
| --- | ---: | ---: |
| Plain-text parsing | 1,143,680 | 360,608 |
| Diff parsing | 1,089,248 | 306,176 |
| Search content reset | 896,240 | 97,104 |
| 1,000 alternating unsuccessful queries | 470,480 | 150,904 |

- The unsuccessful-query guard allows 192,000 bytes, leaving runtime warm-up
  headroom while still rejecting the original filtering pipeline. The first
  successful focused run measured 150,904 bytes against its initial 160,000-byte
  budget; broad regression uses the more conservative budget, not a zero-allocation
  assertion. Required property notifications remain included.
- Scoped whitespace checks pass after expanding the parser's existing compressed
  extension switch entries. The complete solution builds with zero warnings/errors.
- Initial external build output passes focused tests but cannot satisfy existing
  source-level tests that locate the repository through the test assembly's parents.
  That unsuitable full-run attempt is stopped after the root-discovery failures;
  its logs are retained. Full verification uses separate ignored repository `obj`
  output instead, without changing tests or overwriting the running Debug app.

No AXAML or appearance is changed. Native screenshots, live/paid model calls,
publication and whole-product visual parity are not claimed for this slice.
Evidence, pre-change source, TRX and logs stay outside Git in
`%TEMP%\MossAgent-preview-cleanup-20261013-083312`.

### Complete regression result and remaining limit

- The eight complete test assemblies pass 5,650/5,651 cases with zero skipped.
  Agent 119, Visual 12, Browser 76, Infrastructure 199, Windows 137, Providers
  516 and Tools 491 pass completely. App passes 4,100/4,101, including all 34
  new cases. This complete run is classified as failed, not as a full pass.
- The sole failure is the untouched
  `ProviderCatalogLookupTests.WarmModelCommandAndAvailabilityChecks_DoNotAllocate`
  case with `missing: false`: its zero-allocation assertion observes 2,920 bytes.
  A subsequent 217-case preview/lookup regression passes completely, including
  that case, rendered preview interactions and all new allocation guards. No
  provider implementation, old allocation assertion or test isolation is changed.
  The initial failure's cause is not established; the passing replay does not
  replace the failed complete App run.
- Subsequent focused/full measurements retain the parser/reset values in the
  table and measure 144,000 bytes for 1,000 unsuccessful queries. All query-budget
  checks pass. These samples remain workload-specific, not App memory claims.
- Repository architecture checks pass 7/7 within the Agent run. The scoped Roslyn
  audit verifies one type per file, no syntax errors and a longest method of 37
  lines. Scoped whitespace checks, explicit tracked/untracked source comparisons
  and the repository-default `git diff --check` pass.
- Builds use the existing Debug/JIT configuration in isolated output. The running
  default Debug App is not restarted or overwritten; its App DLL remains SHA-256
  `319A951FBB3562556F23F378B0EB6648059445D29B914560D107217D0E5D4FEC`.
  Automatic temporary-output cleanup is rejected by the execution-tool policy;
  outputs remain in ignored repository `obj` and the external evidence directory.
  Source baselines, reports and logs remain available. The Git index stays
  empty, all pre-existing changes are retained, and no commit/push/publish occurs.

This completes only the preview-projection cleanup, not the whole product goal
or a repair of the pre-existing allocation-test failure.
