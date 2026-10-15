# Terminal size and input integration

## Design self-check

1. Tools.Abstractions owns immutable grid metadata and optional session-bound control
   contracts. Windows owns process mutations; App owns measurement, coalescing and
   keyboard encoding. No platform APIs enter the VT model or business views.
2. Resize and input reuse terminal tools and ToolExecutor. Optional session tags
   prevent an approval granted for an old process from reaching its replacement.
   Snapshot dimensions reflect successfully applied ConPTY changes, not UI intent.
3. Resize is side-effecting and must not bypass ReadOnly/AskEveryTime. Captured task
   scopes, cancellation, deadlines and session revalidation apply to queued work.
   No terminal query callback writes directly to the process. Queues/caches stay bounded.
4. Geometry, session mutation, operation ownership and keyboard encoding remain
   separately named types. Files retain one handwritten type and size limits.
5. Focused tests must prove grid preservation/reflow, resize failure and stale-session
   rejection, policy refusal, request coalescing, cancellation/late completion and
   real process geometry/input; headless/native layout and full regression are separate.

## Acceptance status

The size/input integration is implemented. The overall MossAgent goal remains
active; this is not a claim of complete Codex workspace or terminal fidelity.

## Implemented behavior

- Immutable applied grid metadata supports 20-500 columns and 5-200 rows. Windows
  publishes only successfully applied dimensions; failures leave metadata intact.
  Same-session state transitions retain geometry. Native resize, metadata commits
  and stop cleanup are serialized per owner. Cancelled requests waiting for another
  resize cannot mutate the child; started calls retain ownership until return.
- Optional session tags reject replaced processes and mismatched canonical working
  directories. Both write and resize use path prevalidation before approval and
  revalidate roots when execution begins. Resize requires process capability and
  side-effect approval, including ReadOnly/AskEveryTime refusal paths.
- VT resize uses the managed processor's primary/alternate resize operation,
  preserving screen identity and content. History adapts under a 100,000-cell
  per-buffer ceiling, with a 400,000-cell aggregate owner budget and eight-task cap.
- The shared viewport measures real cells/scroller space, clamps to supported
  geometry and coalesces requests with a 150-ms delay. Running requests retain their
  owner; newer measurements produce at most one follow-up. Refusal/failure stops
  automatic retries until an explicit retry. ReadOnly, missing-grid, cancelled or
  mismatched scopes do not automatically resize.
- Focused text and Enter/Backspace/Tab/arrows/Home/End/Insert/Delete/Page/F-keys and
  control shortcuts flow through `terminal.write`, not a manager callback. The frame
  selects classic/application cursor or Win32 key-record encoding. Unicode Win32
  input preserves UTF-16 ordering; printable/AltGr keys are left to TextInput routing.
  Ctrl+Shift+C/V are reserved, not converted to destructive Ctrl-C/V behavior.
- Input bursts coalesce for 15 ms and serialize through task-owned tool operations.
  Pending plus in-flight encoded input is capped at 32,768 UTF-16 units per owner;
  at most eight non-returned owners are retained. Refusal, cancellation, overflow or
  session/scope change discards pending input and exposes a localized safe notice.
  Explicit resume accepts new input only, never replays an uncertain write.
  Noncooperative operations keep same-task ownership; returning to the task displays
  a waiting notice until cleanup returns. Both coordinators use 30-second deadlines.
- The legacy command box remains functional and sends a captured session tag when
  metadata is available. Direct keyboard writes prevent interleaving its Send action.
  The shared 28-DIP toolbar height remains unchanged when cancel icons appear; the
  native/Headless measurement feedback loop reproduced during development is fixed.
- PowerShell initializes console input/output and `$OutputEncoding` to UTF-8 without
  loading user profiles. Passive native captures exposed system-code-page replacement
  of combining marks/emoji; a real-process regression and fresh pixels confirm the fix.

## Verification (2026-10-09)

Evidence and the passive Python scripts/native host remain outside the repository at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-terminal-interaction-20261009`.

- Focused tests cover state/grid preservation, extreme geometry and cache budgets,
  coalescing, explicit retry, refusal, stale approvals, revoked roots, session/scope
  replacement, cancelled waiters, deadlines and noncooperative cleanup ownership.
- Seven real ConPTY tests verify applied console geometry, Win32 Up/Ctrl-Left/Enter
  records, Unicode text with Left/Backspace editing in PSReadLine, Ctrl-C cancelling
  a running command without replacing the shell, and combining/supplementary output.
  These use actual tools/processes, not paid providers or mocked terminal output.
- Headless routing/layout checks cover both themes and 280/470-DIP panes, shared
  focus and command binding, text-vs-key routing, hidden controls, stable automatic
  geometry and existing appearance/composition gates. Headless fallback cells can
  exceed available width at the 20-column minimum; horizontal scrolling remains
  intentional there, rather than silently shrinking the supported child dimensions.
- Native `final-Light-470`, `final-Dark-470`, `final-Light-280` and
  `final-Dark-280` cover Render, Keys and Scroll (twelve passive captures total).
  Each host is non-elevated, scale 1, starts a real ConPTY through the tool executor
  and routes programmatic control events through the real shared viewport/controller.
  Capture verifies exact PID, title and window bounds and rejects black pixels.
  All hosts exit 0 with empty stderr. Settled captures report no busy/failure notices;
  wide panes use 64x26 initially/62x26 with history, narrow panes 35x26/33x26.
- `terminal-interaction-comparison.png` compares the live code-page/UTF-8 captures
  with a narrow dark pane. It is a MossAgent iteration comparison, not a Codex
  side-by-side screenshot or a claim of pixel-identical workspaces.
- A repeat full run exposed a measurement test sampling the first idle frame before
  scrollbar/grid catch-up finished. It now requires a bounded stable interval with
  metadata/frame agreement. The controller also skips requests already applied by
  the authoritative state source, avoiding redundant approvals when UI metadata lags.
  The same run had an unrelated sidebar test directory-release IOException; no
  sidebar or cleanup behavior was changed in this slice.
- `terminal-final`: 178/178 terminal cases pass; `settling-regression.log`: 15/15
  resize/routing/layout cases pass; `resize-ownership.log`: 21/21 Windows control,
  timeout/ownership and spawn cases pass. These supplement the full regression.
- `full-final`: default parallel full regression passes 3609/3609 with zero failed
  or skipped (App 2726, Windows 129, Providers 283, Tools 188, Browser 76,
  Infrastructure 144, Agent 63). All seven TRX files, including bracketed names,
  were aggregated with literal-path reads. The sidebar cleanup case passes on this
  final run; the earlier transient remains documented rather than patched here.
- `accepted-build.log`: solution build has zero warnings/errors. `format-final.log`
  verifies scoped whitespace; `source-audit.log` verifies one type per handwritten
  file, 500-line/80-line-method limits and shared-control XML. Normal scoped and
  whole-worktree `git diff --check` pass. Ordinary debugging remains JIT and release
  remains Windows-x64 single-file; no packaging or AOT configuration is changed.

## Remaining boundaries

- Selection/copy and explicit user clipboard paste are now implemented separately;
  see `TERMINAL_CLIPBOARD_ACCEPTANCE.md` for current safety and native evidence.
  Mouse reporting and VT query responses remain disconnected. No host or VT response
  callback can bypass tools. This document's size/input results remain their own baseline.
- Native event-routing tests are programmatic, not proof of physical keyboard,
  international layout, IME preedit/candidate positioning or accessibility behavior.
  Cursor blink, physical IME, non-100-percent DPI and screen-reader acceptance remain.
  The shared preedit client and candidate-geometry integration are now implemented;
  see `TERMINAL_IME_ACCEPTANCE.md` for their separate tests and native captures. That
  evidence does not establish physical Windows IME or actual OS candidate-popup behavior.
- Fixed-size native component hosts prove rendered integration, not the complete
  Codex workspace or dragging a live inspector splitter; that comparison remains.
- Legacy third-party terminal managers without metadata/session-control support do
  not receive automatic resize or direct keyboard input. Tagged tool requests fail
  explicitly instead of silently downgrading session ownership checks.
- No commit, push, package release, AOT change or memory update is performed.
