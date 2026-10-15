# Compact navigation

## Design and required self-check

The fresh passive Codex reference is a 526x601 conversation with a collapsed
sidebar. Before this slice, MossAgent's 980x640 window minimum prevented that
state. Both shells independently reserved a fixed 264-DIP sidebar; simply
lowering the window minimum would have hidden primary actions and settings.

1. App owns a shared navigation-pane layout control consumed by both shells.
   No dependency, platform integration, persistence or provider change is needed.
2. Reuse the actual sidebar and primary controls, existing selection commands,
   shared surfaces and the window's sidebar toggle. Preserve the wide-window
   visibility preference separately from temporary compact drawer state.
3. Layout and navigation invoke no tools or model requests. An open drawer blocks
   underlying pointer/keyboard input; dismissal restores eligible prior focus.
   Escape, outside click and explicit navigation selection close only the drawer.
   Hidden, disabled and detached hosts cannot execute stale toggle commands.
4. Use one type per handwritten C# file and shared appearance variants. Do not
   recreate settings editors, conversation controls or drafts during resize.
5. Cover exact layout boundaries, both themes, preference/draft/focus retention,
   selection and dismissal, disabled/hidden/detached commands and real consumers.
   Run related tests and a solution build, then native screenshots and replay.

The shared compact breakpoint is 264 + 360 = 624 DIP in the navigation host,
not a claimed Codex breakpoint. MainWindow now permits 526x601 while keeping
the default 1280x820 size. Wide sidebar preference remains independent of the
temporary compact drawer. Both real shells use the same control and the same
toolbar toggle; the primary/editor instances are not recreated during resize.

AppResponsiveColumns stacks the actual automation list and history below its
300 + 260 + 20 DIP breakpoint, retaining both scrollable views. Shared settings
dialogs constrain both dimensions to the host minus their 24-DIP margins. The
existing extent converter is renamed to describe its width/height use, including
approval cards; approval policies, commands and persistence remain unchanged.

## Execution evidence

External evidence is stored at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-compact-navigation-20261014`;
the passive Python capture/comparison and independent launch/audit tools are
outside the repository. Debug/JIT, JSON adapters and single-file release settings
are unchanged. No commit, push or release is performed.

- Initial real-consumer tests exposed an unconstrained provider dialog and an
  incorrectly injected Escape on the window rather than its focused control.
  The dialog now fits its actual host. Routed keyboard input plus retained
  primary focus cover the production toolbar, not just direct command execution.
- An additional real keyboard case exposed Escape from the toolbar while the
  drawer is open. The attached host now handles unconsumed root Escape and
  unsubscribes on detach. Outside left click, Tab containment, unavailable hosts,
  replacement, dynamic metrics and retained expansion preference are covered.
- Initial compact pixel checks sampled a palette card instead of the empty
  page margin and assumed integer DIP bounds at 125 percent scaling. The final
  guards compare primary width with its actual host and sample empty margins;
  exact palette checks and all pre-existing pixel guards remain intact.
- Ordinary-permission native App PID 4792 verifies 526x601, 96-DPI light/dark
  settings/dialogs, workspace drawer/Escape, settings selection/outside dismissal,
  the compact automation list/history and the scrollable editor with fixed
  footer. Returning to 1920x1032 restores the wide sidebar and side-by-side
  automation history. No plan is saved, enabled or executed during this replay.
- App and observer exit zero after ordinary close. The final read-only audit
  matches all nineteen table fingerprints, three original artifact checks,
  four-request history and restored-Light UI bytes; observer port 51792 is free.
- The first complete App run passes 4,316 cases and fails three old fixed-grid
  source guards. They now assert shared navigation ownership, actual-visibility
  tint binding and active-shell toolbar routing. The window geometry guard moves
  out of the formerly 494-line file rather than exceeding the file limit. The
  remaining palette, rail, titlebar, frame and dialog assertions are retained.
- Final `verified/gate-exit.json` exits zero: all 86 related cases, 72 pixel cases,
  seven architecture cases and the complete 4,319-case App run pass with no
  failures/skips/aborts. Six complete non-UI groups pass 1,538 cases; their DLL
  hashes stay unchanged after the final build. `verified/verified-checkpoint.json`
  independently checks TRX counters, eight assembly identities and native audit
  ownership: 5,929 total passes, excluding duplicate related/architecture subsets.
  Scoped formatting, zero-warning/error solution build and full diff whitespace
  checks pass. The final App hash matches the native replay's candidate.

`compact-native-reference.png` contains unscaled 526x601 native Codex and
MossAgent screenshots. Their conversations/context states differ; it proves the
new matching viewport, not complete page or component pixel equality. Codex is
only passively captured. The end-of-run `codex-end-passive.png` is a valid
600x750, 96-DPI conversation capture, still not an equivalent Appearance page.

The separate native DPI/clipping issue, equivalent Appearance/PI-Desktop pages,
full same-state surface matrix and overall product objective remain open.
