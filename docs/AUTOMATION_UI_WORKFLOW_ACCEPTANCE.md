# Automation editor and row-action workflows

## Scope and design self-check

This slice extends the UI/UX review from appearance to actual visible-control
behavior. It adds four test files, not a production UI or scheduler correction.
The earlier shared focus/disabled-state correction remains documented separately
in `SHARED_INTERACTION_STATES_ACCEPTANCE.md`.

1. App owns input and presentation, Infrastructure owns scheduling/persistence,
   and the existing Agent/provider pipeline performs the run. No dependency
   direction changes.
2. Tests reuse the production composition, shared visual controls, repository
   ports, scheduler and protocol adapter; no alternate execution path is added.
3. Each fixture owns isolated SQLite data and a loopback model server. Tests use
   ReadOnly schedules and cancellation; no existing desktop window, user data,
   paid endpoint or approval setting is accessed.
4. The four handwritten C# files each contain one type and remain within file
   and method limits. Evidence and the source-audit utility stay outside the repo.
5. Eight connected cases cover success and rejection/cancellation/deletion in
   Light/Dark at 526x601 and 1280x820 using production MainWindow controls.

## Actual UI paths

- The navigation-rail entry opens automations. New opens the real editor.
  Keyboard input supplies the name and prompt. F4 opens project/model/frequency
  dropdowns, and hit-tested popup items select their actual options. Native
  numeric text fields receive keyboard input and commit on blur.
- Save persists the chosen nondefault model, two-hour interval, three-minute
  timeout and ReadOnly policy. A new schedule stays paused and sends no request.
- Enable and Pause update the stored schedule. Run Now creates a separate task;
  it does not consume the foreground draft, selected model or current task.
  Duplicate runs and deletion are unavailable while that run is active.
- Recorded Responses requests verify the saved model ID, medium reasoning effort
  and exact saved prompt in the user message. The controlled server completes the
  run; its stored history opens the corresponding completed conversation. The
  final Markdown answer is hit-tested inside the visible window.
- Invalid Save preserves the name/editor, reports an error and creates neither
  a stored schedule nor a model request. Cancel dismisses the editor.
- Stop produces Cancelled run/task records. Opening that history does not make
  another model request. Escape cancels deletion; confirmation removes the
  schedule while preserving its run and task history. The request count stays one.

Compact actions use keyboard Space after focus/scrolling; wide actions use
hit-tested pointer input. These are Headless Avalonia inputs, not operating-system
desktop automation. State waits include asynchronous command and list/history
refresh completion, not only command invocation.

## Test development and visual observations

Initial failures came from fixture assumptions: Home after F4 did not select a
popup item, and CloseEditorCommand identifies both an icon and a Cancel button.
The fixture now clicks the popup item and disambiguates Cancel by localized text.
An expanded request assertion then incorrectly expected content parts, whereas
the production Responses builder emits string content for these text messages.
It now checks that exact string, retaining model/effort/prompt assertions. The
original 152-case run records 148 passes and those four assertion failures; it
is not a reproduced production-red baseline.

Raw saved/running/editor/history screenshots and ancestor geometry are retained.
In compact mode a card exceeds the finite list viewport: scrolling legitimately
clips its rounded edge. Its actual radius is 12 DIP and shared shadow remains
applied. This observation does not justify disabling viewport clipping or adding
page-local corner/shadow overrides.

## Validation

- `automation-app-verified.trx`: all 152 App automation cases pass, including
  the eight new connected UI cases. `automation-infrastructure.trx`: all 41
  scheduler/timing/repository/migration cases pass. `architecture.trx`: all seven
  architecture cases pass. These disjoint final scopes total 200, with no
  failures, skipped/not-executed or aborted cases and unique execution identities.
  Earlier eight-case and four-case subsets are not added again.
- After all test processes finish, the complete solution build succeeds with
  zero warnings/errors. Eleven recorded test/runtime DLL hashes remain unchanged
  across that build. Scoped whitespace-format and conflict/trailing-whitespace
  checks pass. Roslyn source audit confirms one type per file, 66-118 physical
  lines per file and a maximum method span of 54 lines.
- Evidence is outside the repository at
  `%TEMP%/MossAgent-automation-ui-20261016`: raw TRX, logs, state PNGs/geometry,
  `verified-test-counts.json`, `source-size-audit.json` and before/after hash CSVs.
  The full pixel suite is not repeated for these test-only changes; its earlier
  208-case result remains separate historical evidence, not part of the 200.

## Remaining gates

This slice does not restart the composition, establish restart/no-replay behavior,
or exercise every time-zone/DST rule through the UI. Scheduler/repository tests
retain their own bounded evidence. Opening existing history without a new request
is not the same claim as restart recovery.

Native screenshots and existing-window operations remain user-postponed.
Headless rendering does not establish native Windows DPI, same-state Codex parity
or a complete current-product visual/interaction sweep. The earlier App allocation
failure remains unexplained; these subsets do not replace it or a full eight-suite
checkpoint. No packaging, JSON, AOT/JIT, staging, commit, push or release changes
are made; the full product goal remains active.
