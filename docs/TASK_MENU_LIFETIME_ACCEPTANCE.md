# Task menu lifetime and native sidebar replay

## Design and scope

1. App owns this shared-control behavior. `AppTaskNavigationRow` retains its
   existing task parameter and commands; dependency direction is unchanged.
2. Existing Avalonia property and visual-tree hooks suffice. The row closes its
   options on context/target/action-command replacement, effective disablement,
   own or ancestor hiding, and detach. Ancestor subscriptions are removed on
   detach and rebuilt on attach. Title/selection updates do not dismiss the menu.
3. No tool, authorization, approval, timeout, output or persistence contract
   changes. Native replay uses an independent database backup, one paused plan
   and a loopback observer refusing unexpected model requests. Only that copy's
   task pin is changed and then restored; no send, run, archive or delete occurs.
4. Production and test files remain single-type and below 250 lines; no shared
   dimensions, icons, colors, templates or page-local styles are changed.
5. Twenty focused cases cover both themes, invalid owners/targets, detach,
   command replacement, reattachment, removed-ancestor isolation and preserved
   title/selection updates. Menu dismissal never invokes a task action.

## Reproduction and checks

Evidence stays outside source under
`%TEMP%\MossAgent-task-menu-20261013-1040`.

- The original twelve-case baseline has ten failures and two preservation
  passes. Open flyouts remain after row/parent hide or disable and context change.
- The first repaired group passes eighteen cases. With reattachment coverage,
  the wider sidebar/project/search/flyout/danger group passes 420/420, including
  all twenty lifetime cases. None fail or skip.
- The first complete eight-suite run passes 5,696/5,697. Its single failure is
  the existing `WorkspaceToolProgressOptimizationTests` allocation assertion:
  the recorded measurement is one byte/operation instead of zero. Its separate
  five-case recheck passes. No production optimization or assertion is changed
  to hide that failure. Original TRX evidence remains under `full/`.
- The final sequential eight-suite recheck passes 5,697/5,697: App 4,135, visual
  24, Agent 119, Browser 76, Infrastructure 199, Platform.Windows 137, Providers
  516 and Tools 491. None fail or skip. Its separate completed TRX files remain
  under `serial/`; the earlier failure is not erased or recast as a pass.
- The final solution build passes with zero warnings/errors, and scoped
  whitespace formatting verification passes. An earlier overlapping build
  failed to copy the App DLL while the App test host held it open; the build is
  rerun successfully only after that test process exits. The final production
  assembly hash still matches the native replay. Size and whitespace checks pass.

## Native evidence ownership

The executing default Debug/JIT App SHA256 is
`8DFEF6A76562ABA8C499C02F728C4D501FEA2FE257E5D1B8A08A6941BB524BA0`.
Its launch, passive PNG/JSON captures, independent data and audits are under
`%TEMP%\MossAgent-narrow-fidelity-20261013-1015\native-menu`.

- Startup is maximized at 1920x1032, DPI 96 and ordinary privileges. Explicit
  activation restores 1280x820; the actual caption action subsequently restores
  maximized size. Both sizes are measured, not normalized by image scaling.
- Initial input is refused because Codex is foreground. Fresh MossAgent
  activation/observation precedes retry. Codex is passively captured only.
- Actual task-menu pin moves task A to the pinned section while keeping its
  selected conversation. Read-only SQLite checks prove only the expected pin
  appears: task rows, messages and archived rows remain unchanged.
- Ctrl+K opens search. A set-value call times out but the next observation proves
  the requested query and empty notice are present; no blind retry is sent.
  Escape dismisses the popup while retaining filtering. Reopening and the real
  clear button restore pinned/project/recent lists and retain the conversation.
- Clicking outside an open task menu first light-dismisses it; a separately
  observed second click hides the sidebar without residual popup. Restoring the
  sidebar leaves the selected pinned task's menu usable. These native actions
  do not replace the deterministic owner-invalidation/reattachment tests.
- Actual unpin restores the original pin set. Catalog rows, all six task rows,
  saved messages, four terminal histories, original audits and three hash-checked
  artifacts remain unchanged. Only the four historical requests exist; no new
  execution or descendant process is observed.
- The actual close button exits zero. The observer stops normally and port 51792
  has no listener. The original process instances exit; a later reused PID is
  not treated as a surviving observer. Original application data is not changed.

## Codex reference comparison and limits

`sidebar-native-comparison.png` uses equal-size 1920x1032 DPI-96 passive frames
and unscaled crops. Five sampled titlebar/rail/sidebar/main/selected-row colors
match. Search and sidebar-toggle ink bounds match; pinned-heading bounds match.
The project-heading thresholded ink retains a one-pixel left-edge difference.
An initial sidebar color sample landed on Codex's scrollbar; it is corrected
to unobstructed background, not treated as an appearance defect.

Tasks/projects, rail feature availability and context-card preferences differ;
these are shell samples, not whole-window or equivalent-content acceptance.
Higher DPI, dark/current Codex and PI-Desktop reference equivalence, all visible
control states and full-product completion remain open. Keep the product goal
active. No AOT, JSON, packaging, commit, push or release change occurs.
