# Settings typography acceptance

## Scope and design checks

The saved 96-DPI Codex Appearance frame has 14-DIP titlebar menus and a
14-DIP visual-style heading. MossAgent used 13 for both, while its ordinary
palette labels correctly used 13. This change is limited to shared presentation.

1. App owns the behavior; no dependency direction or composition changes.
2. Existing menu controls and settings variants remain the contracts. No new
   view model, cache, service, or per-view appearance override is introduced.
3. No tool, authorization, approval, network, timeout, persistence or output
   policy changes. Native replay uses a separate synthetic-data instance.
4. All edited source files stay within the hard size limits. Every edited
   handwritten C# file contains one type, and visual controls remain wrappers.
5. Eight new real-window headless cases cover both themes, Segoe UI and Inter,
   two content widths, inherited fonts, shared typography and unchanged anchors.
   Existing titlebar tests retain pointer, keyboard, Escape, resize, hidden-input
   and workspace/settings navigation-isolation coverage.

## Shared styles

Menu triggers use font size 14 and horizontal padding 6 instead of 7. They retain
their 40x30 Chinese-label hit areas. The common titlebar-menu inset changes from
9 to 10, and the permanent-wide-settings inset from 4 to 5, compensating for the
padding change without shifting the first menu's ink. The existing 13-DIP spacing,
caption buttons, history actions, flyouts and navigation rules are unchanged.

Only the visual-style heading uses font size 14, line height 20 and margin
0,41,0,16. Its typography override follows the generic settings-section-title
variant, which remains 13 for other sections. Putting this override in the earlier
layout include initially allowed the generic style to overwrite it; rendered
tests caught that cascade defect. The layout variant owns only the margin.

The title remains 28 with its existing font metrics. For Segoe UI the mode picker
still starts at pane y=115, stays 78 high, and precedes the palette card by 16.
Inter keeps its own natural title metrics rather than acquiring a forced Segoe
height. The default font service, Chinese fallback and global antialiasing mode
are not changed. An external probe disproved the idea that setting Avalonia title
LineHeight=34 alone would move its glyphs upward; no title transform is added.

Test drafts contained incorrect assumptions about FontFamily.Name and the full
palette wrapper's status area. Final cases compare FontFamily values and measure
the actual 226-high AppCard. A temporary production-fixture version also allowed
asynchronous default-font application to replace the test's Inter resource; the
final isolated presentation test needs no provider fixture or command execution.
These failed drafts and the cascade failure are retained as development evidence,
not counted as successful acceptance runs or repaired by weakening product code.

## Native and pixel evidence

External scripts and artifacts: `%TEMP%/MossAgent-settings-typography-20261015`.
Final native receipts and captures are in its `final-native` subdirectory.
Independent Debug/JIT build: `.scratch/settings-typography-final-20261015`.
Final App DLL SHA256:
`3AF85B79BD10B41F3BF13926A96AEA18D2DE6A381A15FEC486AFFD870225FD07`.

Ordinary-permission PID 18296 produces ten accepted settings captures at 96 DPI:
1920x1032, 1280x820 and 526x601, light/dark themes, real File popup, compact drawer,
Escape dismissal and return to maximized Light. An additional workspace frame
records the initial input diagnostic and is not settings-page evidence. The
earlier intermediate process exits zero before the final style build and has no
accepted final captures. The final process also exits zero with empty stderr.

The passive Python helper checks executable/PID/hash, captures only the owned
window and popups, and rejects black, minimized or undersized frames. Its audit
confirms nineteen unchanged database table hashes, zero artifacts, byte-identical
restored Light/default-font preferences and process/window disappearance.

The unscaled comparison uses the saved full-size Codex reference from
`MossAgent-child-settings-runtime-20261014`, the preceding accepted MossAgent frame,
and final `03-light-system-reference.png`. Threshold-192 ink measurements show:

- Visual-style heading: 52x12 at (747,201) becomes 56x12 at (747,202), matching
  reference size and position.
- File menu: 26x12 becomes 28x12 at (83,12), matching reference size and position.
- Mode-label, palette and title crops have zero changed pixels against the
  preceding accepted MossAgent frame; this does not mean they match Codex.

Residual differences remain explicit: Edit starts one pixel later than reference,
View two and Help two; Help ink width differs by one. Title ink is still two
pixels lower and one pixel shorter, and several palette labels remain one pixel
higher. Theme previews, actions, preset choices and other surfaces are not made
equivalent by this typography change.

Fresh passive Codex is version 26.1007.9446 in a 481x601 conversation window,
not an equivalent Appearance state. It is not substituted into the full-size
comparison. Initial native rail-settings clicks and a keyboard shortcut did not
prove a page transition; actual settings access is verified via File > Settings.
This slice does not certify that separate rail-input path.

## Validation and remaining gates

The earlier broad focused run passes 114 cases. The complete solution then runs
6,099 cases: 6,096 pass and three fail, with no skipped or aborted cases. App passes
4,474/4,477; visual 84, Agent 119, Browser 76, Infrastructure 199, Windows 137,
Providers 516 and Tools 491 all pass.

Two failures are old source guards for menu padding and settings margins. Their
expected values are updated without removing the shared-style, geometry, color
or wrapper-boundary checks. A final 30-case focused run passes on the updated test
assembly, covering both corrected guards, all eight new typography cases and
existing titlebar, appearance and content-alignment cases. The complete App suite
is not rerun after these test-only guard updates; its original result is retained
and is explicitly not all-green.

The third failure is the existing SidebarFilterAllocationTests project/matching
case: 3,112 allocated bytes instead of zero. Three isolated reruns of its entire
six-case class all pass on the final test assembly. No allocation implementation,
assertion, threshold or test exclusion is changed. Those eighteen cases and both
focused subsets are not added to the 6,099-case complete-run total. The allocation
failure remains unexplained, not claimed fixed by a typography change.

The final solution build has zero warnings/errors. Scoped whitespace formatting,
source size/type checks and workspace diff whitespace checks pass. External
verification records the distinct pre/post-guard test-assembly identities,
unchanged seven other suite assemblies, identical final native App hash, capture
metadata, source hashes, normal cleanup and byte-restored preferences.

Current-version same-state references, title/rasterization differences, remaining
menus, other surfaces/states, real DPI transitions and full-product acceptance
remain open. No AOT, JSON or packaging changes are made. Nothing is staged,
committed, pushed, released or installed; the overall goal remains active.
