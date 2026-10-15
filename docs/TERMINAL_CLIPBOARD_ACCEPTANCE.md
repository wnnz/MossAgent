# Terminal selection and clipboard integration

## Design self-check

1. App owns selection of immutable rendered cells and explicit user clipboard
   gestures, using Avalonia's existing clipboard API, as file preview does. No
   Windows APIs, filesystem operations or model-readable clipboard port are added.
2. Process input reuses TerminalInputController and terminal.write through the
   existing executor, session/path validation, approval and metadata-only audit.
3. Reads occur only after an explicit paste gesture. Copy/paste retain an owner
   until the clipboard operation returns; cancellation, a 30-second deadline,
   visibility/context changes and stale sessions prevent subsequent input. Paste
   has a 32,768-unit encoded limit, rejects control-sequence injection and requires
   explicit confirmation for newline-containing text. Uncertain writes never replay.
4. Selection, clipboard commands and paste encoding are separate single-type
   files. Shared controls/variants own hit geometry, menus and selection brushes.
5. Tests cover Unicode/wide cells, wraps/history, snapshot changes, bounded text,
   cancellation/late operations, stale sessions, ReadOnly/refusal, real ConPTY input
   and native light/dark wide/narrow render checks. These are separate from complete
   Codex workspace and physical keyboard/IME acceptance.

## Acceptance status

Selection and clipboard integration are implemented. The overall goal remains active;
this does not establish complete Codex workspace or terminal fidelity.

## Implemented behavior

- Drag selection, Ctrl+Shift+A and Esc use a shared rendered-cell selection model.
  Wide-cell continuations copy their complete glyph exactly once; graphemes and
  supplementary Unicode are not split. Hidden text is not extracted. Copy joins
  soft wraps, retains hard breaks and trims terminal padding. Selected-row mutation,
  task/session/view, alternate-buffer or geometry replacement clears stale selection.
  It never rereads a file or another task's screen. The extraction limit is 32,768 units.
- Ctrl+Shift+C/V and the shared toolbar menu have real copy, paste and select-all
  commands. Ctrl+C still reaches the process; reserved clipboard shortcuts never
  fall through to control characters. Shared variants own selection color; one fill
  per selected row avoids fractional-cell seams seen in the first native capture.
  Hidden/detached/disabled controls reject gestures; scope changes close the menu.
- Clipboard read requires an explicit user gesture, not a model/tool callback. Copy
  takes only the selected immutable snapshot. Ownership, a 30-second deadline and
  visibility/scope/session/mode revalidation suppress late notices and input. Started
  OS clipboard operations cannot be undone and remain busy until return. Cancellation
  or invalidation clears staged paste, never replays it, and exposes only fixed labels.
- Paste rejects ESC/C0/C1/DEL controls other than tab/newlines and rejects malformed
  UTF-16. CRLF and CR normalize consistently. Newline-containing content is held only
  in bounded memory until explicit confirmation; a newline can execute commands when
  the consumer does not support bracketed paste. Confirmation is not a substitute for
  tool approval. Pending content is discarded on scope changes or cancellation.
- Admitted paste is exclusive in TerminalInputController. Typing/legacy Send cannot
  interleave, and paste cannot merge into an outstanding keyboard approval. The existing
  terminal.write tool revalidates session, paths, policy, cancellation and timeout.
  Audit/progress are metadata-only, not clipboard payload logs. Refused/uncertain writes
  require fresh input rather than automatic replay. Encoded input, including envelope
  and Win32 record overhead, stays within 32,768 UTF-16 units.
- Win32 paste emits paired press/release records, including bracketed envelopes.
  Real Console.ReadKey testing exposed duplicated consecutive characters with a
  key-down-only envelope; matching releases preserve the exact payload. Raw VT
  bracketed strings did not reach the Windows console consumer correctly and are
  not used when Win32 input is enabled. Ordinary keyboard encoding is unchanged.

## Verification (2026-10-09)

Evidence, passive Python scripts and the native test host remain outside the repository:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-terminal-clipboard-20261009`.

- Focused model/controller tests cover wide glyphs, combining/supplementary text,
  hidden content, history/soft wraps, changed snapshots, extraction/encoding limits,
  invalid/control text, ReadOnly/stale scope, refusal/no replay, exclusive writes,
  cancelled/noncooperative reads and same-session scope/mode changes.
- Headless tests route real drag/clipboard shortcuts against its isolated clipboard,
  validate themed selection brushes, actual shared-menu bindings/disabled states,
  multiline staging, ancestor hide/show cancellation and menu dismissal on scope change.
- Real ConPTY tests use the actual tool pipeline: bracketed Unicode/repeated-character
  and normalized newline payloads reach a Windows console consumer exactly; plain
  Unicode/repeated-character paste works in PSReadLine and does not run before Enter.
  These tests do not claim PSReadLine supports an artificially enabled bracketed mode.
  Initial artificial-mode tests and raw-VT experiments failed and remain in the evidence;
  the final consumer and normal-shell checks validate the supported encoding instead.
- Native `accepted-Light-470`, `accepted-Dark-470`, `accepted-Light-280` and
  `accepted-Dark-280` capture Render, Keys, Selection and Menu. Hosts are non-elevated,
  use scale 1, real ConPTY/tool execution and the shared pane, and exit 0 with empty
  stderr. Initial/applied grids settle at 64x26 or 35x26 with no pending/failure notices.
  Programmatic control events/selection are not physical input or IME acceptance.
  Main captures validate PID/title/native bounds and reject black pixels. Popup capture
  additionally checks exact owner and foreground before/after reading its window rectangle.
  Earlier PrintWindow-only popup composites show black transparent corners; the accepted
  on-screen menu pixels preserve the actual Windows rounding without painting a mask.
- `terminal-release-check`: 229/229 terminal tests pass; the final ownership check
  additionally passes 12/12, including clearing old-session selections and late waiting
  notices. `full-accepted`: seven-project default parallel regression passes 3660/3660,
  zero failed/skipped (App 2777, Windows 129,
  Providers 283, Tools 188, Browser 76, Infrastructure 144, Agent 63). TRX aggregation
  uses literal paths, including filenames with brackets. The solution build reports
  zero warnings/errors. Scoped whitespace, one-type/file, 500-line/80-line-method and
  XML checks pass; the largest changed source is 212 lines, maximum method 40 lines.
  These checks preserve the existing dirty checkout without staging any paths.
- `terminal-clipboard-comparison.png` is a crop/contact sheet of native iteration
  captures: the original fractional selection seams and corrected light/dark/narrow
  selected rows/menu. It is not a Codex side-by-side workspace comparison.

## Remaining boundaries

- These are component/integration checks, not a full Codex workspace comparison.
  Physical keyboard/IME, accessibility, non-100-percent DPI and live splitter dragging
  remain separate. Word/line selection, rectangular selection and drag auto-scroll are
  not added; history can be scrolled before selection.
  `TERMINAL_IME_ACCEPTANCE.md` records the later preedit-client/candidate-geometry
  integration; its programmatic evidence does not replace physical Windows IME checks.
- Clipboard extraction uses Avalonia's existing text API, which obtains OS text before
  the App's limit check; it is not a streaming OS-allocation limit. No clipboard history,
  image/file paste, model clipboard read or automatic clipboard inspection is introduced.
- A consumer that advertises bracketed paste must actually support it. The App cannot
  undo commands once accepted by a process or OS clipboard writes already started.
  Mouse reporting and VT query responses remain disconnected from process input.
- No commit, push, release, AOT configuration change or memory update is performed.
