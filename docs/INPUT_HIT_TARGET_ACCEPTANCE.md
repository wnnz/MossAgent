# Wrapped input and checkbox hit-target acceptance

## Plan and design self-check

- The previous projection optimization made verified progress. This slice
  addresses the separately observed long-composer and checkbox blank-area gaps;
  the complete product/fidelity goal remains active.
- App owns both behaviors in shared FieldVariants and SharedCheckBoxTheme.
  Existing AppInput/AppCheckBox wrappers and application ports remain unchanged.
  No per-view patch, custom pointer handler, new I/O or execution behavior.
- Human multiline prose, prompt and composer fields share wrapping and vertical
  scrolling. Single-line and JSON editors retain their original code/address
  editing. Composer height stays bounded at 180 DIP; dimensions and typography
  are not duplicated in views.
- Checkbox template background covers its full native bounds, including spacing
  and vertical insets. The 16-DIP surface and 12-DIP glyph remain centered and
  unchanged. Native enabled/disabled, keyboard and three-state behavior is reused.
- No authorization, approval, timeout, output, audit, persistence, credential,
  JIT/Windows-x64 single-file publication or dependency-direction change.
- Both new handwritten C# test files contain one type and stay below size/method
  limits. New appearance rules stay in existing shared styles.
- Focused tests prove wrapping with finite viewports, unbroken text, raw draft
  preservation, vertical scrolling, role-specific input behavior, disabled pointer
  rejection, full checkbox hit regions and native three-state cycling.

## Reproduced failures and focused regressions

- Original composer variant has no TextWrapping rule. Six narrow/wide light/dark
  long-input cases fail with NoWrap. Two human multiline/prompt cases independently
  reproduce the same omission; JSON editing passes its non-wrapping baseline.
- Original checkbox template has no background on its centered content grid.
  Two enabled hit-region cases and a blank-area three-state case fail. Disabled
  click rejection and single-line editing already pass and remain passing.
- All 174 focused composer, checkbox, field, shared-style, native-control-boundary
  and bound subagent-editor cases pass after the shared-style correction.
- Source snapshots and failing/passing TRX stay outside Git under
  `%TEMP%\MossAgent-input-hit-target-20261012`.

## Actual native interactions and passive Python screenshots

- The updated external host runs without elevation on PID 17844 with a new
  synthetic root. No daily data, real provider credentials or approval/security
  controls are changed. No model send occurs: parent and child request counts
  remain zero throughout. There is no paid or public-network dependency.
- Clicking the enabled checkbox at x1260/y869, far to the right of its glyph and
  label, changes its bound draft to false. The stored profile remains true. Space
  with that checkbox focused restores true, without saving the synthetic draft.
- Actual typing creates an unsent 1,189-character draft. Its original bounds are
  706 by 180 DIP with 24 wrapped lines, horizontal offset zero and only vertical
  scrolling. Ctrl+Home reveals its start marker; the earlier end marker is visible
  at the bottom. The send/model/approval footer stays visible.
- Restore from 1920 by 1032 to 1280 by 820 DIP preserves the draft. Opening the
  inspector narrows the input to 423 DIP and increases wrapping to 41 lines;
  horizontal extent equals its finite viewport and the footer stays visible.
- Actual appearance-theme selection switches to dark. Returning to the workspace
  retains the same 1,189-character, 41-line draft and vertical-only scrolling.
  Native screenshots cover light/dark, expanded inspector and wide/normal windows
  at measured scale 1; other DPI combinations are not claimed.
- Native geometry compares the live input with the workspace draft but exports
  only lengths, equality flags, dimensions and scroll metrics, not arbitrary text.
  Screenshot text is synthetic. The passive Python helper remains outside Git.
- Evidence in the root's native directory includes checkbox-blank.png/json,
  checkbox-keyboard.json, composer-light/start/narrow-light/narrow-dark.png/json
  and final-assertions.json. Normal close confirms QA_FINAL_REQUESTS=0,0 and
  QA_UI_EXIT=0.

## Final gates

- Complete App regression passes all 3,667 cases, with zero failures/skips
  (5 minutes 41 seconds). Evidence: input-hit-app-full.trx.
- The test host exits before the serial solution build. `dotnet build
  MossAgent.slnx --no-restore -m:1 -v minimal` succeeds with zero warnings/errors.
  Evidence: solution-build.log.
- Post-build replay passes all 15 new input/hit-target cases, and all seven
  architecture-constraint cases pass. Evidence: input-hit-post-build.trx and
  input-hit-architecture.trx, with their corresponding final logs.
- Final Roslyn audit confirms one type per new test file, both below 250 lines,
  and a longest method of 37 lines. Shared FieldVariants and CheckBoxVariants
  remain 33 and 66 physical lines, respectively, with no trailing whitespace.
  Evidence: source-audit-final.log.
- Included-file whitespace formatting verification and `git diff --check` pass.
  Existing uncommitted work is preserved; nothing is staged, committed or pushed.

This is not a claim of complete Codex visual parity, all editor states,
input-method behavior, all DPI combinations or full product acceptance.
The existing fixed-length held-response diagnosis remains unresolved.
