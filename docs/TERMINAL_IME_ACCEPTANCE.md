# Terminal input-method integration

## Design and safety self-check

1. App owns IME preedit presentation and candidate geometry in the shared terminal
   viewport. Platform-neutral domain/contracts and Windows ConPTY remain unchanged.
2. Avalonia's existing TextInputMethodClient provides composition; only committed
   TextInput continues into TerminalInputController and terminal.write. No second
   process-input path, provider, file or clipboard access is introduced.
3. Preedit stays bounded and in memory, is never sent to a process, stored or audited.
   Missing/rejected commands and unavailable scopes cannot acquire an input client.
   Scope/session/command changes, hiding, disabling, focus loss and detachment reset
   composition and invalidate old clients. Existing tool approval, path/session
   validation, cancellation, deadline and input-output budgets remain authoritative.
4. The input-method adapter is a separately named single type. The existing shared
   viewport owns its overlay; shared styles own text appearance. No business-page
   dimensions, colors, native visual controls or mutable global state are added.
5. Focused tests must cover acquisition, preedit without writes, one committed
   request, accurate/clamped scrolling geometry, live frames, rejected/stale clients,
   bounded input and retained native keyboard/clipboard behavior. Native captures
   and physical Windows IME behavior are separate acceptance evidence.

## Implemented behavior

- One task-owned Avalonia input-method client presents an underlined, shared-style
  overlay. Preedit is limited to 1,024 UTF-16 units and rejects control characters.
  Caret clamping does not split surrogate pairs. Surrounding terminal text is never
  offered to the input method. This adds no file, clipboard or provider access.
- While composing, terminal key encoding does not send Enter, Backspace, Escape or
  arrows to the child process. Only committed TextInput follows the existing
  TerminalInputController and terminal.write pipeline, including its approval,
  cancellation, session validation and bounded-input behavior.
- Client identity captures task, view, session, alternate screen, scope generation
  and input command. Lost focus, detachment, hidden/disabled ancestors, failed frames
  or command rejection clear preedit and permanently invalidate late callbacks.
  Restored eligibility requests a fresh client rather than reviving an old one.
- Candidate rectangles follow the real cell/scroller geometry, remain within the
  visible viewport, and update when frames, scrolling or window size change.
  Screen/scroller references are resolved once after markup loading; each geometry
  update shares one visible-area measurement, and redundant restoration refreshes
  are removed. This is reduced repeated work, not a measured performance claim.

## Verification (2026-10-10)

Evidence, the passive Python capture script and the native QA host remain outside
the repository at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-terminal-ime-20261010`.
No QA code or screenshots are added to application source or release packaging.

- The initial regression reproduces the missing input-method client: ten failures
  and one pass in `before/before.trx`. The first implementation also exposed a native
  Canvas violating shared-component gates, an incorrect requery event argument,
  cross-thread shared text-decoration state and inaccurate focus/detach test setup.
  These are corrected without relaxing visual-component or execution boundaries.
- Headless checks cover both themes, narrow/wide panes, composition without writes,
  exactly one committed request, ownership/eligibility changes, invalid input,
  scrolling/new frames and narrowing/widening while retaining the current client.
- Three real ConPTY tests route programmatic IME/TextInput events through the actual
  viewport/controller/tool executor. FullAccess commits `你好🚀`; AskEveryTime refusal
  and ReadOnly prove no admitted write. These do not invoke a physical Windows IME.
- Initial non-elevated native hosts capture the real App/MainWindow with synthetic
  tasks and an unreachable synthetic provider: dark 980x640 and light 1920x1032.
  Passive captures show the normal terminal, underlined `zhong wen 中文` preedit,
  and clearing it. Both candidate rectangles stay inside the viewport; both hosts
  stop their real ConPTY and close normally with empty stderr.
- Read-only checks of those isolated databases find no terminal.write calls, no
  conversation messages and no unfinished audit records. They contain only terminal
  start, resize and stop. The native preedit is a local programmatic client callback,
  not a keyboard command, provider request or physical candidate-window test.

- The QA host is rebuilt after the final cached-control/layout refactor.
  `final-dark-preedit.png`/`final-dark-cleared.png` cover 980x640 dark mode;
  `final-light-preedit.png`/`final-light-cleared.png` cover 1920x1032 light mode.
  Underlined preedit and clearing render correctly, candidate metadata remains
  viewport-clamped, and both fresh hosts stop the terminal and close normally.
  All four hosts have empty stderr and no remaining QA process. Read-only final
  database audits again find only successful start/resize/stop, zero terminal.write,
  zero messages and zero incomplete records (`native-audit-final.log`).
- `terminal-cached/terminal.trx` passes 255/255 terminal cases, including 26
  input-method cases. Narrowing/widening updates candidate bounds without replacing
  the live client or changing preedit. Shared-component and lifecycle gates remain.
- The initial solution-wide parallel run finishes 4330/4334, with four Windows
  timing failures: cancelled native resize, pending file presentation, shell
  descendant startup and natural terminal cleanup. No platform code, test deadline
  or test parallelism is changed to mask those failures. The evidence is retained
  in `full-final`; it is not a clean full-regression claim.
- The final run executes all eight projects sequentially while preserving default
  in-project parallelism: 4334/4334 pass, zero failed/skipped (`full-serial`).
  App 3246, Visual 8, Windows 129, Providers 366, Tools 297, Browser 76,
  Infrastructure 149 and Agent 63. Literal-path TRX aggregation verifies all eight
  results; the Windows project's 129 cases all pass on this separate run. This
  does not establish that the first run's concurrency-sensitive timing is fixed.
- Solution and rebuilt native host compile with zero warnings/errors. Scoped
  whitespace verification passes after correcting one initializer indent; the
  first formatting failure remains in `format-final.log`. Roslyn auditing checks
  six C# files plus the viewport/style AXAML: one type per C# file, at most 500
  physical lines and at most 80 lines per method. The largest is 243 lines.
  Whole-worktree `git diff --check` passes. Existing changes remain unstaged;
  normal JIT debugging and Windows-x64 single-file release settings are unchanged.

## Remaining boundaries

- Physical Windows IME key routing, OS candidate-popup placement, international
  keyboard layouts, non-100-percent DPI and screen-reader behavior are not verified.
- Mouse reporting, VT query responses and cursor blinking remain separate work.
- This is terminal integration evidence, not pixel-identical Codex workspaces or
  completion of the full MossAgent goal. No commit, push, release or AOT change.
