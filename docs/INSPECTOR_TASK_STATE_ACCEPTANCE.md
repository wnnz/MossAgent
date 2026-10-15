# Task-owned inspector presentation

## Scope and design self-check

1. App owns presentation, through `WorkspaceInspectorViewModel`. MainWindow
   retains its existing binding/command surface and forwards notifications.
   Application, Browser, Infrastructure and Windows dependencies are unchanged.
2. The existing catalog selection and archive events identify owners. Existing
   file-preview, page-navigation and browser-session ports remain authoritative;
   no alternate tool execution or browser-navigation path is introduced.
3. Retained state contains only a tab key and two display flags. This change
   performs no file, network, process, database or model operation. Approval,
   authorization, timeout, cancellation and output bounds are unchanged. The
   background-run regression uses an isolated loopback provider and SQLite data.
4. The controller and each handwritten test file have one type, with files and
   methods below the 500/80-line hard limits. MainWindow is smaller, rather than
   extending its near-limit class or using partial files. No visual dimensions,
   AOT, source-generated JSON, packaging or shared-style exceptions are added.
5. Regressions cover successful round trips plus invalid/locked/foreign/null
   navigation, disposal, record replacement, archive, reload, coherent observer
   snapshots and background schedule completion/cancellation.

## Behavior

- Every conversation retains its selected inspector tab, docked visibility and
  floating-context-card preference separately for this window's lifetime.
- The first visit to a conversation uses the sources/home tab, a closed docked
  inspector and an enabled floating card. The existing view determines when the
  card can actually be displayed; an empty draft does not invent task content.
- Empty drafts retain separate presentation for each project, including the
  no-project context. Starting a conversation does not copy its draft's state.
- Task identity, not record equality or display title, owns state. Title/status
  replacements do not reset it, and a background task cannot select its pane.
- Restoring a state replaces all three fields before publishing notifications,
  preventing observers from seeing another task's tab with the previous flags.
  The shared host continues to own layout measurements and splitter width.
- Archiving discards the retained task state. Restoring the archived conversation
  starts with default presentation. Dispose removes event handlers, clears the
  cache and ignores subsequent mutations/selections.
- This does not persist preferences across application restarts, restore closed
  file-preview tabs, or create/release browser or terminal sessions.

## Deterministic evidence

Evidence root: `%TEMP%\MossAgent-inspector-ownership-20261009`.

- Before implementation, the 13 new ownership cases reproduce nine failures and
  four passes (`red/ownership-red.trx`), including inherited tabs/docking, draft
  presentation and missing context-card message inset.
- After implementation, 109 related MainWindow, inspector, navigation and
  embedded-browser isolation cases pass (`focused/inspector-focused.trx`).
- The additional safety/background regression passes 31/31, including the 22 new
  ownership/safety cases (`safety/ownership-safety.trx`). The background cases use
  the actual scheduler, App runner, normalized provider stream and repositories,
  hold one loopback model response, then complete or cancel it. The foreground
  task, automation page and retained browser-inspector state remain unchanged.

## Native window and screenshot evidence

- The production Debug executable runs under ordinary permissions with fresh,
  separate project/task/SQLite data. The two seeded plans stay paused, the
  provider points to an unused loopback port and no real model, user schedule,
  daily browser profile or user database is used. The QA program/scripts/data
  remain outside the repository.
- Windows Computer Use input opens A's browser inspector, visits B's default
  presentation, opens B's terminal inspector and returns to A. Passive Python
  PrintWindow captures confirm A restores browser rather than inheriting terminal.
  `inspector-task-roundtrip.png` collects original-size sidebar/pane crops from
  those four 1920x1020 native frames without resizing their pixels.
- The actual automation page is visited in light and dark themes; selecting the
  already-selected A conversation returns to its retained browser inspector.
  No plan is run in this native window; actual scheduled completion/cancellation
  is covered by the isolated scheduler regression described above.
- Native checks include maximized, restored and pointer-resized windows. After
  resize, one close action is refused because its stale geometry identifies a
  non-target window. No input is sent to that window. Fresh enumeration, binding,
  activation and screenshot observation allow the single retry to succeed.
  Diagnostic capture `dark-small-geometry-before-rebind.png` is retained and is
  not labeled as a successful close. The actual final small frames are 980x642.
- In the small dark window A's inspector is closed, B restores its terminal tab,
  and A restores its closed state and floating card. Original full-window frames
  appear in `inspector-small-state.png`. Native browser/terminal processes are
  not started by these presentation checks. The App closes normally; its owned
  PID is no longer present and native stderr is empty.
- `codex-mossagent-shell-native.png` compares original-size shell crops from the
  passive Codex reference and the initial MossAgent frame. Codex's eight-pixel
  maximized system border is cropped, not rescaled. At sample y=500 the first
  white main-background pixels are x=316 and x=395 respectively. This exposes
  an outstanding physical scaling/geometry difference, not pixel equivalence;
  its DPI/zoom cause has not been established by this presentation fix. The
  conversations, project lists and inspector contents also differ. Exact crop
  rectangles and native sizes are recorded in `native-comparison.json`.

## Final verification

- All 3,706 tests pass across seven solution test assemblies, without failures
  or skipped cases (`full/`, `full-summary.csv`). The latest solution build has
  zero warnings and errors (`build.log`).
- Scoped whitespace formatting, normal Git diff checking and the Roslyn
  one-type/file/method-size audit pass. MainWindow is 435 lines (largest method
  75), the inspector controller 143 (16), ownership tests 158 (23), and safety
  tests 139 (36). Existing unrelated work stays untouched; nothing is staged,
  committed, pushed or published.

## Remaining acceptance

- Native screenshots and input evidence must be reported separately from
  Headless state assertions; a tab named browser is not proof of an initialized
  WebView2 session or successful concurrent DOM operations.
- Codex automation/editor screenshots in the same state, full pixel fidelity,
  multiple DPI combinations and two-session native browser release/DOM/capture
  acceptance remain outstanding. Do not mark the overall product goal complete
  based on this presentation fix or its passing regression.
