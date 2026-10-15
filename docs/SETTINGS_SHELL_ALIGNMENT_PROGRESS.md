# Settings shell alignment progress

## Scope and design checks

This slice aligns the shared settings shell with a passive light Codex General
reference. General content is not an Appearance reference; no whole-page or
whole-product pixel-equivalence claim follows from these shell measurements.

1. App owns view composition and UI navigation. Domain, Application, execution
   adapters and their dependency directions remain unchanged.
2. Reuse AppNavigationRail, existing settings/inspector commands, the automation
   page command and shared window frame variants. No new IO port is needed.
3. Navigation performs no model request or tool execution and changes no approval
   policy, authorized root, timeout or output limit. Fixtures use independent
   temporary storage and deterministic loopback services.
4. Keep one type per handwritten C# file and remain below file/method limits.
   Add shell tests separately instead of expanding the nearly full geometry file.
5. Render both themes at narrow/normal/wide widths. Prove return/automation/audit
   navigation preserves task, project, model and unsent draft; reject invalid tabs
   without state changes and close flyouts when their page becomes hidden.

## Reference and capture limits

Evidence remains outside Git in
`%TEMP%\MossAgent-settings-shell-20261013-134013`.
The Codex General frame is light, maximized, 1920x1032 and 96 DPI. Its shared shell
has a 52px rail, 264px sidebar, search bounds (60,96,247,36), and the first
navigation row at (60,178) with height 30. Sidebar/selected-row samples are
`#F9FBFD`/`#ECEFF1`; the rail sample remains `#EEF4F9`.

The earlier `before/light-settings.png` actually shows the workspace at 120 DPI,
not settings. The fresh passive PrintWindow attempt reports 1920x1020 native
bounds but only 1536x816 content with black padding and is rejected. Neither is
a settings baseline. Sky also returned Codex pixels for MossAgent metadata;
inputs remain stopped until identity can be established safely. These capture
failures do not establish a renderer root cause or repair the native restore bug.

Appearance content sizing, provider/model editor ownership and full-page dialogs
remain separate from the shell. The native theme/size/state matrix, equivalent
Appearance/PI-Desktop references and full-product acceptance are still open.

## Implemented and verified

- Settings now uses the shared 52px rail, 264px sidebar and outer frame/inner
  rounded clip. Shared title/search/heading variants reproduce the measured
  shell anchors. Only the settings selection token changes; rail/card palettes
  and existing shared card shadows stay unchanged.
- The settings home action returns without creating a task or sending a draft.
  Automations and valid inspector selections leave settings before navigating;
  invalid tabs leave state untouched. The rail suppresses hidden-page selection
  in settings and closes flyouts when its owning page disappears.
- Appearance's shared 768px maximum stays unchanged. Headless column anchors
  move to x728 at width1919 and x409 at width1280. At width980 they are x344 and
  width598, without horizontal scrolling. These are current shell-layout
  regressions, not measurements from an equivalent Codex Appearance page.
- The final isolated Debug App assembly SHA256 is
  `52DE3D413749348C8752265AF2C62BCEFA0F5C81978849CA4D86D636E271E019`.
  It is built, not natively launched or substituted for the default executable.

Validation sequence and limits:

1. Sixteen new focused cases initially produce 13 failures/3 passes, exposing
   missing shell/navigation behavior while invalid-tab guards already pass.
   Initial test-code import/analyzer errors are repaired before this baseline.
2. Final shell/navigation/alignment/rail/geometry App group passes 71/71.
   The first broader run passes 1463/1466: two assertions still expect the old
   settings color, and the unchanged warm-allocation test reports 5256 bytes.
   The color expectations are updated from the reference; the allocation test
   is not changed and passes an isolated rerun. The final serial wider run
   passes 1466/1466 with no failures or skips.
3. All 48 pixel cases pass. Ten new settings cases cover both themes, 980x640,
   1280x820 and 1920x1032, with 1x/1.25x/2x Headless scaling cases. They verify
   actual render scaling, physical frame size, search/selection/sidebar colors,
   page color, upper/left shadow and rounded corner clipping. The first fixture
   omitted inactive-page flags and its offscreen scaled bitmap distorted cached
   child layers. The corrected fixture uses explicit flags, SetRenderScaling
   notifications and CaptureRenderedFrame. This is test-fixture repair, not a
   native renderer or restore-defect repair.
4. Solution build passes with zero warnings/errors. Scoped dotnet whitespace
   verification and Git diff whitespace checks pass. App.axaml remains 498
   physical lines, MainWindowViewModel 453 and ShellGeometryTests 493.
   Seven architecture constraints pass separately in the Agent assembly.
   Complete runs of the other six test assemblies and the full App assembly are
   not performed here.
5. External Python creates `shell-comparison.png/json`. Codex is a passive native
   General frame; Moss is a Headless Appearance frame. Four flat-color samples
   agree exactly, but labels/content differ and native/whole-window equivalence
   is explicitly false. The screenshot/comparison scripts and PNGs stay outside
   the repository.

The independent old-binary preview's 19 SQLite tables retain their schemas/rows,
integrity passes, Light remains selected, three original artifact hashes/audits
are unchanged and the request history stays at four. The legacy scheduler audit
returns one expected false flag for fresh cancellation disconnect: no cancellation
is executed in this read-only preview, so that audit is not a new scheduler gate.
The preview has not been confirmed closed; its launch monitor remains pending.
No new after-preview is launched while the target binding is unsafe. Native
settings, search/editor/return interactions, theme/size/DPI acceptance and normal
preview shutdown still require a trustworthy target or manual user handoff.
