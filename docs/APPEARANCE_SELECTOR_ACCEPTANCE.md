# Appearance selector acceptance

## Design self-check

1. App owns the shared pill selector template and Appearance composition. No
   dependency direction changes are needed.
2. Retain native AppComboBox selection, popup, keyboard and accessibility behavior;
   reuse the existing theme/font services and preference store. The selected font
   may have a compact label without shortening its full popup/accessibility label.
3. Only synthetic appearance preferences change during verification. Tool approval,
   authorization, model execution and cancellation policies remain unchanged. No
   new file, network or process operation belongs in UI code.
4. Keep each handwritten type in a separate small file. Shared variants own
   geometry and states; do not add per-page dimensions or grow App.axaml.
5. Rendered tests cover default pill sizes, contained swatches and adaptive long
   labels in light/dark narrow/wide layouts. Interaction tests cover native keyboard
   selection, popup dismissal, disabled input, independent persistence and failed
   saves without overwriting malformed or unrelated preferences.

## Reference and correction

The existing same-state passive Codex Appearance reference is 1920x1032 at 96 DPI.
Its accent pill outline spans (1380,384)-(1458,412), 78x28 pixels; the default font
pill spans (1398,516)-(1458,544), 60x28 pixels. The pre-change MossAgent selectors were
92 pixels wide, kept a separate accent swatch and used the larger native minimum
height. The default font's closed label was also longer than the reference.

Correct those controls through reusable shared components while preserving real
selection and error behavior. Theme presets/import/export, complete thumbnail
artwork, title rasterization, DPI and full-product acceptance remain separate open
gates. Exact selected samples are not whole-page pixel-equivalence evidence.

## Implemented shared components

- `AppPillComboBox` inherits `AppComboBox`; `PillSelectorVariants.axaml` owns its
  28-pixel height, 14-pixel radius, compact arrow, contained content, shared colors
  and states. The accent swatch is inside the selectable hit area. Long font
  labels expand the closed selector instead of being clipped to a fixed width.
- The native `PART_Popup` and `PART_ItemsPresenter` retain selection, keyboard,
  light dismissal and accessibility behavior. Popup, items presenter and data
  validation wrappers each have their own small handwritten type file. Default
  `AppComboBox` templates elsewhere are not changed.
- The UI font's closed selection uses localized `CompactLabel` (default: System),
  while the popup retains the full System default label and actual font choices.
  Existing preference services still own save, reset, per-theme independence and
  failure reporting. No schema, JSON source-generation or AOT change is included.
- Popup item keyboard focus uses the shared highlight. Its explicit null
  `FocusAdorner` suppresses Avalonia's fallback black frame; the closed control's
  visible keyboard focus remains. Shared variants, not the Appearance page, own
  all new geometry and appearance.

## Corrections found during verification

The initial red geometry/interaction baseline failed six of ten checks: the old
selectors were 92x32 and the accent swatch was outside their hit area. Initial
template compilation errors were repaired before runtime testing. Four rendered
geometry failures exposed double vertical rounding; stretching the content slot
restored the expected swatch centering. Two focused-navigation tests needed an
actual focus transition, and two existing source guards needed the new shared
control names without losing their binding/ownership assertions.

Native screenshots then exposed the popup's thick black keyboard frame. A style
against the popup's ancestor did not cross its logical-tree boundary. Two added
regressions verify the final item-container theme, including an explicitly set
null FocusAdorner and the actual shared highlight brush. Those failed/intermediate
candidates are not presented as the final tested production App.

## Native and screenshot evidence

Evidence root:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-appearance-selectors-20261014`.
The final candidate has its own `final` evidence directory and independent Debug,
Windows x64, JIT artifacts at `.scratch/appearance-selectors-final-20261014`.
Final production App DLL SHA-256:
`EE676859F798B19902065E25D1DBDC51729E9769C976AD608A447B3FCABDDA3D`.

The Python capture/audit scripts live outside the repository. PrintWindow rejects
black padding and verifies the exact PID, path, ordinary permission and 96 DPI.
Related popup pixels come only from the same App process. Nineteen main captures
and five separate popup captures cover the following real UI operations:

- Same-state System/effective-light, maximized 1920x1032 baseline, with the already
  acquired passive Codex Appearance reference; no new Codex input automation.
- Swatch click, Down/Enter, light blue save, font popup with full labels, Segoe UI
  selection and actual restore to System. A stale Sky frame after font restore was
  checked against Python pixels/preferences rather than patched into the product.
- Real Light palette reset, Dark selection, Down/Enter dark blue save, End/Enter
  Inter selection, Home/Enter System restore and real Dark palette reset. Saved
  snapshots independently verify that Light and Dark preferences remain separate.
- Restore to 1280x820, actual resize to 526x601, both themes at minimum size, compact
  accent popup and Escape dismissal, compact font popup and Escape dismissal.
  Closed controls stay within the card. The font popup may extend beyond the main
  window while remaining inside the screen; its separate PNG contains all pixels
  beyond the clipped main-window composite.
- Actual return to Light/default fonts, maximize, then normal Alt+F4. The launch
  process reports exit zero; both its PID and HWND disappear. Final preferences
  explicitly retain Light/Dark Default font entries rather than pretending the
  settings file returned byte-for-byte to its original Theme-only form.

`native-audit.json` verifies nineteen unchanged synthetic SQLite table hashes,
zero artifact files, normal exit, final preferences and the nine recorded App/test
assembly identities. The synthetic database copy is not the user's real data.
No approval settings or old task sends change; no packet capture was performed,
so unchanged tables are not proof of zero network traffic.

`measurements.json` and `selector-comparison.png` compare the final
`01-light-max.png`, not an intermediate binary. Selected light-gray outline
samples match the reference accent bounds (1380,384)-(1458,412), 78x28, and default
font bounds (1398,516)-(1458,544), 60x28. The composite shows reference, pre-change
and candidate. This does not assert equal text, arrow or every border pixel.

## Final verification

- Final related tests: 582 passed. New coverage adds fourteen cases: four rendered
  geometry cases, two popup keyboard-highlight cases, two swatch/disabled hit-area
  cases, two keyboard/per-theme persistence cases and four failed-save cases.
- Final complete App: 4,428 passed, zero failed/skipped. The other seven complete
  assemblies pass 1,622: Visual 84, Tools 491, Providers 516, Agent 119,
  Infrastructure 199, Browser 76 and Windows 137. Complete total: 6,050. The 582
  focused cases are a subset and are not counted again.
- The independent final solution build has zero warnings/errors. Scoped App/test
  whitespace formatting and explicit whitespace checks pass. All nineteen paths
  in this checkpoint are currently untracked in this dirty checkout; the checker
  uses `git diff --no-index --check` for them. Per-command `core.autocrlf=false`
  suppresses line-ending advisories without changing repository configuration.
  Handwritten C#/AXAML files remain below 500 physical lines; the largest touched
  source is the existing 470-line shared-style test file. The index remains empty.
- `test-audit.json` independently parses all final TRX results, checks actual case
  outcomes, excludes intermediate candidates, prevents duplicate complete assembly
  counts and confirms the recorded DLL identities are unchanged. The intermediate
  App run's 4,426 successes are not used as the final 4,428-case result.
- Native, table/hash, preference, artifact and normal-exit checks described above
  pass for the same final App identity. Only acceptance documents are edited after
  these tests; product source and tested outputs remain unchanged.

## Remaining gates

The 96-DPI samples establish this selector correction, not complete Appearance or
product equivalence. Title rasterization/vertical ink position, glyphs/arrows,
thumbnail artwork, theme presets/import/export, high DPI and the remaining full
surface/state matrix remain open. Current successful child runtime and PI-Desktop
provider-page equivalence remain separate product gates. No default launch output,
staging, commit, push, release or installation is changed by this checkpoint.
