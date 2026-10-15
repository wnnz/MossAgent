# Settings preview scroll and syntax fidelity

## Scope and design self-check

- The previous input/hit-target slice completed regression, build and native
  gates. This continuation retains the complete product objective as active.
- App owns the existing read-only AppDiffPreview and shared appearance styles.
  Reuse native ScrollViewer/ScrollBar/RepeatButton/Thumb behavior and AppIcon;
  no custom input handler, execution port or platform dependency is needed.
- No authorization, approval, timeout, cancellation, output, persistence,
  credentials, JIT debugging or Windows-x64 single-file publishing change.
- Keep new test files below 250 lines, methods below 40 where practical, and
  exactly one handwritten type per C# file. Shared markup owns appearance.
- Focused tests must prove idle arrow visibility, real overflow rather than
  forced blank scrolling, independent arrow/thumb interaction, disabled-input
  rejection, theme switching and isolation from ordinary inspector syntax.

## Current evidence and intended correction

- The original clipboard PNG is no longer present. The retained comparison
  sheet under `%TEMP%\MossAgent-settings-preview-20261009` contains its original
  unscaled 768-by-126 diff crop and 248-by-174 theme thumbnails.
- The reference dark thumbnail intentionally contains a white list card;
  this is not a dark-theme bug and its colors/structure must remain unchanged.
- A fresh ordinary-permission production-composition host reproduces both
  horizontal bars with AllowAutoHide=true, IsExpanded=false and line-button
  opacity zero. At wide width, the forced 384-DIP minimum creates a 1.5-DIP
  overflow even though the synthetic source text fits.
- The reference uses visible muted filled arrow glyphs, not a permanently
  drawn inactive thumb. Remove the artificial minimum; keep the existing
  16-DIP native bar and expose real line/page/drag behavior only when needed.
- Reference-crop token samples and the fresh native frame show distinct
  identifier/type/number hues. Preview-only theme resources must own the
  sampled light palette without changing ordinary CodeKeyword/CodeType brushes.
  Preserve the existing dark palette because no new dark Codex reference exists.
- External screenshots, native host, synthetic data, observer and Python helper
  stay outside Git under `%TEMP%\MossAgent-preview-syntax-20261012`.

## Validation

- The original implementation fails six of 12 new cases; the six already
  passing native arrow/drag/disabled behaviors remain passing. The focused
  preview/markup replay passes 20 cases and the expanded theme, preview,
  settings, inspector, sidebar and native-control-boundary set passes 388.
  Evidence: preview-baseline/fixed/focused.trx.
- Headless font metrics differ from the real code font. Zero-overflow headless
  cases use a 1,100-DIP window, while the real page confirms no horizontal
  overflow with the unchanged 768-DIP content column. Narrow component cases
  exercise actual overflow without adding a synthetic minimum to production.
- The production-composition page runs without elevation, at measured scale 1
  and a captured 1,920-by-1,032 window. Both 382.5-DIP viewports equal their
  horizontal extents; all four 16-DIP native line buttons have visible 8-DIP
  filled glyphs. Zero-range bars are disabled and their thumbs are hidden.
- Actual page clicks select Dark and then Light, updating syntax/selection
  resources in place. The synthetic preference file saves each selection;
  the original dark-thumbnail white list card remains intentionally unchanged.
- A separate real AppDiffPreview control window at 420 by 250 DIP verifies
  overflow: 286.92-DIP extent versus 208.5-DIP viewport. Clicking the left
  column's right arrow moves only that column to 16 DIP; its left arrow restores
  zero. Dragging the right thumb moves only its column to about 56.90 DIP;
  clicking its remaining track reaches the 78.42-DIP range end. This is a
  control-window interaction check, not a supported 420-DIP full-app layout.
- The first control host retained the main window's maximized state. Its
  observation was not counted as narrow coverage; the corrected external host
  restores Normal before applying control-window dimensions. No production
  window behavior is changed. The passive Python helper's old 300-pixel height
  filter also rejected this live small window; exact-PID passive captures now
  allow it. Neither observation issue was treated as application termination.
- All native hosts close normally with QA_UI_EXIT=0 and empty stderr. Parent
  and child model request counts remain zero; no real account/provider request
  or security/approval-setting interaction occurs.
- Unscaled original-position diff crops compare reference, fresh baseline and
  revised frames. Dominant ink hues for all six sampled token kinds now match
  the retained reference: #751ED9, #BD5800, #751ED9, #BD5800, #008809, #0071EA.
  Pixel counts differ, so this is not a whole-frame/font-raster parity claim.
  Evidence: preview-syntax-comparison.png/json and native-final-assertions.log.
- C# whitespace verification passes; Roslyn confirms one type per changed test
  file, with new files below 250 lines and their methods below 40 lines.
  Existing AppearanceDiffPreviewTests retains its pre-existing 44-line method,
  below the 80-line hard limit.
- Full App regression passes 3,679 cases with zero failures/skips. The serial
  solution build completes with zero warnings/errors; post-build architecture
  checks pass seven cases and the final preview replay passes all 20 cases.
  Evidence: preview-app-full.trx, solution-build.log, preview-architecture.trx
  and preview-post-build.trx under the evidence directory above.

This record does not establish whole-window parity, every DPI/state or complete
product acceptance. There is no fresh dark Codex settings reference; the dark
palette preserves prior behavior rather than inventing reference values.
The held fixed-length SSE diagnosis remains a separate open item.
