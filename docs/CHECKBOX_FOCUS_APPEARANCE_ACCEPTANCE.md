# Shared checkbox focus appearance

## Design and scope

This slice applies the UI/UX skill's desktop guidance: retain visible keyboard
focus, keep interaction bounds stable and use shared semantic theme resources.
It extends the existing rounded button/dropdown language rather than replacing
the Codex-inspired design or imposing mobile-sized controls.

Required design self-check:

1. App owns shared checkbox appearance; dependency directions do not change.
2. The existing AppCheckBox/native CheckBox contract and theme are reused.
   No new application service or port is needed.
3. No tool, approval, file, network or persistence behavior changes. Evidence
   uses isolated Headless rendering, not existing native desktop windows.
4. Production changes stay in CheckBoxVariants.axaml. Each handwritten test file
   has one type and passes the existing source/method limits.
5. Rendered tests cover all three selection states, Light/Dark, fractional DPI,
   geometry stability, keyboard/pointer activation and disabled rejection.

## Reproduced defects

The unchanged UI fails 20 of the 22 new cases. All 18 shape cases show a native
black/white square focus rectangle; the two interaction/theme cases also detect
the missing shared adorner. The two explicit-focus-owner cases already pass.
Baseline TRX and rendered frames record executed failures, not compile failures.

The first rounded-adorner candidate removes the square frame, but four geometry
cases still fail: checked/indeterminate glyphs in both themes move by 0.8 DIP at
125-percent scaling. The existing focus selector increases the check-surface
border from one to two DIP, changing fractional layout rounding. That candidate's
failed TRX is retained, not presented as a successful final revision.

## Correction

- The shared checkbox theme supplies a default non-interactive AppBorder focus
  adorner using BorderFocusBrush, a two-DIP outline and the control's corner
  radius. The theme's default radius is four DIP, consistent with its check box.
- The check-surface border no longer changes thickness when focused. Its existing
  focus brush remains; the separate adorner provides emphasis without reflow.
- The native 16-DIP check surface, 12-DIP glyph, 28-DIP minimum height, label gap,
  full-row hit surface, hover/pressed treatment and disabled opacity are unchanged.
- Native Space/pointer activation, three-state cycling and disabled behavior are
  retained. An explicit local FocusAdorner still overrides the theme default.
- The existing structural interaction test now checks the retained focus brush
  instead of requiring the old geometry-changing thickness. Pixel tests assert
  unchanged surface/glyph/label origins, preventing the drift from returning.

## Final verification

- The complete pixel suite passes 282/282, including all 22 new cases.
- Related App controls, checkbox hit targets, shared-template/component rules,
  themes, settings layout and focus checks pass 424/424. Discovery also reports
  424 cases, matching the executed set.
- Architecture checks pass 7/7. These three disjoint scopes total 713, without
  failures, skipped/not-executed or aborted cases. Focused reruns and the earlier
  baseline/candidate runs are not added again.
- Solution build succeeds with zero warnings/errors. Scoped whitespace, diff
  hygiene and source-limit verification pass. Four checkpoint assembly identities
  remain unchanged across testing and final incremental build; the App copies in
  both test outputs match the production App assembly.
- Light/Dark before/after frames and the comparison sheet were visually inspected.
  Tests cover 100-, 125- and 200-percent rendering and live theme changes.

Evidence stays outside the repository in
`%TEMP%\MossAgent-checkbox-focus-20261016`, including `baseline.trx`,
`focused.trx` (first candidate), final TRX reports, `before-after.png`,
`source-audit.json` and build/format/identity reports. No screenshot helper,
generated image or synthetic settings data is added to source control.

## Remaining limits

These are production-style Headless frames, not native MossAgent/Codex same-state
screenshots. The user's postponed native capture remains respected. This is not a
complete current App/all-suite checkpoint, proof of Codex pixel equivalence,
resolution of the earlier unrelated allocation assertion or product completion.
The long-term product goal remains active.
