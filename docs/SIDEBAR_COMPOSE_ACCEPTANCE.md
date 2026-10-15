# Sidebar compose entry acceptance - 2026-10-10

## Ownership and self-check

- App owns the existing `UiIconCompose` vector and shared sidebar action styles.
  The business view still uses `AppActionButton` with `Catalog.NewTaskCommand`;
  no native visual control, new runtime port or dependency is introduced.
- The icon's open square, pen contour and stroke alignment are corrected together.
  Its shared viewport is 11 DIP with a one-DIP stroke and optical translation
  `(0.5,-1)`. The global `AppIcon` normalization/rendering code is unchanged.
- Icon-label spacing increases from 10 to 11 DIP, retaining the label's x=92
  client position as the icon becomes smaller. Moving one DIP from the action's
  top margin to its bottom margin raises the label without changing the pinned,
  project or recent section positions. The 30-DIP hit height remains unchanged.
- Changes do not access files, providers, SQL or OS APIs from production UI;
  authorization, approval, timeout, cancellation and output limits are unchanged.
  No security/privacy setting, real credential, project data or provider request
  was changed during native QA. Debug remains JIT and release remains Windows x64
  single-file publishing, without AOT changes.
- Changed handwritten C# files contain one type, with methods below 80 lines.
  App.axaml remains at the 500-line limit; the shared style files remain below it.

## Native pixel comparison

Python passively captured the actual Codex window and ordinary-permission
production App/MainWindow hosted with independent synthetic SQLite data.
The Codex image is 1936x1048; only its eight-pixel outer frame on each side was
cropped, producing 1920x1032 client pixels. No capture or comparison is resized.

| Measurement, client coordinates | Codex | Before | After |
| --- | --- | --- | --- |
| Icon bounds at luminance <160 | (70,105)-(82,117) | (69,107)-(82,120) | (70,105)-(82,117) |
| Icon size at luminance <210 | 12x12 | 14x14 | 12x12 |
| First label character at <160 | (92,106)-(104,118) | (92,107)-(104,119) | (92,106)-(104,118) |

Final icon bounds also agree at the stricter <100 and looser <210 thresholds.
The pen contour is a vector, not a copied bitmap. Identical bounding boxes are
not a claim that every contour/antialias pixel or the whole sidebar is identical.
Brand names, task contents and product-specific commands intentionally differ.

## Interaction and regression evidence

- Native light maximized 1920x1032, light restored 1280x820 and dark narrow
  980x640 captures show the shared entry without clipping or row misalignment.
- Actual clicks on New Conversation in light/dark previews clear selected-task
  ownership and the previous task's inspector; both hosts report
  `QA_SELECTED_TASK: none`. No prompt, paid API or tool approval is submitted.
- Each native process uses an asInvoker manifest, reports `QA_ELEVATED: False`
  and exits itself with `QA_WINDOW_CLOSED` and `QA_LIFETIME_RETURNED`.
- Ten new cases cover six theme/viewport combinations, normal action-button
  isolation, keyboard activation and disabled-command behavior. The original
  sidebar fails six measured-margin cases. A scaffold assertion used the raw
  icon-label default 5 instead of the already-styled action spacing 9; it was
  corrected without changing unrelated production styles.
- Twenty-five local geometry/rhythm cases and 91 initial focused component,
  markup, header and navigation cases pass. Existing pinned/project heading
  positions remain asserted, rather than updating their expectations to hide
  a one-DIP layout regression from an intermediate iteration.
- The first full run exposed four old compose-only cases expecting the previous
  12-DIP viewport. Their size, gap, margin and optical-center assertions are
  updated to the measured resource/style geometry; project rows and section
  typography expectations are unchanged.
- The final focused run passes 111 cases. Focused C# whitespace verification and
  size/type audits pass for all four changed test files; each contains one type,
  stays below 500 lines and has no method above 80 lines.
- The final solution build succeeds with zero warnings and errors. The subsequent
  full run passes 4268/4268 cases across eight test assemblies, with zero failures
  or skipped cases. Logs and TRX files are retained in `full-final` under the
  evidence directory below. These results precede the later hot-path optimization
  slice and are not its verification results.

## Evidence and remaining scope

Scripts, hosts, synthetic data, logs and screenshots stay outside Git:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-sidebar-compose-20261010-01`.

- `sidebar-header-comparison.png` contains native-size reference/before/after crops.
- `sidebar-header-measurements.json` records thresholded bounds and label position.
- Intermediate captures/measurements retain the rejected contour/alignment attempts.
- `native-new-conversation.png`, `native-light-normal.png`,
  `native-dark-narrow.png` and `native-dark-new-conversation.png` record interactions.
- The restored light capture includes unrelated OS maximize-hover chrome near the
  right edge; the measured left entry is unobstructed. No frame-fidelity claim is
  based on that overlay region.

This does not complete the broader Codex fidelity/product goal. Dark Codex colors,
other DPIs, every icon, draft-tab heading behavior and all responsive chat/panel
states remain outside this comparison. No staging, commit, push or publish occurs.
