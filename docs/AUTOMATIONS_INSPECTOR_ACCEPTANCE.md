# Automations and inspector acceptance

## Scope (2026-10-08)

The previously disabled automation entry now opens a retained application page.
It supports new/edit/save, explicit project and model selection, hourly/daily/weekly
timing, time zones, approval policy, execution timeout, enable/pause, manual run,
stop, confirmed removal, bounded history and opening the actual run conversation.
Switching pages does not replace the workspace or consume its draft.

The inspector has persistent sources/diff/terminal/browser/audit tabs, a back action
and a close action. Header actions share 32-pixel hit areas and 15-pixel icons.
The existing floating context card remains visible while docked inspection is closed.
Empty, loading and safe failure states replace unusable blank browser/preview frames.

## Design and safety self-check

1. Domain owns schedule/run values. Application owns timing and scheduling through
   `IAutomationRepository`, `IAutomationLease` and `IAutomationTaskRunner` ports.
   Infrastructure owns SQLite/lease implementations. App owns observable presentation
   and the bridge to the existing workspace runner; no dependency points back to UI.
2. Background execution reuses workspace task creation, run tracking, provider streams
   and the standard tool pipeline. It does not implement another execution engine.
   Missing/disabled configured projects or models fail instead of silently falling back.
3. New schedules are paused and read-only. An explicit manual run may invoke a paid
   configured model even when the plan is paused. Tool authorization, approvals,
   task-owned cancellation, timeouts, bounded output and artifact storage remain in
   the standard pipeline. Terminal buttons also use that pipeline. No real external
   model request is part of the automated acceptance tests.
4. Each handwritten C# file contains one type. The scoped AST audit checks the
   500-line file and 80-line method limits. New shared appearance variants, markup
   validation and analyzer rules preserve the existing wrapper-only UI boundary.
5. Focused tests cover real editor-to-run-to-conversation flow using a local SSE
   provider, exact model routing, retained drafts/selections, cancellation, lifecycle
   rejection, unavailable configuration, stale edits, atomic claims, single ownership,
   overlapping runs, timeouts, DST, shutdown and forward-migration rollback.

## Scheduling contract

- Scheduling runs only while MossAgent is open; it is not a Windows service or an
  operating-system scheduled task. Polling is every 15 seconds, not hard real time.
- Missed occurrences while the app is closed are advanced, not replayed. Daily/weekly
  times use the selected time zone. DST gaps advance to a valid local time; repeated
  local times use the later occurrence and do not generate duplicate runs.
- One instance per data directory owns scheduling. A plan has at most one active
  durable claim. Pause affects future starts, not an already-running conversation.
- Stop and application shutdown cancel task-owned execution and await finalization.
  Unfinished records become interrupted on restart. A terminal-persistence failure
  retains the durable claim rather than repeating an uncertain side effect.
- Removal archives the plan and retains its existing run records and conversations.
  Running plans cannot be removed. History queries are bounded; the page displays
  the latest 50 runs for the selected plan.
- Version-nine migration creates separate schedule/run tables transactionally. It
  preserves existing projects, conversations, failures and messages and is idempotent.

## Verification evidence

Temporary scripts, synthetic data, the native QA executable and screenshot files are
outside the source tree or in ignored validation artifacts. They are not release files.
Evidence root: `%TEMP%\MossAgent-automation-inspector-20261008`.

- Full solution regression passes 2,899 tests across seven test assemblies. After
  the final empty-state class/position fix, all 2,206 App tests pass again against
  the rebuilt assembly. Final solution and ordinary debug App builds report zero
  warnings and errors. The scoped 57-file AST audit has no violations; whitespace
  verification and diff checks pass.
- The ordinary, non-elevated JIT App is rebuilt and restarted. Its automation entry
  is enabled, and Ctrl+P opens the new inspector header and centered no-conversation
  state. No schedule or conversation is created in the user's data during this check.

- Deterministic application flow uses the existing loopback SSE test server, not a
  fake success status. The resulting messages/task IDs are read back from SQLite.
- Native QA runs the real App startup/shutdown lifecycle against separate synthetic
  data directories. Sample plans are paused; its synthetic provider is deliberately
  non-executable. The native diff/audit screenshots use real git.status/git.diff tools
  against a temporary Git repository, not seeded tool-result text.
- Passive Python PrintWindow captures check for black output and unexpected dimensions.
  Light 1280x820, dark 980x820 and the dark 980x640 editor are inspected without scaling
  the original images. The editor scrolls while preserving a reachable footer.
- The first captures exposed a native list-item selection frame and unapplied input
  dimensions. Wrapper styles must target the inherited native StyleKey or the owned
  shared class, and automation variants must load after base dialog styles. The
  corrected card has no native thick frame, uses the shared four-sided shallow shadow,
  and the editor prompt has a 96-pixel minimum height with top-aligned text.
- Empty-state tests inspect the actual heading's position, not just the stretched
  wrapper center. Explicit shared classes preserve styling for UserControl wrappers.
- Native Escape closes the synthetic editor, and Ctrl+J switches the synthetic
  workspace inspector. Mouse automation returned `coordinate input geometry is
  unavailable`; native mouse/hover behavior is therefore not claimed as verified.

## Remaining limits

The current Codex reference capture shows a file-preview side panel, not the same
automation/editor/inspector state. It is useful for shell geometry but does not prove
pixel-for-pixel equivalence of these new pages. Same-state Codex screenshots are
still needed for final fidelity work. The subsequent file-preview/rendered-diff
phase is documented in `FILE_PREVIEW_ACCEPTANCE.md`; all Codex automation workflows
are not implemented by this change.

Real paid-provider execution, browser sessions, native terminal interaction and
Windows-service scheduling are not accepted by these screenshots. Terminal pipeline
authorization and late-result boundaries are covered by focused tests; native capture
only checks its inactive presentation. No packaging/AOT/serialization changes are
part of this work; ordinary development remains managed/JIT.

## Editor timing preview (2026-10-08)

The editor now shows an expected next occurrence as fields change, using the
same Application timing/DST rules as scheduling and the selected time zone.
Invalid/incomplete settings replace the old preview with an actionable hint.
Saving still recomputes from the actual save time; this is not a continuously
ticking scheduler or a promise that a paused plan will run. Hourly schedules
ignore the hidden daily time field rather than being blocked by its stale value.

This is App-owned presentation over an existing Application calculation. It adds
no repository mutation, model call, authorization change or migration. Deterministic
clock tests cover daily/weekly/hourly timing, invalid fields, no enable/run/storage
side effect and the hourly hidden-field regression. The native light 1280x820
editor capture in `%TEMP%\MossAgent-preview-interactions-20261008` shows the time-zone
preview and reachable footer; same-state Codex editor comparison remains outstanding.

## Run history presentation and navigation (2026-10-08)

`AutomationHistoryViewModel` and its separate wrapper-only view now own the
selected plan name, bounded recent history, loading/empty/read-failure states,
explicit retry and conversation opening. Rows use shared centered 14-pixel
status icons, elapsed-time labels and actionable localized failure/timeout/
interruption hints. Untrusted stored error codes and repository exceptions are
never echoed into the UI. Open failures preserve the rows and current page.

Design self-check:

1. App owns presentation and navigation through the existing
   `IAutomationRepository` and `IWorkspaceRepository` ports. Domain, scheduling,
   provider routing, approval policies and persistence schemas do not change.
2. History queries request and display at most 50 rows. Foreign-plan rows are
   rejected, and detached/stale row commands cannot navigate. Active project
   identity is checked again against the returned conversation and catalog.
3. Each read has cooperative lifetime cancellation and a 30-second deadline.
   Plan replacement, refresh, page hiding, mutation blocking, catalog locking
   and disposal invalidate pending navigation even after hide/reopen or
   lock/unlock. A noncooperative pending open keeps its opening ownership until
   storage returns; cancellation does not forcibly stop an unresponsive port.
4. The separate view/model remain below the file and method limits and each
   handwritten C# file contains one type. Existing shared wrapper appearance
   variants own status geometry and the reusable thin-scrollbar gutter.
5. Focused tests cover state exclusivity, retry, bounded/foreign results, late
   success/failure, cancelled/disposed scopes, transient locks, unavailable and
   foreign-project conversations, exact command bindings, real page visibility,
   localized safe hints and centered icons/text in both themes.

Native evidence uses separate paused synthetic plans under
`%TEMP%\MossAgent-automation-history-20261008`, not user schedules or model calls.
Python passive window capture inspects light 1280x820 and dark 980x640 pixels.
Actual keyboard interaction verifies that a missing conversation produces an
actionable local error, and that a completed record opens the seeded persistent
conversation without consuming the retained composer draft. The first native
capture exposed an overlay scrollbar clipping trailing text; the shared
thin-scrollbar variant now reserves a content gutter in history and plan lists.
The native acceptance uses keyboard input and passive pixels; mouse/hover
behavior and paid-provider execution are not claimed as verified. Synthetic
previews are closed normally and no user schedule or provider configuration is
changed. Screenshot/comparison Python helpers remain outside the repository.

The current passive Codex capture is still its conversation/file-preview state,
not an automation page. This evidence does not establish pixel-perfect Codex
automation equivalence. Same-state Codex list/editor screenshots remain needed.

Final verification for this slice:

- Solution, ordinary managed/JIT App and native QA builds report zero warnings
  and errors. The final automation/scrollbar filter passes 65 tests. The scoped
  13-file AST audit has zero violations; scoped whitespace verification passes.
- The last full solution run executes 3,011 tests: 3,009 pass and two sidebar
  heading tests fail during temporary-directory deletion with Windows file
  sharing violations, after their layout assertions succeed. A separate rerun
  of all 12 sidebar heading cases passes. This is not a clean full-suite result.
- An earlier broad run also had one command-stop failure; its isolated rerun
  passes and the last broad run passes it. No command execution or unrelated
  test-cleanup code is changed in this slice. Failure logs and the final TRX
  results are retained under the evidence root for investigation.
- Final native screenshots are `history-Light-1280-820/window-accepted.png` and
  `history-Dark-980-640/window-final.png`; `history-scrollbar-comparison.png`
  compares identical synthetic history and native-size crops without scaling.
  The Python helpers, screenshots, synthetic SQLite data and QA host are not
  repository source or release assets. No commit, push or publish is performed.

## Scheduler shutdown ownership (2026-10-09)

Shutdown previously waited for the scheduler gate before cancelling operations.
A recovery, catalog read or claim that needed cancellation to return could hold
that gate indefinitely. Start, tick and manual-run operations now register with
an Application-owned lifetime and link caller cancellation with shutdown.
Shutdown rejects new work, cancels current/queued operations, drains their
ownership, awaits poll/execution/finalization and only then releases the lease.
Concurrent stop/dispose calls share the same completion task.

Design self-check:

1. Application owns scheduler lifecycle; existing repository/runner/lease/clock
   ports and dependency direction remain unchanged. No UI, SQLite or OS access
   is added to the scheduler.
2. The existing execution path remains authoritative. No parallel execution
   engine, tool bypass, schedule schema or model-routing change is introduced.
3. Caller cancellation preserves its token identity and does not cancel another
   queued owner's operation. Noncooperative ports and runners stay owned until
   return; shutdown is not a forced-termination guarantee. Started terminal
   persistence deliberately completes without the cancelled execution token.
   A late durable claim never invokes the runner, is finalized as cancelled
   when storage permits and otherwise retains the durable claim with only
   `persistence-error`. Uncertain effects are not automatically retried.
4. The nine scoped C# files each contain one type, are at most 195 lines and
   have methods at most 35 lines. No appearance variant, AOT, serialization or
   packaging change is part of this slice; development remains managed/JIT.
5. Twelve new deterministic cases cover cancellation-before-gate shutdown,
   cooperative/noncooperative ports, queued cancellation isolation, repeated
   stop/dispose, late read/claim rejection, safe failed finalization and retained
   runner/terminal-persistence ownership. Tests have bounded cleanup rather than
   leaving a hung scheduler behind.

Evidence root: `%TEMP%\MossAgent-automation-shutdown-20261009`.

- The initial eleven-case reproduction has ten failures and one pass before
  the fix (`red/shutdown-red.trx`). After the fix, those eleven cases pass in
  three consecutive runs. The added terminal-persistence case and all existing
  Infrastructure automation tests pass together: 41/41 in
  `latest/automation-latest.trx`. The scoped Roslyn audit and whitespace check
  pass, and the solution/native QA builds report zero warnings/errors.
- Ordinary, non-elevated native App lifecycle runs use separate temporary
  SQLite/project/provider data and a bounded loopback-only SSE provider. Both
  light 1280x820 and dark 980x640 manually start one plan, stop it, start again
  and close the actual window while running. Both hosts exit normally with
  `QA_EXIT: 0`, exactly two model requests and two persisted cancelled runs
  whose actual workspace tasks are cancelled/read-only. The plan stays paused
  and the retained workspace draft is checked throughout the open window.
- Passive Python capture checks for black output and unexpected native dimensions.
  Each native case saves ready/running/stopped/pre-close PNGs without rescaling.
  The dark window's first input attempt reports unavailable geometry; a fresh
  returned window binding and observation permit the single retry. Successful
  button activation does not establish all mouse-hover/pointer-state behavior.
- No real provider, paid request, tool execution, user schedule or user settings
  participate in the native flow. QA sources/scripts/data remain outside the
  repository, with the executable in ignored validation artifacts. Current Codex
  reference pixels still do not show the same automation state; these captures
  do not claim pixel-perfect Codex automation equivalence.

Final verification for this slice:

- App automation/shutdown regression passes 62/62
  (`latest/app-workflow-latest.trx`). The latest full solution run passes all
  3,241 tests across seven assemblies (`full-latest/`), including the new
  terminal-persistence case. Narrow whitespace verification reports no changes.
- The earlier full run passed 3,239/3,240: the Windows shell timeout test failed
  while reading its synthetic `process.pid` due to a file-sharing violation.
  Its isolated rerun passes, and the latest full run passes all 57 Windows
  cases. The original failure/TRX is retained under `full/`; no unrelated
  shell implementation or test fixture is changed to mask it.
- `native-automation-run-stop-comparison.png` compares original-size plan-card
  crops in both themes and running/stopped states without resizing. It is
  a MossAgent state comparison, not a same-state Codex reference comparison.
  Both QA hosts and test hosts exit; no commit, push or publish is performed.
