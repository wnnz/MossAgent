# Native settings interaction progress

## Scope and design checks

This continues `SETTINGS_NAVIGATION_INTERACTION_ACCEPTANCE.md`. Native evidence
is version-specific; successful new previews do not repair the earlier resize
defect or complete the product goal.

1. App owns the settings title. Change only the shared settings-sidebar-title
   appearance variant, not individual views or platform-neutral modules.
2. Reuse AppText and existing settings/navigation commands. No new port,
   platform hook, layout workaround or duplicate component is needed.
3. No tool execution, model request, approval policy or persistence schema changes.
   Native checks use independent temporary fixture storage. Python captures and
   audits passively; Computer Use inputs target only the verified MossAgent window.
4. Keep one type per handwritten C# file and existing size limits. The shared
   style and extended shell-layout tests remain below 100 lines each.
5. Require a red/green font-weight regression in both themes at three widths,
   pixel regression and solution build. Align title ink without moving search/row geometry,
   then compare a fresh native title crop with the passive Codex reference.

## Native replay of the navigation candidate

Evidence remains outside Git at
`%TEMP%\MossAgent-settings-shell-20261013-134013\after`.
The candidate is Debug/JIT, App SHA256
`1D35374501158D3B03DF81A9E47963BE4B0C29C14091F6AB746004A49BC8BFBC`,
launched from `.scratch/settings-interactions-frame-20261013` with independent
data. PID13316/HWND15796302 are checked against the executable and actual pixels.

- Empty search hides all groups and shows the localized empty result; searching
  for Browser leaves only its Integration group and retains Appearance content.
  Clearing search restores the seven items and three groups. A set-value timeout
  is reobserved, not retried blindly; the actual query is confirmed before input.
- Ctrl+N leaves settings for a new, unpersisted conversation. A synthetic unsent
  draft survives settings return and history back/forward. Ctrl+Enter from the
  focused settings search does not submit that hidden draft.
- History back/forward buttons leave settings for the requested conversation.
  An attempted Alt+Left does not navigate: that gesture is not registered. Its
  image is retained but excluded from successful history evidence.
- Appearance renders at 1920x1032 in both themes, restores to 1280x820 in Dark,
  resizes through the real grip to 980x760, switches to Light, maximizes and
  restores to 980x760 again. Caption, content and settings anchors visibly reflow.
  These are successful 96-DPI checks, not a native high-DPI or root-cause repair.
  An attempted outward drag is rejected by Computer Use's window bounds; the
  unchanged 980x760 frame is not mislabeled as a successful 1280x820 resize.
- Passive PrintWindow/screen captures are non-black and have no trailing padding.
  Both the old PID14468 preview and this candidate close normally with exit zero;
  their observers also exit zero. No force-stop or duplicate preview is used.
- Read-only hash audits retain all nineteen schemas and original rows, three
  artifact hashes and the four-request history; UI settings return to Light.
  No new task row or model request is persisted. The observer's bounded lifetime
  expiring without requests is not new scheduler/cancellation acceptance.

## Bounded reference comparison and title correction

`after/native-shell-comparison.png/json` compares two native, light, maximized,
96-DPI 1920x1032 frames without resizing. The reference remains Codex General,
not Appearance. Rail, sidebar, search and selected-row RGB samples match exactly.
Search solid-color bounds are (60,96)-(307,132), and selected-row bounds are
(60,178)-(307,208) in both frames. Matching these bounded anchors does not imply
matching text, icons, content or the whole window.

The comparison exposes a title-weight difference hidden by palette/geometry-only
checks. At RGB channels below140 in the title-only crop, Codex's Settings title
has 354 ink pixels and bounds (68,61)-(104,78); the previous Moss title has
198 pixels and bounds (69,63)-(104,79). The shared title changes from SemiBold
to Bold while retaining its 18px size and 28px line height. A fresh native crop
shows the title still two pixels too low, so its shared top/bottom margins become
10/14 rather than 12/12. Their total remains24: surrounding row/search geometry
does not move. The first Bold crop still differs in glyph rasterization; neither
the weight change nor a matching ink bounding box establishes exact text pixels.
Six strengthened shell tests fail against the old weight before the change.

## Corrected-title verification

The final Debug/JIT App in `.scratch/settings-title-fidelity-20261013` has SHA256
`CD5FD637D11AADEF8A0379E56E3234B7B044A1E7E0CF66BBC8E50736B8642BC8`.
The final 42-case shell/search/navigation/frame regression and all 48 pixel cases
pass with zero failures/skips. Solution build has zero warnings/errors. Fresh
Headless frames use actual scaling notifications at1/1.25/2, not resized native
screenshots. TRX/frame evidence remains in the external title-tests and
title-rendered-final directories. The previous complete 5,804-case run applies
to the preceding navigation checkpoint, not this final title assembly; all eight
assemblies are not rerun for this two-setter style change.

The independent title-final fixture launches PID23528/HWND6095994 with the
verified final hash. Passive captures confirm Light/Dark at1920x1032, Dark
restore to1280x820, and Light at the restored size, all at96 DPI and ordinary
privilege. Actual pixels and search/theme controls are verified before accepting
the title frame. Earlier workspace frames fail the solid-region comparison gate
and are not reported as settings references. The first Bold-only candidate is
separately versioned; neither it nor the navigation candidate is the final binary.

`title-final/title-ink-comparison.json` records identical title ink bounds
(68,61)-(104,78) without resizing. Mean absolute RGB difference in the fixed
80x35 title crop drops from about16.7 to11.3; dark ink remains258 pixels versus
Codex's354. This improves weight/alignment but does not establish exact glyph
rasterization. The four shell colors and both solid-region bounds still match.
The comparison PNG is `title-final/native-shell-comparison.png`.

The final preview and observer exit normally with code zero. All observed preview
PIDs are absent, observer port51792 is released, final App hash is unchanged,
and the post-close read-only audit retains all original schemas/rows, artifacts,
four requests and Light UI state. The same preservation checks pass for the
first title candidate. No paths are staged. Python scripts and images stay
outside the repository. Scoped whitespace/diff checks pass.

## Remaining gates

Complete equivalent Codex Appearance and PI-Desktop references, native high-DPI
transitions, unsaved provider-editor/background-update interaction, other key
surface/state combinations and the resize root cause before product sign-off.
The latest passive Codex rechecks show the workspace, not Appearance, and are
not silently substituted. No Codex input, security/approval setting change,
default executable replacement, JSON/AOT/packaging change, commit or release occurs.
