# Appearance theme palette picker acceptance

## Scope and design boundaries

The saved 1920x1032, 96-DPI Codex Appearance reference has a 176x28 theme pill,
a circular typography mark and two preceding action positions. MossAgent had a
static light/dark title and only reset/apply buttons. This slice adds a real
default/current-custom palette selector, not a fabricated theme catalog or
unverified import/export/clone behavior.

1. App owns presentation and selection. It uses IThemeService and the existing
   per-theme appearance store; module dependencies remain unchanged.
2. Existing palette validation, SaveCommand and ResetCommand remain authoritative.
   A current custom option contains an already validated immutable ThemePalette.
3. No tools, commands, files, HTTP or SQL are accessed by UI code. There are no
   approval or security setting changes; verification uses an isolated synthetic
   data directory. Persistence failures show an error without claiming saving.
4. Controls use shared wrappers and appearance variants. The largest changed C#
   file has 217 lines; each handwritten C# file has one type. No AOT, JSON
   source-generation or publishing changes are introduced.
5. Tests cover popup/keyboard behavior, disabled keyboard input, invalid drafts,
   foreign options, save failures, restart loading and per-theme/font isolation.
   Geometry cases cover both themes at 320/728 widths.

## Implemented behavior

- The pill offers only default colors until a custom palette really exists.
  Saving colors or changing the accent updates the current-custom option.
- Selecting default applies that theme's defaults; choosing the retained custom
  option restores its committed background, foreground and accent. Fonts and
  the other theme remain untouched. Accent updates do not discard color drafts.
- The last custom palette is retained only for the editor's lifetime. Restart
  reconstructs the currently persisted palette, not an archive of old presets.
  Resetting to default and then restarting does not recover a prior custom theme.
- Unknown/null options cannot apply colors or discard drafts. Save/Reset remain
  localized and usable with honest refresh/check glyphs, not unidentified actions.
- Shared geometry is 176x28 with a blue 20x20 Aa mark. Below 352 card width the
  pill contracts to 144 pixels. Standard 32x32 icon hit areas and eight-pixel
  gaps align the action centers with the reference without shrinking their ink.

## Final native evidence

Final artifacts: `.scratch/theme-preset-final-20261015`.
Production App SHA-256:
`97011DC1809F9F72EEEAE999C95B81ED986C0DA6EFF88EB0A27679F32740F5D7`.
External evidence:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-theme-presets-20261015\final`.

Python PrintWindow binds exact PID/path/hash, rejects black pixels, records
ordinary permissions and 96 DPI, and composites only same-process popups.
Fourteen accepted settings captures cover maximized 1920x1032, normal 1280x820,
minimum 526x601, light/dark, the real popup, default selection, keyboard custom
restoration, isolation, reset and normal close/reopen. Three frames show failed
initial settings navigation and are excluded from settings acceptance.

Native PIDs 20556/19548 exit normally with code zero and empty stderr. Their
HWNDs/processes disappear. All 19 synthetic table hashes remain unchanged, no
artifacts are produced, reopened custom preferences remain unchanged, and final
Light/default-font preferences exactly match the original bytes. `audit.py`
independently checks these facts and writes `native-audit.json`.

`theme-header-comparison.png` contains unscaled reference/before/after strips.
Its manifest records paths, hashes and a fixed crop. The reference is the
previously captured Codex Appearance frame, not a new live capture or proof of
whole-window equivalence. Labels and action glyphs still differ intentionally.

## Validation and correction history

Initial and final focused runs both pass 59 cases, including 18 new cases.
Initial native checks expose tight action spacing and a gray Aa mark. Final
shared styling corrects both and adds compact geometry. A missing
Avalonia.Controls extension import initially prevents compilation; the import
fixes it without weakening build guards.

The initial complete-App run belongs to the separate C80A9588 candidate. It sees
a later source-guard change, fails that stale guard and is interrupted; it is not
a passing run or final evidence. Initial native PID 10076 exits zero. Final
solution build passes with zero warnings/errors. Scoped whitespace formatting
and git diff whitespace checks pass.

External replay assertions initially use the wrong saved accent field and expect
a null palette object instead of the store's removed key. Corrected checks use
the existing Accent field and key-removal semantics; no product or data changes
are needed. An out-of-bounds resize input is rejected, followed by fresh window
observation before the corrected window-relative input.

The complete final solution run passes 6,083 cases across all eight assemblies:
App 4,461, visual 84, Agent 119, Browser 76, Infrastructure 199, Windows 137,
Providers 516 and Tools 491. There are no failures, skips or aborted cases.
The focused 59 cases are included in App and are not added to this total.
Full catalogs, reference action semantics, export/import/clone, title ink, high
DPI/native geometry and complete product acceptance remain open. Nothing is
staged, committed, pushed, released or installed. The overall goal stays active.
