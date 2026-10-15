# Window sidebar glyph and native replay (2026-10-13)

This corrects a measured titlebar glyph mismatch and supplies fresh production-App
evidence. It does not establish whole-window Codex equivalence or product completion.

## Design and safety self-check

1. App owns the shared geometry and semantic size variant. Dependencies and
   `AppIconButton` composition remain unchanged; no native implementation is added.
2. The existing left-panel command, accessible label, tooltip and hit area are
   preserved. UiIconPanel has only the titlebar binding as a production consumer.
3. Authorization, approval, models, persistence schemas and execution limits do
   not change. Native replay uses a read-only SQLite backup in a separate data
   directory, a paused plan and a bounded loopback observer. No message, new run,
   plan enabling, permission change, paid request or security control is invoked.
4. App.axaml remains 496 lines and WindowVariants.axaml 58. AppIconButtonTests is
   217 lines; the new single-type pixel test is 102. Methods remain below 80 lines.
5. Twelve Skia cases exercise both themes, 980x640/1280x820 and bitmap scales
   1/1.25/2, plus unchanged hit geometry. Real native capture, pointer movement,
   navigation, window state, data integrity and shutdown are verified separately.

## Measured mismatch and correction

The earlier native 1920x1032 comparison records a 14x12 MossAgent sidebar-toggle
ink extent against Codex's 12x12. AppIcon intentionally preserves geometry aspect
ratio and applies an unscaled stroke. UiIconPanel's wide rectangular path therefore
cannot become the reference square contour merely by changing the button's hit area.

- UiIconPanel now uses a rounded square contour and its internal left divider.
- The titlebar-panel shared icon Size changes from 12 to 10, retaining stroke 1.
- The existing 32x30 button hit area, centered glyph and toggle command remain
  unchanged. No other icon renderer, resource consumer or per-view style changes.

The final native comparison uses unscaled, equally sized 1920x1032 passive frames:

| Glyph | Codex ink | Current native ink | Position |
| --- | --- | --- | --- |
| Sidebar toggle | 12x12 | 12x12 | Measured ROI bounds identical |
| Minimize | 10x1 | 10x1 | Measured ROI bounds identical |
| Restore | 10x10 | 10x10 | Measured ROI bounds identical |
| Close | 10x10 | 10x10 | Measured ROI bounds identical |

Sampled titlebar/rail RGB 238,244,249, sidebar RGB 249,251,253 and workspace white
also match. Native close-hover RGB is 232,17,35 (`#E81123`) with a white glyph.
Dark normal close-hover capture records the native Arrow cursor, not Hand.
Matching thresholded extents and sampled colors do not imply complete pixel masks,
typography, other states, dark Codex pixels or whole-window equality.

## Deterministic verification and harness corrections

Evidence root: `%TEMP%\MossAgent-sidebar-icon-fidelity-20261013-0933`.

- The first baseline incorrectly assumes a 28-pixel button height and fails 12
  cases. This is a harness mistake, not twelve reproduced product regressions.
  After preserving the existing 30-pixel hit height, `baseline-corrected/` fails
  all twelve cases on the actual nonsquare ink extent, with width/height differing
  by two pixels at the selected threshold.
- The corrected glyph passes all twelve rendered cases. The first mixed 41-case
  group still fails 24 cases: twenty source-root lookups cannot find the repository
  above the external output path; four caption cases encounter a mixed
  HeadlessPlatformTypeface/SkiaTypeface error. Those failures remain in `focused/`.
- Pixel tests are moved to the existing separate Skia-only App.Visual.Tests
  assembly, reusing SharedPixelTestApplication. Source-inspection outputs move
  beneath ignored repository `.scratch`, so their existing ancestor lookup works.
  No source-root assertion, font-cache implementation or production behavior is
  weakened to make the group pass.
- Final App caption/button/shell group passes 29/29. The complete pixel assembly
  passes 24/24, including the twelve new cases and all existing rendering cases.
- Final serial full regression passes 5,667/5,667 across eight completed TRX files:
  App 4,105; visual 24; Agent 119; Tools 491; Providers 516; Infrastructure 199;
  Browser 76; Platform.Windows 137. None fail or skip.
- Isolated solution build and scoped whitespace formatting verification pass.
  The already-running default-output QA App is not overwritten during testing.
  No commit, push, release, AOT, JSON or packaging change occurs.
- After all QA instances close, the default-output solution build also completes
  with zero warnings/errors. Its App SHA256 is
  `1D1FB84D717C9364B8CE95D8414C1445EF733E44744A44FC77C013D2A2558AAB`.
  That build has not been replayed natively at this checkpoint; the native
  evidence below belongs only to the separately identified isolated assembly.

## Fresh native production-App replay

The executing isolated Debug/JIT App assembly SHA256 is
`E2A392571F6D94106E6F9C94523ACF04D1597B402FB119CEBCE26FE6B6B16A86`.
Its executable is under ignored `.scratch\sidebar-icon-fidelity-20261013-0933`;
the data backup, helper scripts, PNG/JSON evidence and loopback logs stay outside
repository source under the evidence root's `native/` directory.

- Startup, ordinary privileges and DPI 96 are checked. Cold maximized capture
  contains the full 1920x1032 frame, with 1280x820 normal bounds and no black padding.
- A fresh exact executable/window selection followed by explicit activation
  produces the correct MossAgent pixels. Activation restores the maximized window
  to its 1280x820 normal bounds; this side effect is measured, not hidden by scaling.
  Actual caption maximize/restore actions subsequently produce both native sizes.
- Light maximized close-hover is exercised with a zero-delta wheel pointer move,
  not a close click. Passive PNG verifies the measured red and white glyph.
- Actual inspector expansion hides the floating project-context card. Deep-theme
  switching through the View menu updates the open inspector; collapsing it shows
  the context card again. No browser/tool tab or authorization prompt is activated.
- In dark 1280x820, the actual minimize button makes IsIconic true while preserving
  1280x820 normal bounds. The expected minimized-state observation error is followed
  by fresh selection/activation; restoration returns to the recorded normal size.
- The paused automation list displays four saved terminal histories. Opening the
  newest Completed row shows its persisted synthetic assistant reply, project B,
  saved ReadOnly policy, model B and high reasoning without invoking that model.
  Accessibility occasionally lags the pixels after navigation; stale indexes are
  not used for the history action or as proof of its outcome.
- Before/after read-only audits retain one paused plan, four independent terminal
  runs, six tasks, original fifteen audits, three hash-verified artifacts and only
  four historical whitelisted requests. App descendants are empty.
- After restoring light theme, the actual caption close exits the App with code
  zero, recorded by the live launch-monitor process. The observer is signalled to
  stop and its process/listening socket disappear; no forced termination is used.
- The earlier caption-build QA instance is freshly selected and activated before
  closing its observed real close button. Its PID disappears and its original
  data audit still passes. Its exit code is not observed. All four QA/observer
  PIDs and the observer socket are absent at final cleanup.

The earlier incorrect-target and padded captures in
`WINDOW_CAPTION_FIDELITY_ACCEPTANCE.md` remain historical failures. Correct fresh
selection/capture and invalid-frame rejection make this replay possible; they do
not independently establish the cause or a fix of the Windows helper's earlier
wrong-pixel/DPI behavior. Codex is only passively captured, never automatically
operated. No historical automation run is promoted to a new execution.

## Remaining gates

Native 980x640 and higher-DPI replay of this build, complete surface/state/visible-
control audit, equivalent dark/current Codex and PI-Desktop references, and full
window/font/shadow equivalence remain open. Keep the complete product goal active;
do not promote these bounded titlebar and restored-history checks to all surfaces.

The later default-output responsive replay and shared danger-palette repair are
recorded in `DANGER_BUTTON_THEME_ACCEPTANCE.md`. Its before default assembly adds
native 980x640 light inspector and dark dialog-reopen evidence; the separately
identified repaired assembly adds native light/dark provider and safe hover
evidence. Transient higher-DPI captures and a rejected black frame remain bounded
observations, not a completed high-DPI sweep or proof of a capture-helper fix.
