# Shared button focus shape

## Design self-check and scope

1. App owns this appearance-only correction. Module dependencies stay unchanged.
2. Existing AppButton/AppToggleButton wrappers and Avalonia's native focus-adornment
   lifecycle are reused; no service port, alternate button template or event
   handler is introduced.
3. Commands, approval policies, tools, storage and networking remain unchanged.
   Tests render isolated Headless windows; existing desktop windows and user data
   are not accessed.
4. One 13-line shared style is included through ActionButtonVariants, avoiding
   further growth of the 498-line App.axaml. New test files each declare one type;
   modified handwritten C# files/methods remain below the hard limits.
5. Tests cover rendered corners/strokes, layout/content origins, radius updates,
   pointer versus keyboard focus, blur, explicit focus ownership and enabled/
   disabled command activation in Light/Dark and multiple render scales.

The UI/UX skill's applicable guidance is visible keyboard focus and consistent
component states, not removal of focus feedback or mobile-sized desktop targets.

## Reproduced defect and correction

Avalonia's default focus adorner draws a square black/white frame around otherwise
rounded buttons. In the verified unchanged-appearance baseline, all 24 shape cases
fail the corner-pixel assertion: a transparent rounded corner becomes a square
stroke on keyboard focus. Raw screenshots reproduce this across both themes,
100/125/200-percent rendering and the actual composer-send, selected navigation
rail, conversation-tab, settings-nav, history, icon and ordinary action variants.

Shared ButtonFocusVariants replaces that adorner with an AppBorder using the
existing BorderFocusBrush and a two-DIP stroke. Its corner radius binds to the
native adorned focus target. It is not hit-testable and does not participate in
button layout. Native focus creation/removal, button dimensions, content padding,
labels, icons, hover/pressed states and command execution remain in place.
Explicit FocusAdorner ownership, including theme thumbnails, keeps precedence.

The markup guard previously misclassified the nonvisual Template factory as a
native visual component. Its allowlist now recognizes that factory. Negative
tests still reject native Border/Button descendants inside it, and business
markup still cannot replace shared FocusAdorner templates. Native controls are
not newly permitted in views, controls or shared templates.

## Validation

- The focused final run passes all 28 new pixel/state cases. Its 24 shape cases
  are the same cases as the failed verified baseline; four additional cases check
  command behavior and explicit focus ownership.
- The final complete pixel suite passes 236/236, including those 28 cases. The
  earlier focused runs are not added to this complete count.
- All 1,649 related App cases pass, matching the separately discovered filter
  count. This includes the two new markup-boundary cases, existing theme-thumbnail
  focus ownership, caption/navigation/dialog/composer tests and eight actual
  automation UI workflows. The real editor's Cancel button now renders the shared
  rounded ring in the complete MainWindow capture. Seven architecture cases also
  pass. These three disjoint final scopes total 1,892, with no failures, skipped/
  not-executed or aborted cases and unique execution identities.
- After all tests finish, the solution build succeeds with zero warnings/errors.
  Six recorded tested/runtime DLL hashes stay unchanged across that build; all
  three production App copies match. Pixel-test identities also stay unchanged
  while building/running the App subset. Scoped whitespace-format, XML/hygiene
  and Roslyn source-limit/one-type checks pass.
- The first complete attempt records 20 new-test resource-lookup failures: the
  fixture omitted the actual ThemeVariant when finding BorderFocusBrush. It now
  supplies that theme explicitly. That report and earlier fixture compilation/
  guard failures remain evidence, not reproduced production defects or erased
  by the subsequent green run.

Evidence stays outside the repository at
`%TEMP%/MossAgent-button-focus-20261016`: baseline/final TRX and logs, unchanged
state PNGs, source/markup audits and before/after crops. The crops preserve
100-percent pixels and compare MossAgent before/after, not another application.
`app-related-filter.txt`, `app-related-discovery.txt`, `verified-test-counts.json`,
`solution-build.log` and hash CSVs record the exact final validation boundary.

## Remaining gates

Native screenshots and existing-window operations remain user-postponed. Off-screen
render scaling does not establish Windows DPI behavior or same-state Codex visual
parity. Other product interactions, the earlier unexplained App allocation failure
and complete-product acceptance remain open. The 1,649-case subset is not a full
App/eight-suite checkpoint or an allocation-failure investigation. This correction changes no packaging,
JSON, JIT/AOT or user settings; nothing is staged, committed, pushed or released.
