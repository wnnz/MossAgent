# Sidebar fidelity acceptance - 2026-10-07

## 2026-10-10 task-row cleanup

- Project, pinned and recent lists now share `ReplaceTasks` and `SelectTask`.
  Equal immutable task snapshots at the same position retain their existing row;
  identical row sequences produce no collection reset or add/remove notifications.
  Changed title, status or other task fields replace the row rather than keeping
  stale command data. Duplicate project entries still own independent rows.
- Each list retains its own filtering, order, pinning and project-exclusion rules.
  Recent-task deduplication directly chooses `MaxBy(UpdatedAt)` and retains the
  first snapshot on tied timestamps and the previous cross-task tie order.
  The helper does not cache hidden rows or add repositories, mutable global state,
  security decisions, timeouts, schema changes or visual styling.
- Fourteen new regressions cover equivalent refreshes, selection, changed task
  snapshots, empty/no-match results, duplicates and tied timestamps. The narrow
  final selection passed 18 tests. An initial shared fixture compared pinned
  input with deliberately unpinned recent/project rows; its assertion was fixed,
  not the production filter. Full solution verification passed 3843 tests, with
  zero failures/skips; the solution build had zero warnings/errors.
- The rebuilt ordinary-permission App restored the selected project/task in a
  fresh process. Native checks covered dark 980x640, hiding project context and
  reading restored subagent/tool details. Light 1920x1032 checks used the earlier
  host build before this list-only cleanup. These are interaction checks, not an
  allocation benchmark or full Codex pixel-fidelity acceptance.
- Evidence is outside the repository at
  `%TEMP%/MossAgent-subagent-protocols-20261009-2325`: `sidebar-final.log`,
  `solution-build.log`, `solution-tests.log`, `cleanup-format.log`,
  `cleanup-audit.log` and `native/dark-restored-details.png`.
  No staging, commit, push, publish or AOT changes were made.

## Ownership and scope

- App owns the sidebar composition and shared navigation appearance. Existing catalog,
  task-selection, pin, rename and archive commands remain authoritative.
- Pinned tasks, the projects heading, project groups and recent tasks share one viewport.
  Pinned tasks no longer have a separate 160-pixel scroll limit.
- Shared styles own 30-pixel task rows, one-pixel list spacing, 32-pixel closed-project
  steps, eight-pixel expanded-group separation, title fading and the light rounded thumb.
- Resting titles use the trailing space; hover, focus, open menus and running snapshots
  reserve the action slot. Short titles retain the same viewport and are not faded at
  their natural text width. Full titles remain accessible through names and tooltips.
- Running snapshots show a 12-pixel activity ring. Selection alone never implies a run.
  Target replacement and detachment close stale menus without invoking selection.
- Brushes, transforms and animation keyframes are non-control value objects. Negative
  tests still reject native buttons, text blocks, paths, borders and scroll viewers.
- No ports, migrations, tool approval rules, provider routing or publish settings changed.
  Debug/release remain ordinary JIT; Windows x64 compressed single-file publishing remains.

## Validation

- Initial five focused regressions failed against the old composition and title layout.
- Final sidebar/navigation/component boundary selection: 137 tests passed.
- The short-title viewport assertion additionally passed all four theme/row variants.
- Full Debug solution build: zero warnings and zero errors.
- Full serial regression: 2638 passed, zero failures or skips. An earlier parallel run
  had a process-lifetime race and a history-flow timeout; both passed the serial rerun.
  No unrelated test or production behavior was changed to conceal those failures.
- Focused C# whitespace verification and diff whitespace checks passed.
- A temporary ordinary-permission host used the real App, composition root, MainWindow
  and isolated synthetic SQLite data. Native checks covered light 1280x820, dark 980x640,
  selecting the pinned conversation, opening/dismissing its menu and scrolling to recent
  tasks. Headless tests also covered sixteen pinned tasks, both themes and viewport sizes.
- The idle user application was then closed normally, rebuilt in the ordinary Debug
  output directory and restarted. Its actual process token was not elevated; the new
  sidebar was visible and `normal-app-after.png` contained a non-black 1920x1032 frame.

## Pixel evidence and remaining limits

Evidence and Python capture/comparison helpers stay outside the repository at:
`%TEMP%/MossAgent-sidebar-20261007`.

- `sidebar-comparison.png` contains native-size Codex, before and after sidebar crops;
  the comparison removes only window-frame offsets and does not resize the images.
- `verified/sidebar-light.png`, `verified/sidebar-scrolled.png` and
  `verified/sidebar-dark-narrow.png` show the new rendered component states.
- The activity snapshot is synthetic; this round did not send a paid provider request
  or revalidate a real streaming run's sidebar marker.
- Product names, project ordering and task contents differ between the applications.
  Project sorting/reordering, rail workflows and all Codex sidebar interactions are not
  claimed complete. This is a measured layout improvement, not pixel-identical acceptance
  or completion of the overall product goal.

## Project ordering and menu follow-up - 2026-10-07

### Design and behavior

- App owns the project-order projection and commands; Application supplies a narrow
  sidebar-preference port; Infrastructure owns file access. Views never read or write
  preferences directly. No tool authorization, provider protocol or database migration
  is involved. All publishing remains Windows x64 JIT single-file.
- The project-section menu no longer renames, archives or pins the selected conversation.
  It now offers real activity/manual sorting with right-aligned checked state. Project
  summaries have aligned shared icons for edit/up/down actions. End-of-list and stale
  move targets are rejected even when a command is invoked directly.
- Activity sorting includes pinned and search-hidden task snapshots. Manual moves persist
  stable project IDs, retain the catalog's source order, and reconcile added/removed
  projects. Sorting preserves selected and expanded row objects. Catalog refresh preserves
  expansion, search and the selected task, including a task hidden by the current search.
- Preferences use a separate `sidebar-settings.json`, ordinary runtime JSON metadata,
  serialized writes and atomic replacement. Invalid existing content is retained and
  blocks writes. Valid unknown keys survive updates. Cancellation before replacement
  leaves the previous file intact. A detected external change rejects replacement.
- Reads and saves have a ten-second cooperative deadline and lifetime cancellation.
  Noncooperative storage keeps command ownership until it returns. Disposal detaches
  source/project subscriptions and rejects late state changes. Errors are localized and
  do not echo raw exception data. Retry is explicit.
- A regression first demonstrated that waiting for slow sidebar preferences blocked
  workspace startup. Preferences now load independently; catalog/model loading does not
  wait for them. A failed preference read disables only ordering mutations.
- New C# files remain below 250 lines, have one type per file and no methods above the
  hard limit. The catalog delegates project-navigation loading rather than growing past
  500 lines. Shared controls/appearance variants retain the native-control boundary.

### Native evidence

- An ordinary-permission real App host with isolated synthetic SQLite data was checked
  at light 1280x820 and dark 980x640. GUI clicks selected manual sorting, moved a project
  up, closed the summary and retained the selected project/task. A new host restored the
  saved manual order; activity sorting restored the expected activity order.
- The selected marker initially appeared beside the label. A rendered regression caught
  the style-order conflict; the shared menu variant now aligns it at the trailing edge
  in both themes. Project move icons use the existing shared action layout.
- Python passive window capture produced non-black native-resolution evidence under
  `%TEMP%/MossAgent-sidebar-20261007/project-order-verified`. `sidebar-order-comparison.png`
  contains current Codex, pre-ordering MossAgent and updated MossAgent crops without
  resizing. `light-sort-menu.png`, `dark-project-menu.png`, `manual-reordered.png` and
  `restarted-manual.png` record the additional states. Helpers and synthetic data stay
  outside the repository and do not modify the user's database.
- The native running marker remains a synthetic snapshot. No paid model call or real
  streaming-marker chain was revalidated in this follow-up. Drag reordering, navigation
  rail workflows and complete pixel-identical acceptance remain outstanding.

### Follow-up validation results

- Focused sidebar/startup/action/MCP/proxy selection: 99 passed. Infrastructure sidebar
  preference selection: 11 passed. Both rendered menu theme cases passed after the
  shared trailing-alignment fix; the slow-preference startup regression failed before
  decoupling and passed afterward.
- Final full Debug solution builds in both the validation artifacts directory and the
  ordinary user Debug directory: zero warnings and errors.
- Final serial App regression: all 1998 passed. Agent 58, Browser 76, Infrastructure 92,
  Providers 241 and Tools 147 also passed. Platform's complete serial attempt had one
  PID-file sharing race in `ShellRunToolTests`; the entire Platform project subsequently
  passed all 50 tests. The latest seven project snapshots total 2662 passed, no skips;
  this is not claimed as a single all-green full-suite invocation. No unrelated process
  implementation or test was changed to hide the race.
- Earlier internally parallel collection runs also encountered a five-second provider
  initialization timeout and an HTTP MCP completion timeout. Focused repetitions passed,
  as did the final serial App run. The external runsettings used one test-collection
  thread as well as one MSBuild worker.
- Focused whitespace and diff checks passed after fixing one test object initializer.
  Roslyn audited 18 related C# files: one declaration per file, no file/method hard-limit
  violations. Changed AXAML files also remain below 500 lines.
- The idle user application was closed through its normal Close action, rebuilt and
  restarted from the normal Debug path. The actual process token was non-elevated.
  A fresh window binding exposed the new project sorting menu, and Python recorded
  non-black 1920x1032 `normal-app-after.png` and `normal-app-sort-menu.png` frames.
  No synthetic projects/tasks were injected into the user database, and no publish,
  commit or push was performed.

## Native project dragging follow-up - 2026-10-07

### Ownership and boundaries

- App's shared project row owns the six-pixel mouse threshold, native drag lifetime,
  drag opacity, two-pixel themed insertion line and summary dismissal. The business
  view binds scope, command and availability; it supplies no drag geometry or colors.
- Payloads use Avalonia's in-process data format, not external text, files or a
  process-wide mutable registry. Commands check the owning sidebar scope and current
  source/target object identities, rejecting replaced objects even when IDs match.
- A same-row command probe permits drag startup but execution is a no-op. The row
  never accepts a self-drop. Unchanged adjacent placement does not write preferences
  or silently switch the selected sort mode.
- Real before/after drops reuse the sidebar-preference port and switch to manual
  ordering only after storage succeeds. Selection, expansion and source catalog order
  are preserved. Failures keep the previous order and show a safe retry message.
- Guarded generic async commands reject reentry before entering the toolkit command,
  preventing a rejected second drop/move from cancelling the save that already owns
  the editor. The existing ten-second deadline and lifetime/explicit cancellation
  remain effective; noncooperative storage retains ownership until it returns.
- The shared viewport responds immediately in its 32-pixel edge band, then scrolls
  every 16 ms, capped at eight pixels per tick and clamped to the content extent.
  Leaving a child inside the viewport does not stop scrolling; payload invalidation,
  a real viewport exit, drop, scope replacement and detachment do. AllowDrop and event
  subscriptions are restored/removed on detach. Native drag cannot be forcibly
  cancelled through a token; its invalidated payload rejects late drops and errors.
- No database migration, tool-authorization change, provider request or new file
  access in UI code is involved. Debugging and publishing remain ordinary JIT.

### Native evidence and limits

- Native-size comparison exposed two layout errors independent of drag behavior:
  pinned task text was four pixels too low, and closed project groups accumulated
  one extra pixel per row (the final reference project was at y=750 versus y=759).
  Shared variants now use a 31-pixel closed-group stride, preserve expanded-group
  separation, and correct pinned/project/recent heading gaps. The same final label
  is at y=750, with sampled project text bands within half a pixel of the reference.
  These are measured text-band positions, not a claim that fonts/icons are identical.
- An ordinary-permission real App host with isolated synthetic SQLite data accepted
  a mouse drag of `know` before `MossAgent`, wrote manual order and restored it after
  restarting. The selected conversation and project context were retained.
- Dark 980x640 edge dragging initially produced no offset: the available native
  automation releases immediately, before a delayed timer tick. Immediate edge
  response now produces a recorded `QA_OFFSET: 0, 8` before drop. Timer continuation,
  clamping and stopped-state lifetime are separately covered by headless tests.
- The native project menu displays manual sorting after drag and restores activity
  sorting through its real command. Native captures cover light 1280x820, dark
  980x640, reordered and restarted states. Python rejects black/uniform frames and
  compares native sidebar crops without resizing; helpers and evidence remain under
  `%TEMP%/MossAgent-sidebar-20261007/drag-verified` and `drag-edge-verified`.
- Headless coverage includes both insertion directions, same/foreign/stale targets,
  disabled ancestors, missing commands, external text, expired payloads, thresholded
  click preservation, failure sanitization, cancellation/disposal and rejected reentry.
- Native Escape during a held drag, sustained mouse-dwell scrolling and cross-window
  rejection have not been manually verified. No paid model request or live running
  marker chain was exercised. Rail workflows and full pixel-identical acceptance
  remain incomplete; the overall product goal remains active.

### Final follow-up validation

- Final full solution builds in both the validation artifacts directory and ordinary
  Debug directory passed with zero warnings/errors. Focused shared-component and
  sidebar checks covered drag/reorder behavior; the final App run also includes the
  two new twelve-project rhythm cases and corrected header-style contract.
- One final serial solution test invocation passed all seven projects: App 2039,
  Agent 58, Browser 76, Infrastructure 92, Platform 50, Providers 241 and Tools 147.
  Total: 2703 passed, zero failed and zero skipped. TRX files are under
  `%TEMP%/MossAgent-sidebar-20261007/drag-tests/confirmed-final`.
- Focused whitespace and diff checks passed. Roslyn audited 14 related C# files:
  one declaration per file and no file/method hard-limit violations. Production
  files in the audit remain below 250 lines; the largest test file has 265 lines.
- `drag-edge-verified/sidebar-drag-comparison.png` shows Codex, the pre-drag baseline
  and final layout as native-size crops. `light-final.png` and `dark-final.png`
  contain the final shared spacing; native drag/menu/restart captures remain in
  the two evidence directories. The reference was captured passively, without
  automating Codex UI or resizing screenshots to hide differences.
- The idle, empty-draft user application was closed through its normal Close action,
  rebuilt and restarted from the ordinary Debug path. Its actual process token was
  not elevated, and passive Python capture returned a non-black 1920x1032 frame.
  All synthetic QA hosts were normally closed. User data was not seeded with test
  projects/tasks; no AOT, publish, staging, commit or push was performed.

## Scrollbar geometry follow-up - 2026-10-08

### Design and reproduced cause

- App's shared navigation styles own this change. Existing `AppScrollViewer`
  and framework scroll actions are reused; no new port, persistence migration,
  authorization, approval, cancellation or output policy is needed.
- Passive 96-DPI screenshots found Codex's resting thumb at client x307..312,
  six pixels wide, RGB (231,233,235). The previous MossAgent thumb appeared at
  x303..304, two pixels wide, RGB (242,245,247), despite a four-DIP inner Border.
- The Fluent parent Thumb retained its resting panning transform, including
  scale 0.125 and translation -2. Testing only the inner width missed the actual
  rendered geometry. The new tests failed four cases against that baseline.
- The sidebar-only variant removes the parent transform and transitions, uses a
  six-DIP/three-DIP-radius visual inside a twelve-DIP hit area, and adds dedicated
  resting/hover theme brushes. Transparent track chrome and hidden line-arrow
  buttons do not disable the existing page buttons, wheel or thumb dragging.
- Extending the viewport nine DIP and adding matching content padding positions
  the visual three DIP inside the sidebar's right edge without reducing content
  width or changing row placement. Other scroll viewers and separator brushes
  are unchanged. No handwritten production type was added; the new test file
  has one type and remains below the file/method limits. Packaging stays JIT.

### Native evidence and limits

- An ordinary-permission real App host with isolated synthetic SQLite data was
  rebuilt after the final one-pixel alignment correction. Python captured native
  light 1280x820 and dark 980x640 frames; helpers and test data remain outside
  the repository in `%TEMP%/MossAgent-sidebar-20261007/scroll-final-20261008`.
- `sidebar-scroll-comparison.png` compares current Codex, the previous search
  baseline and final MossAgent. Only the Codex window's (8,8) frame offset is
  removed; no screenshot is resized. Both reference and final resting thumbs
  measure x307..312 and RGB (231,233,235) at the same client-coordinate sample.
- Native light thumb dragging reached offset 488; transparent-track page-up
  returned to zero and wheel input reached 250. Dark dragging reached 668,
  page-up reached 193 and wheel-up returned to zero. The selected pinned
  conversation and project context remained unchanged in these captures.
- Dark and hover/pressed brushes are theme-consistent but have no matched Codex
  state reference. Only the resting light thumb has the direct pixel measurement.
  Thumb length depends on differing task counts/window heights, not a fixed
  copied size. Complete arrow/rail/icon typography and all sidebar interactions
  are not claimed pixel-identical; the overall product goal remains active.
- Native held-press appearance and sustained project-drag dwell still rely on
  deterministic headless tests rather than a held-input native capture. No paid
  model request or real running-marker chain was exercised in this follow-up.
- A native project drop after the viewport change reordered the synthetic project
  list while retaining the pinned conversation and project context. That capture
  did not establish an incremental edge-scroll offset; timer/edge lifetime remains
  covered by the existing deterministic tests, not a stronger native claim.

### Validation

- Final focused selection: 31 passed, zero failed/skipped, including ten new
  theme-paired scrollbar cases. Tests cover actual transformed bounds, resting
  and hover/pressed colors, transparent track, hidden line arrows, retained
  page/wheel/drag input, disabled drag rejection, fitting content, unchanged row
  widths, real sidebar placement and existing project edge-scroll lifetime.
- An earlier focused invocation hit an Avalonia headless-session disposal
  `NullReferenceException` in an existing drag-lifetime test. Its unchanged
  eleven-case class rerun and the complete 31-case focused rerun both passed;
  no production or test behavior was weakened to hide that transient failure.
- Full validation-directory solution build passed with zero warnings/errors.
- The idle, empty-draft user application was normally closed, rebuilt from its
  ordinary Debug output and restarted. The actual process token was non-elevated.
  Fresh accessibility state retained its project, empty composer, unselected model
  and disabled Send action; input focus was restored. Python captured a non-black
  1920x1032 `normal-current.png`. Synthetic QA exited normally with code zero.
  No user data was seeded, and no publish, staging, commit or push was performed.
- The first complete serial solution invocation passed 2743/2744, with one
  existing project-row layout case failing in Avalonia session disposal with the
  same `NullReferenceException`. Its unchanged twelve-case class rerun passed.
  A subsequent single complete serial solution invocation passed every project:
  App 2080, Agent 58, Browser 76, Infrastructure 92, Platform 50, Providers 241
  and Tools 147; total 2744 passed, zero failures/skips. Final TRX evidence is in
  `%TEMP%/MossAgent-sidebar-20261007/scroll-tests/confirmed-rerun`. These are
  validation results, not a fix for the framework teardown's intermittent failure.
- Both validation-directory and ordinary Debug solution builds passed with zero
  warnings/errors. Focused whitespace checks passed, and Roslyn audited both
  related C# test files with one declaration each and zero hard-limit violations.
  Shared AXAML files remain within the 500-line limit. Nothing was staged.

## 2026-10-08 navigation rail follow-up

The rail is now a guarded `AppNavigationRail` rather than repeated MainWindow
button markup. Home ink size/position, light selection color and settings' bottom
inset are measured against native Codex pixels. Running-task animation is scoped
to a dedicated icon theme to avoid overwriting unrelated icon transforms.
Component lifecycle, unavailable actions, negative markup boundaries, raw image
comparisons and complete regression evidence are recorded in
`NAVIGATION_RAIL_COMPONENT_ACCEPTANCE.md`. This is not whole-sidebar pixel parity.

## 2026-10-08 project-row follow-up

Project rows now close their summary on ownership changes, unavailable ancestors
and command rejection, with subscriptions released on detachment. Dedicated
folder geometry and shared optical variants match the measured native open/closed
ink bounds while retaining the label's X position and independent action targets.
Twenty lifecycle cases reproduce sixteen failures before the fix. Final native
light/dark inputs, three-column unscaled screenshot evidence, regression results
and remaining text/heading/DPI differences are documented in
`PROJECT_ROW_COMPONENT_ACCEPTANCE.md`. Matching icon bounds is not whole-sidebar
pixel equality, and no synthetic fixture data was added to the user's workspace.

## 2026-10-08 pinned-heading rhythm follow-up

The pinned heading's four-pixel vertical difference is now fixed by shared group
spacing rather than a text transform across the viewport clip. Pinned tasks and
project anchors remain unchanged; the no-pinned-task layout retains its previous
insets. Native unpin/re-pin menu actions and twelve complete-window theme/width
cases verify the existing data binding and command path. Exact raw-pixel metrics,
intermediate-image exclusions and remaining typography/DPI limits are recorded in
`SIDEBAR_HEADING_ACCEPTANCE.md`; this is not whole-sidebar pixel equality.

## 2026-10-08 task text and conditional fading follow-up

Task text now uses shared 14-DIP size and 18-DIP line height. The opt-in shared
text wrapper tracks actual single-line overflow, so fitting short titles retain
their complete trailing glyphs while long titles still clip/fade beside actions.
Measured light-theme body/group colors are updated without replacing global
font preferences. Theme-paired layout/lifetime tests, unscaled Python comparisons
and remaining baseline/font/DPI differences are in `SIDEBAR_TASK_TEXT_ACCEPTANCE.md`.
This is not whole-sidebar pixel equality.
