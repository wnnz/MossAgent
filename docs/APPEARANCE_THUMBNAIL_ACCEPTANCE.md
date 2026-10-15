# Appearance thumbnail acceptance

## Design self-check

1. App owns existing shared theme-mode/preview controls and appearance variants;
   no module or dependency direction changes are needed.
2. Reuse native AppButton commands, keyboard navigation, accessibility labels and
   the existing theme/preference service. Keep thumbnails code-native scenes.
3. Only isolated synthetic appearance preferences may change during native
   verification. Do not change tool approval/privacy policies or user data.
4. Shared variants own dimensions, scene colors and focus/selection overlays;
   keep individual handwritten types and source files below repository limits.
5. Rendered tests verify full-size hit-area content, scene geometry, square System
   seam and non-resizing selection. Keyboard/native tests verify theme selection,
   focus visibility, actual saved mode and both compact/maximized presentations.

## Reference and intended correction

The existing passive same-state Codex Appearance reference is 1920x1032 at 96 DPI.
Its three mode thumbnails and hit areas are 80x60. MossAgent's buttons match that
size, but their border/padding reduce scene content to 72x52. The System scene also
rounds the dark half's central seam instead of clipping both halves in one frame.
Correct shared thumbnail content and scene proportions without replacing real
selection commands or presenting the reference raster as an application asset.

Evidence root:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-mode-thumbnails-20261014`.
Full-page equivalence, title typography, theme presets/actions and high DPI remain
separate gates; initial crops are not final-candidate acceptance.

## Implemented shared correction

- Scene content now fills the full 80x60 hit area. Native buttons keep their actual
  theme commands/accessibility labels, while zero border/padding and unscaled
  viewboxes prevent the former eight-pixel content reduction.
- A rounded shared frame clips the System halves at x=40. Their central backdrop
  corners are square; a separate outline overlays the artwork without resizing it.
- Shared scene variants restore the inset screen/sidebar proportions, two-pixel
  message lines, blue accent, eight-pixel composer and four-pixel send mark. Light
  uses a subtle backdrop gradient; dark screen/sidebar colors follow the measured
  reference. Screen shadows use the existing SharedSurfaceShadow contract.
- Selection stays blue. Keyboard focus contrasts with the depicted thumbnail,
  not merely the surrounding application theme: dark uses a pale outline, light
  uses a dark outline and the mixed System scene uses blue. The native fallback
  adorner is suppressed only because this explicit focus indicator remains.
- Fifteen new rendered/interaction cases cover complete hit-area content in both
  widths/themes, non-resizing overlays, System clipping, all six focus/theme
  combinations and disabled keyboard activation. Existing native theme-command
  tests and scene/source guards retain their behavior assertions.

## Correction history

An initial xUnit collection assertion fails compilation and is corrected before
the red baseline. The geometry baseline then fails all nine provisional cases;
its unbound keyboard fixture is subsequently replaced with the production fixture,
not treated as a diagnosed product bug. The first two candidate builds reject a
custom shadow literal and then a dynamic shadow reference. The final style uses
the required static shared resource; no boundary guard is relaxed.

The first candidate passes 52 focused cases and has App SHA-256
`3C60A6D5E16EEDE0677BA57E0ED12170BA68FA9646DFF4138E8A242864C0E1FF`.
Its native keyboard check exposes the low-contrast dark-thumbnail focus outline.
That is corrected and receives six explicit contrast cases plus a disabled case.
Initial screenshots/tests are retained as intermediate evidence, not silently
promoted to the final App identity. The earlier shared-padding guard changes from
2 to 0 because outlines now overlay artwork; its ownership assertions remain.

## Final native and measured evidence

The separate final Debug/JIT Windows x64 candidate is under
`.scratch/mode-thumbnails-final-20261014`; its App DLL SHA-256 is
`9DD01EA766B60DD457215AD54AD440C973D3FD90BC1D4A68B63A1C77D1E1EB0E`.
Final evidence is in the root's `final` subdirectory. Verification spans the local
midnight of October 14–15; the evidence directory retains its original date label.

Twelve Python passive PrintWindow captures check this exact executable, PID/HWND,
ordinary permissions, 96 DPI and non-black pixels. Actual UI interaction covers
System selection; Tab and Shift+Tab across all modes; Enter saving Dark and System;
contrasting focus on light/dark artwork in a dark page; 1280x820 restore; an actual
resize to 526x601; all three modes at minimum size; and return to maximized Light.
No Codex input is automated: the existing passive same-state reference is reused.

The final App (PID 9160) exits normally with code zero; its process/window disappear
and stderr is empty. All nineteen synthetic SQLite table hashes remain unchanged,
zero artifacts are recorded, and the UI preference file returns byte-for-byte to
the original Light/default-font state. The old cancelled/unapproved task history
is not sent or changed. Unchanged tables are not a packet-capture assertion.

The independently measured 80x60 same-state thumbnail regions are:

| Mode | Bounds | Prior mean absolute RGB error | Final mean absolute RGB error |
| --- | --- | --- | --- |
| System | (1186,242)-(1266,302) | 21.443819 | 3.611806 |
| Light | (1282,242)-(1362,302) | 11.738611 | 4.151875 |
| Dark | (1378,242)-(1458,302) | 56.158958 | 4.221458 |

This error is the mean channel difference on the 0–255 scale within these three
regions, not a percentage of whole-window fidelity. Borders, branding glyphs,
rasterization and some colors still differ. `thumbnail-comparison.png` shows the
reference, prior App and final candidate using the same unscaled crop coordinates
before a three-times display enlargement; it is not an application image asset.

## Verification scope

- Final related App tests: 834 passed, zero failed/skipped, with fifteen new cases.
- Seven other final complete assemblies: Visual 84, Tools 491, Providers 516,
  Agent 119, Infrastructure 199, Browser 76 and Windows 137; total 1,622.
  Fresh final total is 2,456; the related App group is not a complete App suite.
- The final solution build succeeds with zero warnings/errors. Scoped C#
  whitespace formatting, explicit source whitespace and physical size checks pass;
  the largest touched source remains the existing 470-line shared-style guard.
- `verify.py` parses every final TRX outcome/counter, checks the actual assembly
  paths and App identities, unchanged product sources, capture hashes/dimensions,
  preferences, nineteen tables, artifact count, normal exit and the three pixel
  measurements. `verified-checkpoint.json` reports success for this bounded slice.
- The earlier 3C60 candidate's serial complete App run finishes with 4,437 passed,
  zero failed/skipped and driver exit zero. Its assembly identities and TRX are
  verified separately. It is not a final-candidate full suite result and does not
  increase the final 2,456-case count; the final App's complete suite is not rerun.

Full-page/product equivalence, title ink position, remaining branding/glyph pixels,
theme preset/import/export actions, high DPI and the whole interactive-state matrix
remain open. Default launch output, user data, approval/privacy settings, JIT/JSON
and packaging are unchanged. Nothing is staged, committed, pushed or released.
