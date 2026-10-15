# Narrow workspace layout

## Reference and staged design

Fresh passive Python captures of Codex 26.1007.2314.0 show an ordinary-permission,
96-DPI, 526x601 conversation with its navigation sidebar collapsed. This proves an
observed usable size, not Codex's minimum size or its sidebar/inspector breakpoints.
The current MossAgent MainWindow minimum remains 980x640. Do not lower it until
navigation, settings, composer and inspector interaction fit the resulting space.

The first correction addresses shared inspector layout: docking currently reduces
the primary pane below its configured minimum when both panes cannot fit. Reuse the
existing full-width inspector presentation automatically at that point, without
changing the user's explicit expansion or preferred splitter width. Returning to
sufficient width must restore the previous docked split. This is a layout safety
requirement, not a claim of a captured Codex inspector breakpoint.

## Required design self-check

1. App owns the shared AppWorkspacePanes layout and presentation bindings. No Domain,
   Application, infrastructure, platform or dependency-direction change is needed.
2. Reuse the existing pane presenters, expansion commands, inspector close command
   and splitter preference. Keep both pane instances and unsent primary input intact.
3. Layout invokes no tool, provider, storage or command process. Existing approvals,
   cancellation, path authorization and output limits are unchanged. A compact
   inspector must retain its real close action; disabled expansion cannot change
   state through stale commands.
4. Keep one type per handwritten C# file, no partial split, new view-specific
   dimensions or colors. Expose effective full-width presentation separately from
   the explicit expansion preference.
5. Test real headless window resize and conflicting measure/arrange widths in both
   themes, exact breakpoint boundaries, restoration of user width/expansion/draft,
   property notifications and rejection of unusable expansion commands. Re-run
   existing pane, preview, inspector and solution-build checks.

## Outstanding acceptance

This preparatory pane correction does not make MainWindow resizable to 526x601.
Native sidebar behavior needs a fresh reference; navigation/settings adaptation,
composer and page coverage, native small-window screenshots, high-DPI/clipping and
the complete product goal remain open. Record executed checks below before handoff.

## Implemented layout boundary

`AppWorkspacePanes.IsInspectorExpanded` remains the user's explicit preference.
The read-only `IsInspectorFullWidth` reports actual presentation: an open inspector
uses the complete host when explicitly expanded or when the configured minimum
primary/inspector widths and splitter cannot fit. The default host breakpoint is
644 DIP (360 + 280 + 4), not a measured Codex breakpoint. Returning to sufficient
width restores the retained split unless the user explicitly requested expansion.
Neither presenter nor its supplied control is replaced, and unsent input survives.

Ordinary and file-preview headers consume actual presentation. A compact restore
action is disabled, while its real close action restores the primary pane. File
preview cleanup observes the separate explicit `IsExpansionRequested` value rather
than the layout-derived display value: hiding a preview must not toggle expansion
twice while layout notification is pending. Width feedback from a hidden/full-width
column cannot overwrite the user's preferred split. Detach clears effective state
and subscriptions through the existing ownership boundary.

## Executed checks, October 14, 2026

Evidence lives outside the repository at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-narrow-workspace-20261014`.
Only `final-gates` belongs to the final test driver. The earlier root-level driver
started with an older test DLL after a test compilation error; it was explicitly
stopped and is not counted as a passing run. The invalid test API was replaced
with `TopLevel.GetTopLevel` before the final build and driver launch.

- The initial ten-case layout baseline fails eight cases. Subsequent real-consumer
  tests expose preview cleanup's mixed intent/display state and a test assumption
  about a detached primary control. Both are corrected before the final gates.
- Sixteen new pane cases cover both themes, 470/643/644 widths, exact/dynamic
  minimum boundaries, preferred split and explicit expansion retention, rejected
  compact toggle commands, conflicting measure/arrange sizes, notifications and
  initially compact primary attachment. Four real InspectorPane/chat cases cover
  both themes and both ordinary/file-preview headers, keyboard close, retained
  controls/draft, disabled restore and no model submission.
- The final 377-case related regression, all 48 pixel cases and seven architecture
  constraints pass, without failures or skips. The complete App-assembly gate
  finishes at 10:15:46 +08 with driver exit zero: all 4,262 cases pass, with zero
  failures, errors, timeouts, skips or aborts. The final TRX counters and all 4,262
  individual result nodes agree. Driver and xUnit child processes have exited.
  This is a newer complete App checkpoint, not a new complete multi-assembly or
  full-product acceptance run.
- `solution-build-final.log` records a zero-warning/error solution build. Scoped
  whitespace verification exits zero (`format-final-exit.json`), as does the
  complete tracked diff whitespace check. No files are staged.

The final production App DLL SHA256 is
`92C72DB210B7B13CCB5098445756D4D8C98E3F5C5BA46697279190C18B9F0FC7`;
the final App test DLL SHA256 is
`0151A99EA18C952A4DA151C520EAB86CBDE57DF669C9B5F7EEB6D051088872FB`.
These are newer assemblies than the preceding 4,242-case font checkpoint.

## Native replay and independent audit

The same Debug/JIT App runs with ordinary permission and an independent synthetic
data root. Python passive screen capture verifies foreground ownership, process,
96-DPI geometry and non-uniform pixels. Native replay covers 1920x1032 workspace,
docked and explicitly expanded inspectors, then 1280x820 expanded, restored split
and closed inspector states. Restore brings the existing conversation back; close
restores the floating context card. No draft is submitted or model request added.

`native/inspector-expanded-light-normal-clean.png` is the settled ordinary-window
full-width frame. Immediate input frames, including the file named
`inspector-restored-light-normal-clean.png`, contain Computer Use's transient blue
pointer highlight; the closed-inspector frame retains a hovered tooltip. They
verify state/geometry, not clean shadow, hover or palette equivalence. Do not treat
the helper highlight as a product shadow or claim all frames are clean.

Normal caption close produces App and observer exit zero, both processes disappear
and loopback port 51792 is released. `native/final-closed.audit.json` passes database
integrity, all nineteen table fingerprints, three original artifact verifications,
the four-request history hash and the unchanged light UI-settings bytes. No real
user data, approval setting, provider, JSON adapter, AOT or packaging changes occur.

A fresh passive Codex frame after closing the preview still shows the 526x601
conversation with collapsed navigation, not Appearance or an open sidebar.
This replay does not exercise native automatic compact layout: MainWindow's
980x640 minimum is deliberately unchanged. Headless compact behavior, native
ordinary-size replay and Codex breakpoint equivalence remain separate claims.

`shared-chrome-native-comparison.png` places unscaled common titlebar and rail
crops side by side; the Python script and crop provenance stay outside the repo.
Both sources are ordinary, foreground-verified, 96-DPI captures, but their pages,
sidebar and navigation states are not equivalent. Caption ink at a luminance
threshold of 170 occupies the same 10x1 / 10x10 / 10x10 boxes for minimize, maximize
and close. The darkest reference pixel is 31 in each, versus 93 / 130 / 93 in the
candidate. This exposes a remaining contrast/rasterization mismatch rather than
proving button equality or its root cause. Rail feature differences and the missing
same-state sidebar/Appearance references remain visible; no automated Codex input
is used to manufacture the comparison state.
