# Settings status overflow acceptance

## Scope and reproduced failure

The shared settings-page header previously measured status text at unrestricted
height. A saved provider name repeated in discovery feedback could consume the
entire page, hiding the provider list and its actions. Provider names are only
required to be nonempty; this is valid input, not a test-only invalid setting.

Four production-pane Headless baselines reproduce the problem in Light and Dark
at 486x601 and 1024x601 DIP. The list viewport shrinks from 366 to zero at the
compact width and from 408 to zero at the wider width. All four assertions fail;
their original TRX, logs and frames remain in the external evidence directory.

## Shared correction and design self-check

- Ownership stays in App's shared `AppSettingsPageHeader` and
  `SettingsPageHeaderVariants.axaml`; no view-local appearance is added.
- The existing `AppScrollViewer` wrapper bounds status feedback to 96 DIP,
  disables horizontal scrolling and retains automatic vertical scrolling.
- Text, wrapping, error styling and complete accessible names remain intact.
  Short messages retain natural height; clearing status restores the original
  header/list geometry. Other page scroll viewers are unaffected.
- The status viewer is keyboard-focusable. Native Shift+Tab traversal, PageDown
  and PageUp expose the complete message without scrolling the provider list.
  Existing focus feedback is retained; no custom key handler is introduced.
- This reuses the production ViewModels, commands and existing discovery port.
  There is no new production I/O, persistence, subscription, dependency, tool or
  approval boundary. Tests use synthetic configuration and deferred discovery;
  they do not contact a paid provider or use personal configuration.
- The two AXAML files are 22 and 35 physical lines. The new fixture and test
  files are 112 and 140 physical lines, each with exactly one type and methods
  below the 80-line hard limit. No production C# is added or changed in this fix.

The ui-ux-pro-max guidance is applied as desktop keyboard operability and
unobscured actions, not as a claim of web WCAG conformance or a visual redesign.
The existing Codex-oriented palette, typography and control system are retained.

## Focused checks

`SettingsStatusOverflowTests` adds 16 cases: four groups across both widths and
themes. They cover long discovery failures with sanitized errors, pending
discovery followed by normal completion, complete keyboard-readable messages,
and short/cleared feedback with unrelated scroll behavior unchanged. Actual
hit testing and Space/Escape verify that the create action remains usable.

Final validation against the corrected production binary:

| Check | Result |
| --- | --- |
| Related App tests (`related-final.trx`) | 232/232, including all 16 new cases |
| Complete pixel suite (`pixels.trx`) | 188/188 |
| Architecture subset (`architecture-final.trx`) | 7/7 |
| Final solution build | Zero warnings and errors |
| Scoped whitespace format verification | Passed for both new C# files |
| Tested/runtime assembly identity | All six final hashes unchanged after build |

The three test assemblies and three App copies are recorded in
`hashes-before-final-related.csv` and `hashes-after-final.csv`. The earlier pixel
run's visual/architecture assemblies and all App copies also match
`hashes-before-final.csv`; only the App test assembly changed when loading-state
cases were added. Every reported final TRX row passed, with no skips or aborts.

This is 427 checks across those suites/subsets, not a new full-product total.
The historical eight-suite 6,495 checkpoint predates these changes and remains
historical. This slice does not rerun the full App suite or all non-UI suites.

## Evidence and remaining limits

Evidence root:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-settings-status-20261016`

- `baseline/`: original four failed rendered states.
- `final-related-frames/`: final error, loading and keyboard-reading frames.
- `status-before-after-light.png` and `status-before-after-dark.png`: compact
  before/after comparisons built from baseline and corrected rendered frames.
- TRX/logs, final build/format logs and assembly hash CSVs remain external.

These are independent off-screen component/page renders using production
markup, not current native MainWindow or Codex screenshots. They do not prove
whole-window parity, native DPI behavior or repair the separate native resize
defect. The user's postponed screenshot boundary is respected: no existing
Codex/MossAgent window is operated, captured or restarted for this slice.
No files are staged, committed, pushed or released; the product goal remains open.
