# Code preview gutter acceptance

## Scope (2026-10-09)

Horizontal scrolling previously moved line numbers and diff markers out of the
viewport along with long code lines. The shared `AppCodePreview` now keeps its
gutter fixed for both authorized text snapshots and real Git review patches.
This is a shared-component correction, not per-view geometry or a new file reader.

## Design and safety self-check

1. App owns rendered layout and scrolling over the already authorized in-memory
   snapshot or bounded Git result. No module dependency, persistence schema or
   scheduler/provider behavior changes.
2. Existing file-preview and Git tool paths remain authoritative. Scrolling and
   search do not reread files, execute tools, stage files or invoke model requests.
3. One scroller and one row tree own both axes. Each preview owns its own mutable
   translation; previews cannot move another instance's gutter. Opaque gutter
   surfaces block input on code behind them. Content replacement resets both axes;
   search brings a match into vertical view without changing horizontal position.
   Existing snapshot lifetime, cancellation, late-result rejection and rendering
   limits remain unchanged.
4. All visual elements remain shared wrappers. Styles own gutter layering,
   background and transform bindings. Row states are the single source for added,
   removed, hunk and search colors, rather than duplicating semantic classes on
   children. No event subscription to a global owner is added.
5. Sixteen focused cases cover both themes, plain/diff modes, 300/640-DIP preview
   widths, number/marker positions, actual code movement, search navigation,
   content replacement, multi-preview isolation, opaque hit testing and semantic
   row/gutter alignment. Existing search/preview regressions are retained.

## Tests and source checks

Evidence root: `%TEMP%\MossAgent-code-gutter-20261009`.

- The initial behavioral red run executes 12 cases: ten fail and two pass.
  `red/gutter-red.trx` records real position/missing-gutter failures. An earlier
  compile-only attempt is preserved separately as `compile-red.log` and is not
  counted as a behavioral reproduction.
- The first shared styling attempt is rejected by the existing markup appearance
  gate. Transform bindings remain in shared Styles rather than weakening that gate.
- A broad preview run then exposes four existing search assertions counting the
  duplicated child semantic classes. Keeping semantic classes only on the row and
  deriving the gutter surface from ancestor selectors fixes the composition, not
  the existing assertions. The final focused preview filter passes 145/145 cases,
  including all 16 additions, in `focused-final/preview-focused-final.trx`.
- `dotnet format whitespace --verify-no-changes` passes for the four scoped C#
  files. The source check finds one type per file; they are 68, 57, 82 and 79 lines.
  Component/style markup parses as XML and remains 30/176 lines. Solution and
  separate ordinary native QA builds report zero warnings/errors.
- The first full run passes 3301/3302: the existing workspace cancellation test
  throws `ObjectDisposedException` for `SQLitePCL.sqlite3` while creating its
  synthetic task. An isolated rerun of all five cancellation-flow cases passes.
  No unrelated SQLite implementation or fixture is changed. The final identical
  full command passes 3302/3302 with zero failures/skips across seven independent
  TRX reports: App 2499, Tools 188, Providers 283, Infrastructure 136, Browser 76,
  Agent 63 and Platform.Windows 57. Both initial and final logs are retained in
  `full/`, `full-final/`, `full.log` and `full-final.log`.

## Native interaction and pixels

The standalone QA host uses the real App startup/shutdown and production tool
executor, separate SQLite/project data and an actual temporary Git repository.
A nested Unicode file contains 93 lines, including long synthetic C# declarations.
It is untracked and reviewed ReadOnly, then opened through the authorized snapshot
workflow. No paid provider, user file, user database or user settings participate.

- A separately built pre-change Light 1280x820 host reproduces disappearing line
  numbers using native horizontal input. Its baseline image is
  `before-Light-1280-820/window-horizontal.png`.
- The final Light 1280x820 and Dark 980x640 hosts run under ordinary permissions
  with render scaling one. Actual horizontal and combined horizontal/vertical
  input keeps numbers/markers fixed and aligned while code moves. Both Git review
  and ordinary file snapshot presentation are checked, including the compact
  dark toolbar. Backgrounds remain opaque and match added/plain surfaces.
- In Light, native mouse input opens and focuses the real search input, then types
  `Value60`. The match is brought to line 62 and highlighted across its gutter and
  content; the horizontal position remains unchanged. The draft is continuously
  checked by the QA host and is not consumed by review, preview or search.
- Both final Light and Dark hosts record four successful ReadOnly audits and exit normally
  with `QA_INDEX_UNCHANGED: true` and `QA_EXIT: 0`. The index check compares cached
  diff content, not byte identity of Git's index. Baseline selection replacement
  cancels one initial read; it is not a Git-result or shutdown failure.
- Passive Python PrintWindow checks non-black output and exact native dimensions.
  `code-gutter-before-after.png` compares 414x427 native-size Light code-pane crops
  at the same horizontal-scroll state without scaling. It is MossAgent before/after,
  not a same-state Codex reference. Capture/comparison Python, QA sources and
  synthetic data stay outside the repository; QA binaries are ignored artifacts.

## Remaining limits

Same-state Codex review/preview screenshots are still required for final fidelity
acceptance. Native higher DPI, user-font substitutions, keyboard selection/copy
across lines and all hover/focus states are not newly verified by this slice.
Diff metadata/hunk text retains its existing horizontal-scroll behavior; only
the line-number/marker gutter is fixed. Split diff and virtualized large-file
rendering are not introduced. Existing parser/output limits still apply.

Managed/JIT development and Windows x64 single-file publishing remain unchanged.
No commit, push, publish or staging operation is performed.
