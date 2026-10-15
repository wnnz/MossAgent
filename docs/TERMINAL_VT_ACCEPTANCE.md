# Terminal VT rendering acceptance

## Design and ownership

1. App owns a bounded, in-memory VT projection and shared character-grid controls.
   Only the MIT-licensed RoyalTerminal managed VT core (0.6.2) is referenced;
   the RoyalTerminal Avalonia/backend packages are not used. Windows keeps ConPTY.
2. Output and metadata reuse the existing terminal ports. Output gains an optional
   SessionId. Process mutations still require the existing tool execution pipeline.
   No terminal response, clipboard, hyperlink, file/image, title or notification
   callback is connected to the host. This phase does not add keyboard input.
3. Only explicitly attached task scopes are admitted. Scope changes discard cached
   output and block the old session. Late sessions/revisions and unknown tasks are
   rejected. Output is parsed in order on the producer thread with bounded parser
   state, before the existing diagnostic text buffer can truncate it. A single
   background projection worker publishes coalesced immutable frames to the UI.
4. Responsibilities are split into frame/cell values, streaming control-string
   boundary, per-session VT screen, bounded task store, projection and shared
   rendering/scroll controls. Handwritten files retain one type each and the
   repository file/method limits.
5. Tests must cover CR/erase, split CSI, SGR, Unicode/wide cells, alternate screen,
   scrolling, immutable snapshots and task/session isolation. Negative cases cover
   opaque host/graphics strings, scope changes, stale/unregistered output, bounded
   history/cache, disposal and appearance override gates. Native pixels and real
   ConPTY output are required separately from headless layout tests.

## Phase scope

- Render the existing 120-by-30 ConPTY grid without pretending that viewport
  dimensions have resized the child process. Use real horizontal/vertical scrolling.
- Retain at most eight admitted task screens and 128 history rows per screen.
  Glyph drawing caches are per-control and bounded independently.
- Implement follow-output and per-screen scroll restoration, with alternate-screen
  history isolated from primary-screen history.
- Keep current approved command-line input. Automatic child resize, interactive
  keys, bracketed paste, selections/copy and VT response routing remain separate
  work and must not be described as a complete interactive terminal.

## Verification (2026-10-09)

Evidence is outside the repository at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-terminal-vt-20261009`.

- Forty new cases cover the managed grid, streaming boundary, bounded history and
  graphemes, scope/session/cache ownership, immutable frames, dispatcher coalescing,
  source-to-view-model projection, glyph-cache bounds, layout and appearance gates.
- Real ConPTY output flows through ToolExecutor start/write/stop into colored Unicode
  cells. Restart creates a new session/view without old content. ReadOnly policy
  cannot start a shell or write to an existing one. A transient Windows directory
  release race was reproduced during test cleanup and fixed with bounded retries.
- `terminal-final`: 106 terminal tests passed; `app-accepted`: 202 relevant tests
  passed. `full-accepted`: default parallel full
  regression, 3514/3514 passed, zero failed or skipped (App 2654, Windows 106,
  Providers 283, Agent 63, Tools 188, Browser 76, Infrastructure 144).
- `accepted-build.log`: solution build, zero warnings/errors. Scoped whitespace,
  Roslyn one-type/file and size checks, XML checks and `git diff --check` passed.
  Only the managed RoyalTerminal 0.6.2 core and its managed dependencies were added;
  Avalonia remains 12.1.2. JIT debugging and Windows-x64 single-file publishing remain.
- Ordinary-permission native hosts (`QA_ELEVATED False`, scale 1) were captured with
  the existing repository-external passive Python script, exact PID/title/size checks
  and non-black validation. `final-Light-470`, `final-Dark-470`, `final-Light-280`
  each cover render, history, alternate screen and restoration; all hosts exited 0
  with empty stderr. Visual hosts use synthetic output only; real process behavior
  is verified independently by the integration tests.
- Native screenshots exposed the horizontal scrollbar overlapping the last row.
  The shared terminal scroll variant now reserves space, hides native arrow buttons
  and uses the same shallow thumb treatment as the vertical scrollbar. Headless
  regression covers the reserved gutter, measured cells, initial prompt visibility,
  scroll restoration and both themes at 280/470 DIP widths.
- `terminal-vt-comparison.png` juxtaposes the prior plain-text renderer and the new
  light/dark cell renderer using the same synthetic fixture. It was visually reviewed.
  This is a MossAgent before/after comparison, not a Codex-workspace fidelity claim.

## Remaining boundaries

- This document records the historical fixed-grid phase. Variable child/grid
  dimensions, automatic resize, keyboard routing, Win32 input and explicit UTF-8
  startup are implemented in the subsequent `TERMINAL_INTERACTION_ACCEPTANCE.md`;
  its results, not the baseline counts above, apply to the current integration.
- Clipboard/bracketed-paste integration, VT query responses, selection/copy, cursor
  blinking, screen-reader content and physical IME behavior still need acceptance.
  Approved command-line input remains available beside direct keyboard input.
- OSC/DCS/APC/PM/SOS host/image/hyperlink protocols are deliberately not enabled.
  No process-controlled terminal callback can read files, access the clipboard,
  open links or create a second process-write path.
- Full-workspace Codex side-by-side interaction and non-100-percent DPI checks remain
  unverified. No commit, push, package release or memory update was performed.
