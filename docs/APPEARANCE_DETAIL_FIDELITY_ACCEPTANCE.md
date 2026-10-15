# Appearance detail fidelity acceptance

## Design self-check

1. App owns the settings layout and shared appearance controls. No module or
   dependency changes are needed.
2. Reuse the existing palette/font editors, native AppInput editing and theme
   commands. A shared color field owns its swatch and compact pill together;
   thumbnails remain code-native scenes, not screenshot assets or fake controls.
3. Only appearance drafts/preferences change in isolated native replay. Tool
   authorization, approvals, cancellation, storage, models and JIT/JSON stay intact.
4. Each handwritten type has its own small file. Shared styles own dimensions,
   colors and layout variants; do not add more includes to the 496-line App file.
5. Rendered tests cover measured anchors, field bounds and scene structure in both
   themes/widths. Interaction tests cover two-way drafts, keyboard focus, invalid
   save/recovery and disabled fields. Existing settings focus gates must remain.

## Reference and intended correction

The existing passive Codex Appearance capture is 1920x1032 at 96 DPI, System
selected and effectively light. Its title ink starts at (748,124), while the
preceding MossAgent version starts at (730,108). The basic-card dividers have
matching row intervals but are 21 pixels too high; the content column is wider.
Reference color pills contain their swatch and value in one 96x28 outline.
The old native inputs stretch to their row height, with a separate swatch.

Correct the shared settings column/heading, encapsulate compact editable color
pills, and replace the obsolete large settings-list thumbnail with a conversation
scene. Preserve real editing and selection behavior. Full-page/product parity,
theme-preset/import/export functionality and successful child execution remain
separate open gates; this document is not a completion claim.

## Implementation and regression record

The shared content column now caps at 728 DIP, with measured heading/padding
changes instead of per-page offsets. The title uses shared 28-DIP typography.
The palette keeps its existing 226-DIP height and row intervals; its dividers use
the shared card-border brush. `AppThemeColorInput` contains the 14-DIP swatch and
the native AppInput in one 96x28 pill. It forwards two-way text and the localized
accessible name; keyboard editing, selection and focus remain native. Scoped
template states prevent native focus/hover borders from creating a second frame.

The obsolete 248x174 settings-list preview becomes a reusable compact conversation
scene with sidebar, assistant/user lines and composer. System clips two instances
at the midpoint; Dark no longer contains a white inner window. Thirty unused old
preview styles are removed. Existing Diff resources/styles remain in their file,
which includes the new thumbnail variants without growing App.axaml.

The pre-edit geometry group fails ten cases and passes two. The first candidate
passes 451 related cases and fails two old 24-DIP heading expectations. Those
guards and the old shared-column source guard are updated to the measured design,
without removing any navigation/focus, modal ownership or binding assertions.
The final focused group passes 466 cases, including six new field cases, both
themes, multiple widths, invalid save/recovery and keyboard focus/disabled paths.

## Native evidence and measured limits

The separate production candidate under `.scratch/appearance-details-20261014`
has App SHA-256
`867E4848241D62303BD0697A9F6727CB263AC5B25DA3B315EABFCB2303DFF689`.
All eleven Python passive captures have this identity, 96 DPI and ordinary
permissions. Native replay verifies System/effective light and Dark maximized,
normal restore to 1280x820, an actual grip drag to 526x601, both compact themes,
and scrolling to the expanded Diff and content/code-font controls. Actual keyboard
entry of an invalid color is rejected by Apply, keeps the page light and shows the
localized error. The real Reset command restores the draft. Preferences end at
exactly `{"Theme":"Light"}` and the App closes normally, exit zero.

The database is a new independent copy of the preceding synthetic fixture, never
the user's data directory. All nineteen table hashes remain unchanged, including
the cancelled task and its unapproved tool audit; no artifacts are created. No
approval, sharing, authentication or privacy settings are automated. Unchanged
tables are not a packet-capture claim that no network activity occurred.

Unscaled same-state crops now have identical sampled horizontal lines at y=310,
327,375,419,463,507,552, all RGB 237; the six palette lines retain the reference
48/44/44/44/45 intervals. The sampled background-pill outline also matches
(1362,428)-(1458,456), 96x28 pixels. The title ink's horizontal bounds now match,
but its top is still two pixels lower and its raster height one pixel smaller.
These selected gray/ink samples do not prove all border pixels, text or card
geometry equal. Thumbnail artwork/rasterization, accent/font selectors, preset
actions, remaining page/state/DPI and full-product gates stay open.

Scripts, synthetic data, capture metadata, hashes and `details-comparison.png`
remain outside Git under
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-appearance-details-20261014`.
The default launch output, user data, packaging and JIT/JSON are not changed.

## Final checkpoint

The final serial complete App run passes all 4,414 cases, with zero failures,
skips or aborts and driver exit zero. The other seven complete assemblies pass
1,622 cases: Visual 84, Tools 491, Providers 516, Agent 119, Infrastructure 199,
Browser 76 and Platform.Windows 137. The eight-assembly total is 6,036; the
466-case focused group is verified as an App subset and is not counted twice.
Architecture constraints are already included in the App results.

The final solution build has zero warnings/errors. Scoped App/test whitespace
format verification, tracked/untracked source whitespace and file-size checks
pass. External Python verification and an independent PowerShell result check
agree on TRX counters, unchanged test DLL identities and the production App hash.
The native audit is rerun after normal shutdown; both recorded test/native
processes have exited, all nineteen table hashes remain unchanged and preferences
are restored to Light. These checks do not promote the sampled pixel matches to
full-page parity or establish a fresh successful child run.

`verified-checkpoint.json` and `independent-final-check.json` remain with the
external evidence. No files are staged, committed, pushed, released or installed;
the full product objective and the visual/functional gaps above remain open.
