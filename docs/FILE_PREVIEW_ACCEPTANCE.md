# Task-owned file previews and rendered diffs

## Scope (2026-10-08)

The inspector opens source attachments, a selected changed file and audited task
artifacts as read-only tabs. Tabs are selectable, closable, refreshable and bounded
to eight. The selected tab scrolls into view, including its close action, inside a
narrow inspector. Returning to the inspector retains tabs; changing tasks or their
authorization scope cancels pending reads and clears content.

UTF-8 text, PNG and baseline/progressive JPEG have real previews. File previews
show line numbers and existing theme-aware syntax tokens. Unified Git diffs show
old/new line numbers, addition/deletion backgrounds, prefixes and hunk headings.
Git envelope and line-ending warnings do not displace the actual changed lines.

## Design and safety self-check

1. App owns observable tabs and shared visual components. Tools.BuiltIn owns the
   preview tool, Application defines the task-artifact reader port, Infrastructure
   reads and verifies snapshots, and Tools.Abstractions owns bounded format rules.
   No lower module depends on UI. Window chrome actions are extracted to avoid
   extending the already large main window view model beyond its hard limit.
2. Original files use `filesystem.preview_file` through the existing tool executor,
   schema/preflight/capability/approval/timeout/cancellation/output/audit pipeline.
   Audited artifacts use the task-artifact reader after matching the selected task.
   The UI decodes returned memory, never a filesystem path.
3. Preflight canonicalizes the original path against task-authorized roots and
   rechecks it after reading. Snapshot creation rejects existing artifact-directory
   links before creating directories. Readers reject paths outside the task's
   artifact directory, links, changed lengths/hashes and mismatched media types.
   Text is capped at 128 KiB; all snapshots at 2 MiB. PNG/JPEG dimensions are capped
   at 8,192 per axis and eight million pixels. Reads request cancellation after
   30 seconds; noncooperative late completions cannot publish to closed/replaced tabs.
4. Handwritten C# remains one type per file, with scoped AST size checks. Shared
   styles own tab/action geometry, breadcrumbs, image fit, code gutters and diff
   colors. Markup and analyzer gates reject component composition/geometry replacement.
5. Focused tests cover pipeline schema/approval/cancellation, path rejection,
   unsupported/oversized data, safe dimensions, task ownership, changed snapshots,
   junction escape, late completions, equivalent context reattachment, tab limits,
   parser bounds, gutters, themes and component policy. No paid model is invoked.

## Native evidence

Evidence and Python scripts live outside the repository at
`%TEMP%\MossAgent-file-preview-20261008` and the existing automation QA directory.
The native QA host uses separate synthetic data with paused schedules.

- Passive Python captures inspect light 1280x820 text/diff views and dark 980x640
  PNG preview. Captures assert dimensions and reject black images.
- The first native capture exposed missing back/close icon resource names and an
  active image tab clipped outside the visible strip. Correct resources and owned
  bring-into-view behavior correct both, including the active close button.
- The diff screenshot uses real git.status/git.diff execution against a separate
  synthetic repository, rather than a hard-coded success result.
- Native Ctrl+P switches from preview to audited execution records without replacing
  the draft. Mouse input reports `coordinate input geometry is unavailable`; native
  mouse/hover actions are not accepted by this evidence.
- Final full-solution regression passes 2,925 tests: Agent 58, App 2,222, Browser 76,
  Infrastructure 124, Windows 50, Providers 241 and Tools 154. Final solution and
  ordinary Debug App builds have zero warnings/errors. The 26-file scoped AST audit
  has zero violations; scoped whitespace/diff checks pass. Logs are in the evidence
  root (`regression-final-2.log`, `ast-audit.log`).
- The ordinary, non-elevated managed/JIT application is normally closed while idle,
  rebuilt and restarted; its user's empty draft/data are not replaced by QA data.
  A foreground-only Python capture verifies the actual 1536x816 screen pixels.
  A later 1920x1032 QA request returns different native bounds and is rejected by
  the screenshot size guard. That wide capture is not fidelity evidence; window/DPI
  state must be remeasured rather than scaling or accepting a clipped frame.

## Remaining limits

This is not a claim of one-to-one Codex fidelity. The passive Codex reference has
an image-preview panel with a richer toolbar; matched-state Codex diff and automation
screenshots are still needed. External editor integration, split diff,
full language grammars and editing in preview are not implemented.
Unsupported formats fail safely. Rendering is capped at 2,000 lines and 128 KiB;
line selections are independent, not a full editor-wide selection model.

Snapshots retain their audited task artifacts. Preview refresh reads a fresh original
file; refreshing an audited artifact rechecks the immutable recorded length/hash.
No data migration, paid-provider acceptance, packaging, AOT or JSON-source-generation
change is part of this phase. Development remains ordinary managed/JIT.

## Search and image interaction (2026-10-08)

- Each tab owns a literal, ordinal-ignore-case search, bounded to 256 query
  characters and the same 2,000 lines/128 KiB as rendering. Counts identify
  matching lines, not individual occurrences. Enter/Shift+Enter wrap through
  matching lines and scroll the highlighted current row into view. Ctrl+F opens
  and focuses search while focus is inside the preview; Escape closes it.
- Switching tabs preserves their individual search state. Refresh, task/scope
  replacement and disposal clear it. Loading, image and disposed tabs cannot
  open search; no search action invokes a tool or reads another file.
- Image tabs own fit, actual-size and 10%-400% zoom state. The shared image
  viewport maintains aspect ratio, recomputes fit on resize and scrolls oversized
  images. A replaced image resets fit; loading/closed tabs disable zoom actions.
  Zoom starts from 100% when leaving fit mode, not an estimated fit percentage.
- App owns these presentation-only behaviors over the existing authorized
  snapshots; no port, permission, pipeline or persistence change is required.
  Handwritten types remain separate; new image composition is covered by the
  same markup/analyzer boundary. Focused tests cover literal/empty/no-match/bounded
  searches, wrapping, stale tabs, lifecycle, keyboard focus, row scrolling,
  narrow two-theme layouts, zoom limits and resize.
- Passive Python native captures are stored outside the repository in
  `%TEMP%\MossAgent-preview-interactions-20261008`: light 1280x820 search/editor
  and dark 980x640 image fit. A foreground screen-pixel capture confirms native
  Enter selects the second match and scrolls to line 68. UI Automation returns
  stale counts after input; pixels, not that stale tree, establish the outcome.
  A malformed shared refresh geometry seen in those captures is corrected.
- Native Shift+Enter, Escape and Ctrl+F are separately captured: navigation wraps
  back, Escape removes the search strip/highlights and Ctrl+F refocuses the retained
  query. Dark 980x640 zoom and final light 1280x820 fit captures verify actual
  scaled content and the corrected circular refresh icon. The final solution and
  ordinary JIT builds have zero warnings/errors. All 2,938 solution tests pass;
  after the final icon correction and stronger horizontal-scroll/source-reset
  assertions, all 30 focused tests pass again. The 14-file scoped AST audit reports
  zero violations. The ordinary idle app is normally closed and restarted without
  replacing user data; its actual foreground frame is 1280x820 at DPI 96.

This does not accept matching Codex automation states, native mouse/hover input,
external editor integration, whole-editor selection, occurrence-level highlights
or real paid-provider execution. Search is over the rendered snapshot, not a
workspace-wide filesystem search. Final fidelity still needs same-state references.

## File toolbar actions (2026-10-08)

App's shared preview component now provides snapshot copy, absolute/relative path
copy, a keyboard path-copy shortcut (Ctrl+Shift+C), find, refresh and a real
"show in File Explorer" action. Image tabs default to path copy and disable text
copy/search. The open menu states the actual operation; editor launching and file
association execution are not implied. Missing clipboard, loading, hidden,
disabled or detached owners disable copy. Task/tab/scope changes close menus and
reject late notices; pending noncooperative clipboard writes retain ownership.
Already-started clipboard writes cannot be revoked when the owner changes.

Design and safety self-check:

1. App owns clipboard/presentation over already-authorized snapshots.
   Tools.Abstractions owns the file-reveal port; Tools.BuiltIn owns the tool and
   authorization; Platform.Windows owns STA/COM Shell integration. No lower
   module references App, and App performs no filesystem/process operation.
2. Reveal uses `filesystem.reveal_file` through the existing tool executor, with
   external-side-effect risk and FileRead/Process capabilities. Read-only tasks
   and stored audit snapshots cannot reveal their original files.
3. Exact task/policy/roots and a 30-second cooperative deadline reach the pipeline.
   The path is revalidated before approval, after approval, on the owned worker
   and immediately before Shell selection. Traversal, missing/directory targets,
   escaping junctions and approval/worker-stage junction substitution are denied.
   Validation does not atomically pin the Windows namespace: an adversarial
   replacement between final validation and Shell resolution remains a TOCTOU
   limitation. Selection never executes the file or constructs a command line.
   Started Shell calls cannot be forcibly cancelled; ownership is retained until
   they return, then cancellation is propagated. Output/audit contain no file body.
4. One handwritten type per file and scoped AST size checks remain enforced.
   Shared variants own 34-pixel outer toolbar surfaces, centered icon/button
   geometry and borderless, leading-aligned menu items. The native capture caught
   unstyled bordered menu buttons and constrained pill heights; both are fixed.
5. Tests cover schema/capability/approval, cancellation/timeouts, safe errors,
   junction substitution, persisted audit/output bounds, clipboard snapshot/path
   payloads, actual menu bindings, read-only/image states, late completions and
   self/ancestor visibility/enablement/detachment. Two-theme layouts at 300/640
   pixels assert bounds, centerlines, icons and equal outer surface heights.

Native evidence lives outside the repository in
`%TEMP%\MossAgent-file-toolbar-20261008`. Light 1280x820 and dark 980x640 captures
check the real toolbar without scaling. Native keyboard input copies a snapshot
and opens the copy menu. A separate synthetic FullAccess task's open button runs
the real pipeline and Shell API: File Explorer selects PreviewSample.cs and its
SQLite audit records approved/successful completion. Only that newly opened
Explorer window is closed. No paid provider or real user file is used.

The passive Python selector initially picked Explorer's desktop shell surface.
It now excludes Progman/WorkerW and supports an exact title with uniqueness
checking; the corrected `project` capture verifies the selected file. The desktop
capture and the first popup capture (dismissed by activation) are not acceptance
evidence. Mouse automation still reports unavailable coordinate geometry; keyboard
and raw Python pixels, not stale UIA focus/counts, establish the native outcome.

The final full solution regression passes 2,977 tests (Agent 58, App 2,259, Browser 76,
Infrastructure 124, Windows 54, Providers 241, Tools 165). All 49 focused preview,
interaction and automation-preview tests pass after the toolbar/menu corrections.
The accepted light toolbar/menu screenshots verify borderless leading-aligned
items; native keyboard selection copies the path and dismisses the popup.
The ordinary managed/JIT App is rebuilt with zero warnings/errors and restarted
only after observing its idle, empty draft; user data is not replaced by QA data.
The 19-file final AST audit has zero
violations. Development remains ordinary managed/JIT; no packaging, AOT,
serialization or database schema changes are introduced in this phase. Matched
Codex automation/editor screenshots, native hover/mouse verification and external
editor integration remain outstanding. This is not a one-to-one fidelity claim.
