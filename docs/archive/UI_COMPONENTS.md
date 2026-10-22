# 共享控件与外观

> 历史归档：保留原报告当时的结论、实验、失败与限制。整理于 2026-10-21。
> 当前能力与未完成方案统一维护在[功能清单](../FEATURE_STATUS_AND_IMPLEMENTATION_PLAN.md)；
> 原文中的计划、主机状态、测试数字及“正在进行”等措辞均为历史检查点。
> 代码块和仓库相对产物路径保留原意；本机忽略的 `artifacts/` 不随克隆分发。

[文档入口](../README.md)

## 原报告索引

- [Appearance detail fidelity acceptance](#appearance-detail-fidelity-acceptance) — `APPEARANCE_DETAIL_FIDELITY_ACCEPTANCE.md`
- [Appearance mode picker acceptance](#appearance-mode-picker-acceptance) — `APPEARANCE_MODE_PICKER_ACCEPTANCE.md`
- [Appearance palette layout acceptance](#appearance-palette-layout-acceptance) — `APPEARANCE_PALETTE_LAYOUT_ACCEPTANCE.md`
- [Appearance selector acceptance](#appearance-selector-acceptance) — `APPEARANCE_SELECTOR_ACCEPTANCE.md`
- [Appearance theme palette picker acceptance](#appearance-theme-picker-acceptance) — `APPEARANCE_THEME_PICKER_ACCEPTANCE.md`
- [Appearance thumbnail acceptance](#appearance-thumbnail-acceptance) — `APPEARANCE_THUMBNAIL_ACCEPTANCE.md`
- [统一附件组件验收](#attachment-component-acceptance) — `ATTACHMENT_COMPONENT_ACCEPTANCE.md`
- [Shared button focus shape](#button-focus-appearance-acceptance) — `BUTTON_FOCUS_APPEARANCE_ACCEPTANCE.md`
- [Shared checkbox focus appearance](#checkbox-focus-appearance-acceptance) — `CHECKBOX_FOCUS_APPEARANCE_ACCEPTANCE.md`
- [输入区上下文条组件验收](#composer-context-component-acceptance) — `COMPOSER_CONTEXT_COMPONENT_ACCEPTANCE.md`
- [对话标题栏共享组件验收](#conversation-header-component-acceptance) — `CONVERSATION_HEADER_COMPONENT_ACCEPTANCE.md`
- [会话标签共享组件验收](#conversation-tab-component-acceptance) — `CONVERSATION_TAB_COMPONENT_ACCEPTANCE.md`
- [Shared danger-button theme repair (2026-10-13)](#danger-button-theme-acceptance) — `DANGER_BUTTON_THEME_ACCEPTANCE.md`
- [Default UI font fidelity](#default-ui-font-fidelity-acceptance) — `DEFAULT_UI_FONT_FIDELITY_ACCEPTANCE.md`
- [Wrapped input and checkbox hit-target acceptance](#input-hit-target-acceptance) — `INPUT_HIT_TARGET_ACCEPTANCE.md`
- [Navigation rail component acceptance](#navigation-rail-component-acceptance) — `NAVIGATION_RAIL_COMPONENT_ACCEPTANCE.md`
- [共享数字输入组件验收](#numeric-field-component-acceptance) — `NUMERIC_FIELD_COMPONENT_ACCEPTANCE.md`
- [侧边栏项目行组件验收](#project-row-component-acceptance) — `PROJECT_ROW_COMPONENT_ACCEPTANCE.md`
- [Shared selector keyboard-focus appearance](#selector-focus-appearance-acceptance) — `SELECTOR_FOCUS_APPEARANCE_ACCEPTANCE.md`
- [设置列表统一组件验收](#settings-item-component-acceptance) — `SETTINGS_ITEM_COMPONENT_ACCEPTANCE.md`
- [Shared field focus and disabled primary states](#shared-interaction-states-acceptance) — `SHARED_INTERACTION_STATES_ACCEPTANCE.md`
- [共享导航字号与分组文字验收](#shared-navigation-typography-acceptance) — `SHARED_NAVIGATION_TYPOGRAPHY_ACCEPTANCE.md`
- [Sidebar search component acceptance — 2026-10-08](#sidebar-search-component-acceptance) — `SIDEBAR_SEARCH_COMPONENT_ACCEPTANCE.md`
- [Theme thumbnail artwork acceptance](#theme-thumbnail-artwork-acceptance) — `THEME_THUMBNAIL_ARTWORK_ACCEPTANCE.md`
- [主题缩略图阴影验收](#theme-thumbnail-shadow-acceptance) — `THEME_THUMBNAIL_SHADOW_ACCEPTANCE.md`
- [统一 UI 组件验收](#ui-component-acceptance) — `UI_COMPONENT_ACCEPTANCE.md`
- [工作区面板组件验收](#workspace-pane-component-acceptance) — `WORKSPACE_PANE_COMPONENT_ACCEPTANCE.md`
- [工作区欢迎标题与权限组件验收](#workspace-prompt-component-acceptance) — `WORKSPACE_PROMPT_COMPONENT_ACCEPTANCE.md`

---

<a id="appearance-detail-fidelity-acceptance"></a>

## APPEARANCE_DETAIL_FIDELITY_ACCEPTANCE.md

<!-- source: APPEARANCE_DETAIL_FIDELITY_ACCEPTANCE.md -->
### Appearance detail fidelity acceptance

#### Design self-check

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

#### Reference and intended correction

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

#### Implementation and regression record

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

#### Native evidence and measured limits

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

#### Final checkpoint

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
<!-- /source: APPEARANCE_DETAIL_FIDELITY_ACCEPTANCE.md -->


---

<a id="appearance-mode-picker-acceptance"></a>

## APPEARANCE_MODE_PICKER_ACCEPTANCE.md

<!-- source: APPEARANCE_MODE_PICKER_ACCEPTANCE.md -->
### Appearance mode picker acceptance

#### Design self-check

1. App owns this rendered layout. One shared `AppThemeModePicker` replaces the
   appearance view's duplicated choice markup; no dependency direction changes.
2. Reuse the real `SelectThemeCommand`, selected-state properties, existing scene
   renderer and appearance persistence. No second theme service or state cache.
3. Only synthetic preferences change during native replay. Tool authorization,
   approval, cancellation, timeouts, outputs and JSON/JIT publishing stay intact.
4. New handwritten types each occupy their own small file; shared styles own
   button dimensions and selection appearance, not per-view visual overrides.
5. Actual rendered bounds cover light/dark and two widths; keyboard tests use the
   production command. Existing shell, focus, appearance and pixel gates remain.

#### Reference and correction

Fresh Python passive capture finally obtains the real Codex Appearance page,
1920x1032 at 96 DPI, with System selected and the effective light palette. This
is a settings reference, unlike the earlier conversation captures. Codex receives
no automated input. Both production MossAgent captures use the same viewport,
effective theme and System selection; they are not entire-page equality claims.

- The old three-column previews have 174-DIP hit-area height and captions below.
  Four new geometry cases fail before the product edit, in both themes/widths.
- The shared picker places three equal 80x60 buttons in one compact Mode row,
  separated by 16 DIP. Selection uses the blue outline; existing native button
  focus, commands, names and tooltips are retained. New labels use resources.
- The existing 248x174 scene is scaled inside each button; it is not deleted or
  recreated as a screenshot asset. Unused large-choice styles and view markup
  are removed. This intentionally does not yet redraw the thumbnail contents.
- Native blue-pixel extents independently measure 80x60 for Codex and MossAgent.
  Their page positions differ: reference (1186,242), candidate (1208,221). The
  full-page width, title, palette organization and advanced disclosure still
  differ. They remain work, not claimed fixed by matching button dimensions.

#### Verification record

The first candidate's invalid `BoxShadow="None"` is caught by loading shared
styles (28 failures / 19 passes); the invalid override is removed, retaining the
existing shared surface shadow. The next 480-case group has six failures because
the old focus scenario assumes the first accent selector remains offscreen after
compact resize. With the shorter picker it is correctly visible. The scenario now
targets a UI-font selector that is verified visible before resize and offscreen
after it, preserving all dismissal and no-scroll assertions. No focus policy is
weakened or changed. The final related run passes 480 cases with no skips/failures.
The first full App run also encounters the old literal section-heading guard;
it is updated to check the new localized Visual Style resource binding while
retaining its shared typography/margin assertions. An attempted rebuild before
that test process exits fails with MSB3027/MSB3021 on the test apphost. Its log
is retained separately; it is not reported as a clean build or a product defect.
That complete run finishes with 4,394 passes and the one heading-guard failure,
not an all-green checkpoint. The repaired guard is followed by a separate serial
complete run; historical related-group identities are retained independently.

The production candidate is built separately under `.scratch/appearance-mode-20261014`;
it does not replace the default launch output. App SHA-256 is
`F83A6B6B10AE0B76373A93DC7DAC5F92FE974E7342B411BBF27AC35469D04FC4`.
Ordinary-permission native replay verifies System/light and Dark at 1920x1032,
then Dark and Light at the actual 526x601 minimum through a native grip resize.
All choices fit and the selected outline follows the clicked button. Synthetic
preferences return to Light; model requests and saved database rows do not change.

External evidence, scripts and synthetic data stay outside Git:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-child-settings-runtime-20261014`.
`appearance-comparison.png` contains unresized source crops, with reference,
before and after clearly labeled. `appearance-measurements.json` records the
native selection extents. These crops do not establish full-page pixel parity.

#### Separate child-settings probe

Before this UI change, the preceding production App identity `A3B7F0C...` edits
one synthetic child through the real settings page: name, trigger, prompt, Gemini
model and high effort. The invalid empty-name save preserves all nineteen table
hashes; explicit repair succeeds. Sharing remains off and the seeded task retains
AskEveryTime throughout. The subsequent real parent request contains the saved
name and trigger. Its child tool waits for manual approval and sends no child
request. No approval is automated or permission policy raised. Without a user
approval reply, normal Stop cancels the request: `tool_cancelled`, no granted
approval, task Cancelled, exactly one parent request and zero child requests.

This verifies settings persistence, catalog publication, approval gating and Stop,
not a successful current-version child run. The newer picker App reopens that
history without a new model request. A fresh successful settings-to-child run
still requires manual approval. The fresh earlier focused App and Agent probes
pass 164 and 119 cases respectively; they are not promoted to native success.

#### Final checkpoint

The repaired complete serial App run passes all 4,395 cases with zero failures,
skips or aborts. The complete pixel suite passes 84 and the fresh candidate Agent
suite passes 119: 4,479 UI cases and 4,598 across these three assemblies. The
480-case related run is a subset, not additional coverage in that total; its
pre-heading-guard assembly identity remains recorded separately. Five other
non-UI suites are not rerun for this App-only change, and no historical complete
solution test count is promoted to this checkpoint.

The final solution build succeeds with zero warnings/errors after the locked
driver exits. Scoped formatting and all eighteen explicit source/document paths'
whitespace checks pass, including untracked files; the index remains empty.
The native App exits normally with code zero. Independent Python and PowerShell
checks agree on final TRX counts and the production App identity. The external
`verified-checkpoint.json` also verifies native capture metadata, restored-Light
preferences, all nineteen unchanged table hashes, no artifacts, one parent/zero
child requests, the cancelled unapproved tool audit, and released processes/port.

The default launch output, user data, JIT/JSON behavior and packaging are not
changed. Nothing is staged, committed, pushed, released or installed. Full-page
Appearance fidelity, a fresh manually approved settings-to-child success and
the remaining product gates stay open; the overall goal remains active.
<!-- /source: APPEARANCE_MODE_PICKER_ACCEPTANCE.md -->


---

<a id="appearance-palette-layout-acceptance"></a>

## APPEARANCE_PALETTE_LAYOUT_ACCEPTANCE.md

<!-- source: APPEARANCE_PALETTE_LAYOUT_ACCEPTANCE.md -->
### Appearance palette layout acceptance

#### Design self-check

1. App owns this appearance-only layout. Shared controls and style variants own
   rows, selectors and disclosure presentation; no module dependency changes.
2. Keep both existing palette/font editors and their real save/reset/selection
   commands. The basic card follows the effective theme through MainWindow's
   existing theme notification, without a new subscription or cache.
3. This page changes only appearance preferences. It does not change tool
   permissions, approvals, model requests, user databases, JIT/JSON or publishing.
4. New handwritten types have their own small files. Shared disclosure markup
   retains its content on collapse, rather than rebuilding editor state.
5. Rendered tests cover both themes and widths, default-collapsed content,
   keyboard disclosure, preserved drafts, actual theme routing and save errors.
   Existing settings focus, palette/font persistence and visual gates remain.

#### Reference and scope

Use the Python passive 1920x1032, 96-DPI Codex Appearance reference recorded in
[APPEARANCE_MODE_PICKER_ACCEPTANCE.md](UI_COMPONENTS.md#appearance-mode-picker-acceptance). Its basic card contains five rows below
the mode picker; advanced settings are initially collapsed. MossAgent currently
shows a Diff preview followed by both full palette editors, including extended
fonts. This change targets that hierarchy, not a new theme service or fake
preset/import/export controls. Codex's theme-preset actions and thumbnails, full
page positioning and high-DPI equivalence remain separate acceptance work.

#### Correction and regression record

The page now has one five-row basic card bound to `ActivePalette`. Its computed
selection uses the existing effective-theme state and notification, so System
follows the rendered light/dark theme. Both original palette/font editor instances
remain available through the mode choices; switching does not discard either
draft. Save/reset and immediate font/accent selection still use the existing
commands and persistence services. No fake theme-preset/import/export buttons are
introduced. Extended fonts and the read-only Diff move into one shared initially
collapsed `AppExpander`; subsequent collapse retains the realized controls.

Four initial rendered cases fail against the old two-card page. The first shared
template build catches missing explicit template/setter target types. Later tests
expose inherited Expander alignment restricting expanded content to its 577-DIP
natural width: the shared wrapper/template now stretch both the host and content.
The expected full-width Diff assertions remain in place at narrow and wide sizes.
Keyboard tests send both Space down and up. Existing focus scenarios explicitly
expand advanced content, retain font selection and color drafts, and establish
that a remembered editor is visible at 1032-DIP height before compact resize;
their no-scroll, dismissal and lifetime assertions remain intact.

The initial native disclosure reports its StackPanel as the accessible name.
Explicit binding to Header repairs the shared template; the final real tree
reports `button 高级 ID: PART_toggle`. Rendered tests additionally cover disabled
click rejection/re-enable, arrow state, retained controls, effective System mode,
independent theme drafts and unreadable-color rejection/recovery through the real
save button. An obsolete source guard requiring extended fonts inside each basic
card is updated to enforce the new shared advanced ownership instead.

The final template's focused group passes 79 cases. Both complete runs made
before the notification-guard correction pass 4,407 cases and fail one: the old
exact MainWindow notification list omits `ActivePalette`. Their failed reports
are retained separately and are not passing checkpoints. The guard is extended,
with a selected-editor identity assertion; all original notification and disposal
checks remain. The rebuilt focused group, including all three lifetime cases,
passes 82 cases. This is a test expectation correction, not a suppressed failure
or change to the theme service/lifetime policy.

#### Final checkpoint

After rebuilding the corrected guard, the serial complete App run passes 4,408
cases with zero failures or skips. The other seven complete assemblies pass
1,622: pixels 84, Tools 491, Providers 516, Agent 119, Infrastructure 199, Browser
76 and Platform.Windows 137. The total is 6,030 across eight assemblies; the
82-case focused group is a verified subset, not an additional test count.

The final solution build has zero warnings/errors. Scoped whitespace formatting,
explicit checks of all twenty-one tracked/untracked paths and workspace diff
whitespace pass; the index stays empty. External Python verification checks the
TRX outcomes, assembly paths/hashes, native audit and sampled row measurements.
An independent PowerShell check agrees with the counters, confirms the running
test assemblies stayed unchanged, and verifies test/driver process exit. The
seven other test DLLs retain their recorded tested identities after the guard
rebuild, and the App retains the native-captured production hash below. Both
failed pre-correction complete runs remain separate from this green checkpoint.

#### Native evidence and limits

The final production App is built independently under
`.scratch/appearance-palette-final-20261014`. Its SHA-256 is
`BC55A95649D65362885E17EE37B57BD2156158748AC1835C781373A01D37E13E`.
Ordinary-permission native replay verifies System/effective light at 1920x1032,
pointer expansion and keyboard collapse, then Dark at the same viewport. A real
native grip drag reaches 526x601; both theme modes and scrolled advanced Diff/font
controls remain accessible. Light is restored and the App closes normally, exit
zero. No approvals or sharing settings are automated. The synthetic database is
a separate copy, not the user's data directory; all nineteen table hashes remain
unchanged, including the previously cancelled AskEveryTime task and tool audit.

External scripts, data and screenshots stay outside Git under
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-appearance-palette-20261014`.
`native-audit.json` checks identity, viewport/DPI/ordinary-permission metadata,
normal exit, restored preferences and unchanged tables. The opened content area
changes pixels; keyboard collapse restores the closed area's pixels exactly.
`palette-comparison.png` uses the same unscaled source crop coordinates for the
reference, preceding production version and final candidate. Display scaling of
that composed image is not a source-image size change.

Center-line pixel samples independently confirm a 226-pixel basic-card height and
matching row intervals of 48/44/44/44/45 in both 96-DPI images. Their absolute row
positions differ by 21 pixels. Dividers remain RGB 237 in the reference versus
241 in the candidate. These samples do not measure card width or prove complete
pixel equality. Column/title positioning, color-input pills, thumbnail contents,
theme-preset actions and the remaining full-product/state/DPI gates stay open.
The default launch output, JSON/JIT and packaging are not changed. This slice
does not verify a fresh successful child run or complete the overall goal.
<!-- /source: APPEARANCE_PALETTE_LAYOUT_ACCEPTANCE.md -->


---

<a id="appearance-selector-acceptance"></a>

## APPEARANCE_SELECTOR_ACCEPTANCE.md

<!-- source: APPEARANCE_SELECTOR_ACCEPTANCE.md -->
### Appearance selector acceptance

#### Design self-check

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

#### Reference and correction

The existing same-state passive Codex Appearance reference is 1920x1032 at 96 DPI.
Its accent pill outline spans (1380,384)-(1458,412), 78x28 pixels; the default font
pill spans (1398,516)-(1458,544), 60x28 pixels. The pre-change MossAgent selectors were
92 pixels wide, kept a separate accent swatch and used the larger native minimum
height. The default font's closed label was also longer than the reference.

Correct those controls through reusable shared components while preserving real
selection and error behavior. Theme presets/import/export, complete thumbnail
artwork, title rasterization, DPI and full-product acceptance remain separate open
gates. Exact selected samples are not whole-page pixel-equivalence evidence.

#### Implemented shared components

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

#### Corrections found during verification

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

#### Native and screenshot evidence

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

#### Final verification

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

#### Remaining gates

The 96-DPI samples establish this selector correction, not complete Appearance or
product equivalence. Title rasterization/vertical ink position, glyphs/arrows,
thumbnail artwork, theme presets/import/export, high DPI and the remaining full
surface/state matrix remain open. Current successful child runtime and PI-Desktop
provider-page equivalence remain separate product gates. No default launch output,
staging, commit, push, release or installation is changed by this checkpoint.
<!-- /source: APPEARANCE_SELECTOR_ACCEPTANCE.md -->


---

<a id="appearance-theme-picker-acceptance"></a>

## APPEARANCE_THEME_PICKER_ACCEPTANCE.md

<!-- source: APPEARANCE_THEME_PICKER_ACCEPTANCE.md -->
### Appearance theme palette picker acceptance

#### Scope and design boundaries

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

#### Implemented behavior

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

#### Final native evidence

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

#### Validation and correction history

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
<!-- /source: APPEARANCE_THEME_PICKER_ACCEPTANCE.md -->


---

<a id="appearance-thumbnail-acceptance"></a>

## APPEARANCE_THUMBNAIL_ACCEPTANCE.md

<!-- source: APPEARANCE_THUMBNAIL_ACCEPTANCE.md -->
### Appearance thumbnail acceptance

#### Design self-check

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

#### Reference and intended correction

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

#### Implemented shared correction

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

#### Correction history

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

#### Final native and measured evidence

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

#### Verification scope

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
<!-- /source: APPEARANCE_THUMBNAIL_ACCEPTANCE.md -->


---

<a id="attachment-component-acceptance"></a>

## ATTACHMENT_COMPONENT_ACCEPTANCE.md

<!-- source: ATTACHMENT_COMPONENT_ACCEPTANCE.md -->
### 统一附件组件验收

日期：2026-10-06。此记录只验收共享组件与使用边界，不表示整个界面已经与 Codex 逐像素一致。

#### 所有权与安全

- App 的 `AppAttachmentChip` 拥有文件图标、双行文字、长标签省略、完整路径提示、
  可访问名称和移除操作。输入区和消息区都复用它，不再保留两套手动拼装的附件卡片。
- 页面仅传 `DisplayPath`、`SizeLabel` 和可选 `RemoveCommand`。
  不传命令时隐藏移除按钮；命令暂不可执行时保留禁用状态。
- `AttachmentChipVariants.axaml` 维护 42 DIP 表面高度、14 DIP 文件图标、
  24×24 DIP 移除点击区、10 DIP 移除图标、11.5/9.5 DIP 文字、圆角和统一四周浅色阴影。
  消息和输入附件使用相同视觉规则，差异仅在是否提供移除操作。
- 复用已有附件 ViewModel 命令，不新增端口或文件访问，不改变依赖方向、授权目录、
  审批、超时、取消或输出限制，不涉及数据库迁移。组件代码没有文件、进程或 HTTP 操作。
- 每个新手写 C# 文件只含一个类型，方法与文件满足现有硬限制。

#### 边界与自动化

- `ValidateSharedAttachmentChips` 与 C# `MOSSUI002` 禁止业务代码覆盖内部 Content、
  Padding、尺寸和内容对齐。属性、原生所有者限定属性、属性元素、Avalonia 属性赋值
  与清除受到同一规则约束；数据、命令、可见状态和外部布局仍允许。
- 编译期原生视觉组件禁用规则仍生效：只允许共享 Controls 实现原生视觉控件，
  Grid、StackPanel 等原生布局元素只能承担结构布局，不在业务页定义独立外观。
- 实现前的 16 项标记回归因缺少统一组件和门禁而失败。实现后新增组件与边界测试
  31 项，覆盖绑定源隔离、命令可用性、Space 激活、动态替换与移除命令、消息只读状态、
  长中文路径、150/260/420 DIP 可用宽度及明暗主题。
- 布局回归发现奇数高度内容行的舍入使文件图标与移除按钮相差 1 DIP。
  调整共享组件的布局槽，使图标和按钮居中到同一个填充行，而不是添加业务页像素位移。
- App 中临时插入覆盖 Content 和 Width 的 AXAML，实际执行新构建目标按预期失败。
  临时 C# 构造原生 Button、覆盖附件 Width，实际编译分别报 `MOSSUI001`、`MOSSUI002`。
  两份探针已移除；这些预期拒绝不是最终源码的构建失败。

#### 普通权限运行截图

仓库外宿主使用实际 App/MainWindow 和独立临时数据库，设置合成消息、附件和草稿。
没有读取真实凭据、调用模型、自动连接 MCP 或操作真实项目文件。
通过真实附件移除命令验证集合变为空、`HasItems` 更新，再放入用于截图的短路径和长路径。
每个窗口实际显示四个组件：两个消息附件无移除操作、两个输入附件有移除操作。

目录：`%TEMP%/MossAgent-attachment-components-qa-20261006-01`。

| 最终截图 | 主题与原始像素尺寸 |
| --- | --- |
| `attachment-light-normal-final.png` | 浅色，1280×820 |
| `attachment-dark-max-final.png` | 深色，1920×1032 |
| `attachment-light-narrow-final.png` | 浅色，980×700 |
| `attachment-dark-narrow-final.png` | 深色，980×700 |

所有截图已查看，长路径省略不遮挡图标与移除按钮；原始截图没有拉伸或黑屏。
Python 只使用 `--passive --include-popups`，没有点击、窗口激活或调整其他应用。
运行日志均记录 `QA_ELEVATED: False`、`QA_REMOVE_VERIFIED: True`、`QA_CHIPS: 4`，
并核对实际表面与操作尺寸。宿主正常关闭窗口，日志记录关闭与退出完成。
脚本、宿主、临时数据和图片均不加入仓库。

#### 验收限制

最终完整解决方案 1,477 项测试全部通过（App 1,118 项），
解决方案构建 0 警告、0 错误。相关 C# 空白格式检查与 `git diff --check` 通过。
所有临时探针已移除、验收进程已退出，没有暂存、提交或推送。

- 消息和附件由宿主设置，移除是程序化执行真实命令；不是物理鼠标或系统键盘验收，
  也不是原生文件选择器验收。组件的键盘行为另有 Headless Space 激活回归。
- 本轮没有重新操作或获取 Codex 窗口，没有同一附件状态的 Codex 参考图，
  不把组件统一或尺寸断言当作附件外观与 Codex 一比一的证明。
- 尚未穷尽所有页面、DPI、悬停和错误状态。整体目标继续保持未完成。
- 全仓库审计 794 个 C#/AXAML 文件，未发现超过 500 行的文件或多类型 C# 文件。
  仍有 3 个本轮未改动的预先存在超长方法：`OpenAiCompatibleProvider.StreamAsync`，
  `ModelSettingsViewModelTests` 第 154 行和 `SubagentWorkspaceFlowTests` 第 21 行。
  不声称全仓库方法规模全部合格。
<!-- /source: ATTACHMENT_COMPONENT_ACCEPTANCE.md -->


---

<a id="button-focus-appearance-acceptance"></a>

## BUTTON_FOCUS_APPEARANCE_ACCEPTANCE.md

<!-- source: BUTTON_FOCUS_APPEARANCE_ACCEPTANCE.md -->
### Shared button focus shape

#### Design self-check and scope

1. App owns this appearance-only correction. Module dependencies stay unchanged.
2. Existing AppButton/AppToggleButton wrappers and Avalonia's native focus-adornment
   lifecycle are reused; no service port, alternate button template or event
   handler is introduced.
3. Commands, approval policies, tools, storage and networking remain unchanged.
   Tests render isolated Headless windows; existing desktop windows and user data
   are not accessed.
4. One 13-line shared style is included through ActionButtonVariants, avoiding
   further growth of the 498-line App.axaml. New test files each declare one type;
   modified handwritten C# files/methods remain below the hard limits.
5. Tests cover rendered corners/strokes, layout/content origins, radius updates,
   pointer versus keyboard focus, blur, explicit focus ownership and enabled/
   disabled command activation in Light/Dark and multiple render scales.

The UI/UX skill's applicable guidance is visible keyboard focus and consistent
component states, not removal of focus feedback or mobile-sized desktop targets.

#### Reproduced defect and correction

Avalonia's default focus adorner draws a square black/white frame around otherwise
rounded buttons. In the verified unchanged-appearance baseline, all 24 shape cases
fail the corner-pixel assertion: a transparent rounded corner becomes a square
stroke on keyboard focus. Raw screenshots reproduce this across both themes,
100/125/200-percent rendering and the actual composer-send, selected navigation
rail, conversation-tab, settings-nav, history, icon and ordinary action variants.

Shared ButtonFocusVariants replaces that adorner with an AppBorder using the
existing BorderFocusBrush and a two-DIP stroke. Its corner radius binds to the
native adorned focus target. It is not hit-testable and does not participate in
button layout. Native focus creation/removal, button dimensions, content padding,
labels, icons, hover/pressed states and command execution remain in place.
Explicit FocusAdorner ownership, including theme thumbnails, keeps precedence.

The markup guard previously misclassified the nonvisual Template factory as a
native visual component. Its allowlist now recognizes that factory. Negative
tests still reject native Border/Button descendants inside it, and business
markup still cannot replace shared FocusAdorner templates. Native controls are
not newly permitted in views, controls or shared templates.

#### Validation

- The focused final run passes all 28 new pixel/state cases. Its 24 shape cases
  are the same cases as the failed verified baseline; four additional cases check
  command behavior and explicit focus ownership.
- The final complete pixel suite passes 236/236, including those 28 cases. The
  earlier focused runs are not added to this complete count.
- All 1,649 related App cases pass, matching the separately discovered filter
  count. This includes the two new markup-boundary cases, existing theme-thumbnail
  focus ownership, caption/navigation/dialog/composer tests and eight actual
  automation UI workflows. The real editor's Cancel button now renders the shared
  rounded ring in the complete MainWindow capture. Seven architecture cases also
  pass. These three disjoint final scopes total 1,892, with no failures, skipped/
  not-executed or aborted cases and unique execution identities.
- After all tests finish, the solution build succeeds with zero warnings/errors.
  Six recorded tested/runtime DLL hashes stay unchanged across that build; all
  three production App copies match. Pixel-test identities also stay unchanged
  while building/running the App subset. Scoped whitespace-format, XML/hygiene
  and Roslyn source-limit/one-type checks pass.
- The first complete attempt records 20 new-test resource-lookup failures: the
  fixture omitted the actual ThemeVariant when finding BorderFocusBrush. It now
  supplies that theme explicitly. That report and earlier fixture compilation/
  guard failures remain evidence, not reproduced production defects or erased
  by the subsequent green run.

Evidence stays outside the repository at
`%TEMP%/MossAgent-button-focus-20261016`: baseline/final TRX and logs, unchanged
state PNGs, source/markup audits and before/after crops. The crops preserve
100-percent pixels and compare MossAgent before/after, not another application.
`app-related-filter.txt`, `app-related-discovery.txt`, `verified-test-counts.json`,
`solution-build.log` and hash CSVs record the exact final validation boundary.

#### Remaining gates

Native screenshots and existing-window operations remain user-postponed. Off-screen
render scaling does not establish Windows DPI behavior or same-state Codex visual
parity. Other product interactions, the earlier unexplained App allocation failure
and complete-product acceptance remain open. The 1,649-case subset is not a full
App/eight-suite checkpoint or an allocation-failure investigation. This correction changes no packaging,
JSON, JIT/AOT or user settings; nothing is staged, committed, pushed or released.
<!-- /source: BUTTON_FOCUS_APPEARANCE_ACCEPTANCE.md -->


---

<a id="checkbox-focus-appearance-acceptance"></a>

## CHECKBOX_FOCUS_APPEARANCE_ACCEPTANCE.md

<!-- source: CHECKBOX_FOCUS_APPEARANCE_ACCEPTANCE.md -->
### Shared checkbox focus appearance

#### Design and scope

This slice applies the UI/UX skill's desktop guidance: retain visible keyboard
focus, keep interaction bounds stable and use shared semantic theme resources.
It extends the existing rounded button/dropdown language rather than replacing
the Codex-inspired design or imposing mobile-sized controls.

Required design self-check:

1. App owns shared checkbox appearance; dependency directions do not change.
2. The existing AppCheckBox/native CheckBox contract and theme are reused.
   No new application service or port is needed.
3. No tool, approval, file, network or persistence behavior changes. Evidence
   uses isolated Headless rendering, not existing native desktop windows.
4. Production changes stay in CheckBoxVariants.axaml. Each handwritten test file
   has one type and passes the existing source/method limits.
5. Rendered tests cover all three selection states, Light/Dark, fractional DPI,
   geometry stability, keyboard/pointer activation and disabled rejection.

#### Reproduced defects

The unchanged UI fails 20 of the 22 new cases. All 18 shape cases show a native
black/white square focus rectangle; the two interaction/theme cases also detect
the missing shared adorner. The two explicit-focus-owner cases already pass.
Baseline TRX and rendered frames record executed failures, not compile failures.

The first rounded-adorner candidate removes the square frame, but four geometry
cases still fail: checked/indeterminate glyphs in both themes move by 0.8 DIP at
125-percent scaling. The existing focus selector increases the check-surface
border from one to two DIP, changing fractional layout rounding. That candidate's
failed TRX is retained, not presented as a successful final revision.

#### Correction

- The shared checkbox theme supplies a default non-interactive AppBorder focus
  adorner using BorderFocusBrush, a two-DIP outline and the control's corner
  radius. The theme's default radius is four DIP, consistent with its check box.
- The check-surface border no longer changes thickness when focused. Its existing
  focus brush remains; the separate adorner provides emphasis without reflow.
- The native 16-DIP check surface, 12-DIP glyph, 28-DIP minimum height, label gap,
  full-row hit surface, hover/pressed treatment and disabled opacity are unchanged.
- Native Space/pointer activation, three-state cycling and disabled behavior are
  retained. An explicit local FocusAdorner still overrides the theme default.
- The existing structural interaction test now checks the retained focus brush
  instead of requiring the old geometry-changing thickness. Pixel tests assert
  unchanged surface/glyph/label origins, preventing the drift from returning.

#### Final verification

- The complete pixel suite passes 282/282, including all 22 new cases.
- Related App controls, checkbox hit targets, shared-template/component rules,
  themes, settings layout and focus checks pass 424/424. Discovery also reports
  424 cases, matching the executed set.
- Architecture checks pass 7/7. These three disjoint scopes total 713, without
  failures, skipped/not-executed or aborted cases. Focused reruns and the earlier
  baseline/candidate runs are not added again.
- Solution build succeeds with zero warnings/errors. Scoped whitespace, diff
  hygiene and source-limit verification pass. Four checkpoint assembly identities
  remain unchanged across testing and final incremental build; the App copies in
  both test outputs match the production App assembly.
- Light/Dark before/after frames and the comparison sheet were visually inspected.
  Tests cover 100-, 125- and 200-percent rendering and live theme changes.

Evidence stays outside the repository in
`%TEMP%\MossAgent-checkbox-focus-20261016`, including `baseline.trx`,
`focused.trx` (first candidate), final TRX reports, `before-after.png`,
`source-audit.json` and build/format/identity reports. No screenshot helper,
generated image or synthetic settings data is added to source control.

#### Remaining limits

These are production-style Headless frames, not native MossAgent/Codex same-state
screenshots. The user's postponed native capture remains respected. This is not a
complete current App/all-suite checkpoint, proof of Codex pixel equivalence,
resolution of the earlier unrelated allocation assertion or product completion.
The long-term product goal remains active.
<!-- /source: CHECKBOX_FOCUS_APPEARANCE_ACCEPTANCE.md -->


---

<a id="composer-context-component-acceptance"></a>

## COMPOSER_CONTEXT_COMPONENT_ACCEPTANCE.md

<!-- source: COMPOSER_CONTEXT_COMPONENT_ACCEPTANCE.md -->
### 输入区上下文条组件验收

#### 范围与设计自检

1. 行为归属 App 的共享 Controls 与 Styles，编译约束归属 App.Analyzers；
   不改变模块依赖方向，不引入运行时分析器依赖或全局服务定位器。
2. 复用项目选择器、代理列表和既有 OpenProjectPickerCommand/SelectAgentCommand。
   页面只提供语义数据、命令和项目选择业务内容，不另建文件或网络访问入口。
3. 只改变呈现与命令转发，不改变授权目录、审批、超时、取消、输出或持久化策略。
   组件关闭自己的浮层，不取消任务或重建浏览器、模型与代理会话。
4. 新组件的 C# 与 AXAML 分离，每个手写 C# 文件一个类型；文件和方法保持硬上限。
5. 先用长名称、不同宽度和迟到代理加载复现越界，再检查拒绝路径、命令目标、
   DataContext、生命周期和原生窗口；不以 Headless 通过代替实际截图验收。

#### 复现与修复

- 原页面自行拼装 Auto/*/Auto 三列。项目与代理名称同时很长时，360、400-DIP
  明暗主题的四个回归均失败，分支宽度为 0；740-DIP 两项通过。
- 普通权限原生 980×640、展开检查器的同场景截图确认代理文字越出上下文卡片，
  分支只剩图标，与自动回归中的空间挤占一致。
- 新增 `AppComposerContextBar`，实际宽度小于 440 DIP 且有代理时均分有限空间。
  项目、分支与代理使用已有共享图标、字体、表面和截断变体，完整名称保留工具提示。
- 第一版独立布局回归通过，但原生启动后仍保留旧列测量。补充“先布局、后加载代理”
  回归实际失败：列定义已经是 Star，分支仍宽度 0。最终保留既有 ColumnDefinition
  实例，只更新 Width，并在组件宽度变化时重新测量；不靠裁剪或减少功能遮掩越界。
- 最终原生日志：组件宽 326、内部布局宽 282，项目/分支/代理分别宽 89/89/88 DIP，
  位置分别为 0/97/194，无重叠或越界。此前原生日志中同一位置为 182/0/149，
  代理从 198 开始，超过内部布局边界。

#### 功能和边界

- 项目、代理菜单与操作组成在组件内维护，不再依赖宿主 Window 的 ViewModel 形状。
  项目选择器通过共享内容宿主继承业务 DataContext。
- 项目或选择器内容缺失、命令缺失时入口不可用；CanExecute 拒绝继续生效。
  代理选项绑定自己的目标，默认选项传 null；不可用命令不会变成看似可用的按钮。
- 换项目、替换命令或业务内容、代理不可用和卸载时关闭浮层。
  缩放保留相同内部控件、数据和命令，不重新创建选择器来修布局。
- `MOSSUI002` 与 AXAML `ValidateSharedWorkspacePrompts` 拒绝 Content、Padding、
  尺寸和内容对齐覆盖，包括原生属性所有者限定和属性元素；数据及业务内容绑定允许。
- 专项回归 60 项通过，覆盖上述允许/拒绝、布局、迟到加载、生命周期和既有菜单行为。

#### 运行证据

仓库外目录：`C:\Users\wzheng\AppData\Local\Temp\MossAgent-composer-layout-qa-20261006-01`。
宿主加载真实 MainWindow 和独立合成 SQLite；日志确认 `QA_ELEVATED: False`。
使用合成密钥和不可达模型地址，没有发送消息、执行工具或修改真实用户配置。

- `before-agent/composer.png` 为修复前原生暗色窄窗口。
- `verified-dark/composer.png` 为最终修复后的相同尺寸和场景，分支恢复、操作不越界。
- `verified-dark/agent-menu-confirmed.png` 与独立 popup PNG 记录实际鼠标打开的代理菜单。
  首次点击返回 `coordinate input geometry is unavailable`；刷新唯一窗口绑定和截图后，
  一次恢复点击成功。此前同名但未确认的图片不作为菜单成功证据。
- 实际点击默认代理后，标签变为“默认代理”，菜单关闭，输入草稿保持不变。
- 实际收起检查器后，三列恢复宽布局、浮动上下文卡片恢复，草稿仍保留；
  再实际打开项目选择器，项目列表与搜索框正常呈现。对应
  `verified-dark/inspector-collapsed.png`、`project-picker-confirmed.png`。
- 原生浅色 1280×820 复核见 `verified-light/composer.png`：内部布局宽 415 DIP，
  项目/分支/代理宽 182/68/149，三列均在卡片内。明暗图片未被拉伸成同一尺寸。
- 暗色宿主最后因自身八分钟上限取消并关闭，日志含 `QA_FAILURE: TaskCanceledException`；
  随后关闭按钮调用返回前台进程错误。没有将其计为原生关闭按钮成功或应用自身故障修复。
  进程已确认消失，浅色宿主使用外部关闭信号正常清理。
- Python 使用被动 PrintWindow，核对原始 980×640 尺寸与非黑像素；没有移动窗口、
  注入输入或拉伸图片。弹出层单独保存，并按实际偏移合成，不恢复窗口矩形外的 DWM 阴影。
- 被动捕获当前 Codex 作为框架参考，没有操作其 UI；内容、主题和有效宽度不同，
  不将它与本轮截图称为逐像素一致对照。

#### 最终验证

- 最终完整解决方案 2,090 项测试通过，其中 App 1,601 项，没有失败或跳过。
  新增图标断言最初误将两个不显示的可选尾图标计入；修正为检查实际显示的图标后，
  专项 60 项和完整回归再次通过。仍要求三个共享 12-DIP 图标和各宽度下垂直中心差
  不超过 0.5 DIP，不放宽阈值；失败日志保留为 `solution-tests-icon-failed.log`。
- 解决方案构建 0 警告、0 错误；修改的 C# 空白格式检查通过。
- Roslyn 审计 920 个 C#/AXAML 文件，文件行数、单类型和方法行数均无违规。
- 全工作区 `git diff --check` 通过；没有暂存、提交、推送或修改真实用户数据。
- 仓库外日志：`context-focused-final.log`、`solution-tests-final.log`、
  `solution-build-final.log`、`format-final.log`、`audit.log`。
  `composer-context-before-after.png` 只并排放置同尺寸原生截图的原始裁片，没有缩放。

#### 仍需验收

- 本轮主要修复输入区上下文条，不代表设置页、所有 DPI、字体或整个主界面已还原。
- 跨项目切换、模型请求、子代理执行、真实授权目录和工具工作流不能由本地菜单验证替代。
- 全面 Codex 同状态像素校准和产品级跨工作流验收仍需继续；完整目标保持进行中。
<!-- /source: COMPOSER_CONTEXT_COMPONENT_ACCEPTANCE.md -->


---

<a id="conversation-header-component-acceptance"></a>

## CONVERSATION_HEADER_COMPONENT_ACCEPTANCE.md

<!-- source: CONVERSATION_HEADER_COMPONENT_ACCEPTANCE.md -->
### 对话标题栏共享组件验收

#### 2026-10-07 实现与边界

- 沿用全项目共享 Controls、Styles、AXAML 构建门禁和 C# 分析器，未增加第二套组件体系。
  本轮继续把页面拼装的对话标题、状态行、右上角操作和菜单收口到 `AppConversationHeader`。
- 页面只提供标题/菜单业务内容、会话目标、现有命令与状态。组件拥有有限宽度分配、
  52-DIP 高度、20-DIP 状态行、190-DIP 菜单和浮层生命周期；样式值只在共享变体维护。
- 长 MCP 启动提示和运行活动在有限列内省略，完整文字保留为工具提示；不挤掉标题或操作。
  当前 Codex 被动参考帧中，检查器展开时对话区仅显示更多与上下文操作，收起入口在检查器。
  共享标题栏遵循该状态，收起检查器后恢复对话区展开入口；不删除原有上下文功能。
- 缺少会话、菜单或命令时对应操作不可用；命令自己的 CanExecute 仍然生效。
  标题和菜单宿主明确继承业务 DataContext；换目标、菜单、上下文、命令或卸载时关闭菜单。
- `ValidateSharedConversationHeaders` 和 `MOSSUI002` 禁止页面替换 Content、Padding、
  尺寸和内容对齐，包括普通属性、属性元素及原生属性所有者限定；合法业务槽位继续允许。

#### 实现自检

1. 视觉和布局归 App Controls/Styles，本地化文字归 Resources，编译边界归 App.Analyzers；
   不改变模块依赖方向或让 UI 直接访问基础设施。
2. 复用现有会话菜单、上下文/检查器开关、MCP 设置命令和 Avalonia 布局契约，不增加端口。
3. 不执行用户任务、不发出提供商请求、不改变授权或审批策略；没有持久化迁移。
   合成运行状态只存在于独立测试宿主，截图及宿主日志不含真实凭据。
4. 每个手写 C# 文件一个类型，保持文件和方法硬上限；截图、合成 SQLite 和诊断脚本在仓库外。
5. 成功路径覆盖明暗主题、长状态、迟到状态、大小变化、菜单宽度、键盘和真实命令。
   拒绝路径覆盖缺失目标/命令、CanExecute、生命周期及 C#/AXAML 封装绕过。

#### 失败基线与原生修正

- 单独启动警告未复现原缺口；加入真正的 `WorkspaceRunTracker` 运行状态后，
  两项明暗主题真实页面测试均失败，标题宽度坍缩。原生 980×640 截图同样缺失标题/动作。
  共享组件将状态移至有限宽度状态行，没有依靠裁掉操作或放宽布局断言修复。
- 中间版 Headless 通过后，原生菜单仍约 104 DIP：`AppContentControl` 使用原生
  ContentControl 的 StyleKey，类型限定选择器没有命中。改用带 `x:SetterTargetType`
  的共享类选择器，并新增真实菜单 Bounds.Width 为 190 的断言；最终原生菜单复核通过。
- 原生输入只作用于任务自建预览：更多菜单打开、Escape 关闭、检查器顶栏收起、
  对话标题栏重新展开以及 MCP 警告打开实际 MCP 设置页。检查器切换后草稿保留。
  没有点击删除/归档、发送模型请求或操作 Codex UI；UIA 滞后帧不被当作重复点击依据。
- 最大化浅色截图随后发现检查器仍为 296 DIP。只读布局日志确认：实际工作区 1600 DIP，
  列宽仍按 660-DIP 的早期测量约束限幅，宽度偏好为空，不是用户拖动过的结果。
  修正在既有 `AppWorkspacePanes.ArrangeOverride` 中按最终可用宽度配置共享列，
  不在页面添加 Width，也不更改默认比例、最小宽度或用户偏好规则。
- 最终六项面板回归在仓库外旧 App DLL 上运行：四项失败、两项合法浮动布局通过；
  修复后六项全部通过，面板拖动、键盘、限幅恢复、浏览器布局和真实窗口专项共 21 项通过。
  最终普通权限最大化原生日志确认：工作区 1600、对话 844、检查器 752、分隔条 4 DIP。

#### 证据与范围

- 证据目录：`C:\Users\wzheng\AppData\Local\Temp\MossAgent-conversation-header-qa-20261007-01`。
  被动 Python PrintWindow 捕获明确 PID，检查稳定边界、预期尺寸及非黑像素，不激活或移动窗口。
- `header-dark-before.png` 与 `header-dark-verified.png` 是 980×640 标题栏前后帧；
  `header-dark-menu-final.png`、`header-dark-inspector-closed.png`、
  `header-dark-inspector-reopened.png`、`header-dark-open-mcp-settings.png` 记录实际输入结果。
- `header-light-max-final.png`、`header-dark-max-final.png` 是最终明暗主题 1920×1032 帧；
  `header-dark-narrow-final.png` 以最终构建重新验证 980×640 长警告与运行状态。
  `pane-max-arrange-before.png` 保留 296-DIP 失败状态，`inspector-width-before-after.png`
  使用原始尺寸裁剪并排显示，没有拉伸。中间的 `header-light-max-verified.png` 已被最终帧取代。
- `codex-reference-current.png` 是被动 Codex 帧。对照只去掉四边各 8 像素；
  `codex-mossagent-header-comparison.png` 使用同坐标未缩放标题栏裁剪。
  标题相同，但正文、合成项目数据、检查器用途和运行状态不同；图标细节、间距和字体
  尚不能称为逐像素相同。没有重新自动化 Codex 窗口，也未穷尽全部页面、DPI 和状态。
- 早期完整回归曾出现既有浏览器隔离测试的 Avalonia 属性池 `Stack empty` 异常；
  包含该测试的 95 项复测与后续完整回归通过。没有修改浏览器代码，也不声称修好了间歇异常。
- 编译与封装覆盖全项目业务页面，不仅四张示例；允许纯结构布局、共享组件底层实现
  和框架模板内部元素。整个产品目标仍未完成，不暂存、提交或推送。

#### 标题栏与列宽阶段验证结果

- 标题栏新增 43 项组件、封装和原失败回归；面板新增六项最终排列回归。
  最终面板专项 21 项通过，完整解决方案 2,229 项通过，其中 App 1,740 项，无失败或跳过。
- 解决方案构建 0 警告、0 错误；本轮 11 个 C# 文件的空白格式及无修改验证通过。
  Roslyn 审计 945 个 C#/AXAML 文件，文件、方法与单类型硬上限无违规。
- 全工作区 `git diff --check` 退出码 0，暂存路径为 0；Git 的 LF/CRLF 转换提示
  不被当作源代码空白失败，也没有通过批量格式化已有无关改动消除提示。
- 最终预览使用真实 App 程序集，其 SHA256 与仓库构建相同；布局读取、被动截图和
  合成运行不发出提供商请求。正式用户数据库和配置没有被替换。
- 后续自适应子内容继续修正模板测量，最终回归已增至 2,235 项；新增原生帧、工具输入
  限制及实际缩放差异见 [WORKSPACE_PANE_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#workspace-pane-component-acceptance)，不复用中间帧声称最终状态。

#### 2026-10-12 三按钮工具栏修正

- 根据同尺寸 Codex 被动参考帧，默认对话工具栏收口为更多、上下文、打开检查器三个按钮。
  原完整视图操作移入真实更多菜单，保留现有命令、键盘操作、CanExecute 和草稿保留逻辑。
  检查器开启或展开命令替换时关闭旧菜单，缺少目标或命令时不能执行。
- 间距、图标光学校正、28-DIP/8-DIP 圆角概览背景均在共享标题栏变体维护；
  重复圆角 setter 被移除。检查器入口复用正方形 `UiIconPanelAdd`，没有增加另一套图标资源。
- 新增 11 项标题栏回归在原实现上全部失败，修正后与四组既有标题栏/检查器测试共
  56/56 通过，涵盖三种宽度、明暗主题、几何、菜单、键盘及缺失/拒绝命令。

##### 原生帧与剩余差距

- 前序普通权限独立 App 宿主的原生浅色最大化帧与 Codex 参考帧都是 1920×1032。
  `toolbar-comparison.png` 采用相同坐标原尺寸裁片，不做拉伸。截图和外部 Python 对比脚本
  保存在 `%TEMP%\MossAgent-main-fidelity-20261012-114819`，不加入仓库。
- 更多图标的暗像素外接框同为 `[1810,69,1820,71]`；加号方框同为
  `[1884,64,1896,76]`。这是指定阈值和局部区域的外接框一致，不是整个工具栏逐像素一致。
  上下文图标仍相差两列暗像素，活动前景色、加号笔画及抗锯齿仍存在差异。
- 前序原生观察确认更多菜单显示“进入完整视图”，但没有完成该新入口的原生展开/恢复
  点击验收；完整视图和草稿保留目前由真实页面 Headless 工作流回归验证。
  不把合成运行状态当作真实 agent 执行成功，也不把本轮构建当作新的原生截图验收。
- 本次后续代码优化只减少检查器未变化属性的通知，不新增样式或截图；相关结果见
  [UI_HOTPATH_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#ui-hotpath-optimization-acceptance)。完整产品及 Codex 一比一视觉目标仍未完成。

##### 后续整合回归

- 扩展标题栏/检查器/图标/壳布局和封装专项 370/370 通过。旧菜单关闭集成测试改为定位
  唯一共享业务槽，仍检查菜单内容身份、锁定拒绝、键盘关闭标签和任务未被归档。
- 最终解决方案构建 0 警告、0 错误，相关 C# 空白格式及文件/类型/方法硬上限验证通过。
  完整 App 的两次 3,797/3,798 失败及独立复测分别记录在
  [UI_HOTPATH_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#ui-hotpath-optimization-acceptance)，不将专项通过表述为全量通过。

#### 2026-10-12 概览活动色与原生操作补验

- 概览按钮使用实际 `IsContextVisible` 绑定活动类；共享活动变体采用随主题变化的
  `TextPrimaryBrush`，禁用状态不进入活动前景样式。新增两项明暗主题回归检查活动类、
  背景、按钮及图标颜色、主题切换、键盘操作和拒绝命令；旧实现两项均失败。
- 最终上下文图标继续保持 12-DIP、1-DIP 线宽与 `translate(-1px,0px)`。
  半像素偏移试验没有改善局部误差，已撤销；不将试验帧视作最终实现。
- 最终普通权限 `host-verified` 宿主 PID 24572 的 Python 被动截图再次确认
  1920×1032 非黑图像，替换先前指向半像素试验的 `moss-after-light-max.png`，
  并重新生成 `toolbar-pixels.json` 与原尺寸 `toolbar-comparison.png`。
  参考上下文暗像素框 `[1842,64,1856,76]`、47 像素；最终框
  `[1842,64,1855,76]`、48 像素。核心色分别为 26/28/31 与 33/35/38。
  一列边界、笔画、抗锯齿和其他按钮前景仍有差距，不宣称逐像素一致。
- 同一最终宿主实际点击概览开关，确认卡片隐藏与恢复，保留
  `context-closed-verified-light-max.png`。此前合成宿主已经实际输入未发送草稿、
  打开更多菜单、进入完整视图、退出完整视图并收起检查器；截图分别为
  `draft-light-max.png`、`menu-light-max.png`、`inspector-full-light-max.png`、
  `inspector-restored-light-max.png`、`inspector-closed-draft-light-max.png`。
  这些操作帧早于撤销半像素试验，不替代最终几何帧；草稿未发送，未点击删除或归档。
- 原生证据位于仓库外 `%TEMP%\MossAgent-context-native-20261012-124649`。
  宿主使用真实 App/MainWindow 和独立合成 SQLite，未访问用户数据库、凭据或远端模型。
  合成 Running 元数据不代表真实 agent 执行成功；蓝色指针光圈也不是应用阴影。
- 最终偏移恢复后，包含两项活动色及完整工具栏回归的 145 项相关测试再次通过；
  解决方案构建 0 警告、0 错误。检查器标签通知优化及本次全量结果另记在
  [UI_HOTPATH_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#ui-hotpath-optimization-acceptance)。本轮未补验原生暗色/窄窗、其他页面和 DPI，
  完整产品与 Codex 一比一目标继续保持未完成。

#### 2026-10-12 后续暗色窄窗与概览分组验收

- 本节补充前述阶段尚未完成的原生暗色/窄窗观察，不覆盖历史阶段的验证限制。
  真实 App 的最终普通权限宿主验证暗色 980×640、暗色最大化 1920×1032，
  并从实际设置页切换浅色后返回；不是用 Headless 图片代替原生窗口。
- 概览标题/状态、来源标题和来源列表改用共享分组变体，内边距只在共享卡片样式维护。
  同坐标、原尺寸 Codex/MossAgent 裁片的四个指定 ROI 首墨迹行现为 123/152/195/256。
  该局部测量不代表字体、图标、抗锯齿、颜色或整张界面逐像素一致。
- 最终宿主实际打开来源检查器、收起、最大化及切主题；早期/中间宿主另完成更多菜单、
  Escape、完整视图展开/恢复、检查器开关和未发送草稿。最终几何截图与早期交互帧
  分开记录，不把早期帧当成最终构建的验收。没有发送请求或自动操作 Codex UI。
- 证据位于仓库外 `%TEMP%\MossAgent-main-dark-narrow-20261012-144117`，
  详细截图名称、进程退出、程序集哈希、夹具失败历史及测量限制见
  [INSPECTOR_OVERVIEW_FIDELITY_ACCEPTANCE.md](UI_WORKFLOWS.md#inspector-overview-fidelity-acceptance)。
- 新增八项回归，最终相关专项 67/67、八程序集全量 5433/5433，解决方案构建零警告/错误。
  原生 DPI 仅为 96，其他 DPI、全页面/全状态及公网真实提供商仍未验收；
  完整产品与 Codex 一比一目标继续保持未完成。
<!-- /source: CONVERSATION_HEADER_COMPONENT_ACCEPTANCE.md -->


---

<a id="conversation-tab-component-acceptance"></a>

## CONVERSATION_TAB_COMPONENT_ACCEPTANCE.md

<!-- source: CONVERSATION_TAB_COMPONENT_ACCEPTANCE.md -->
### 会话标签共享组件验收

#### 2026-10-08 所有权与变更

- 顶部出现多个标题来自已经打开的会话标签，不是当前标题缓存错误。保留多标签、
  选中项自动滚动、新草稿以及关闭后恢复单标题的既有语义。
- `AppConversationTab` 收口选择按钮、更多按钮和关闭菜单；业务页面仅提供标题、
  选中状态、明确目标及既有命令。组件内部弹框不再依赖 `$parent[Window]` 的业务模型。
- 共享样式维护 238×34 DIP 标签、24×24 DIP 更多按钮、160 DIP 标题上限及
  190 DIP 菜单宽度。按钮图文居中，长标题省略；选中、悬停、键盘焦点与弹框展开
  显示更多按钮。不为每页重设颜色、阴影、圆角或图标尺寸。
- AXAML 构建门禁与 `MOSSUI002` 拒绝业务层覆盖尺寸、Padding、Content、菜单或
  内部组合。语义命令、目标和选中状态仍可绑定；保留原生视觉控件限制。
- 缺失目标或命令禁用对应入口；CanExecute 变化立即更新可用性并关闭不可用菜单。
  目标、DataContext、命令替换、自身/祖先隐藏或禁用、脱离视觉树均关闭弹框。
  祖先和命令订阅只在挂载期间存在，重新挂载改为观察当前宿主。
- 迁移验证暴露同步隐藏菜单会先卸载按钮，令 Avalonia 的后续命令执行被跳过。
  现在在命令之后排队关闭，并以菜单版本拒绝已经关闭/重新打开后的旧回调。
  菜单打开时聚焦关闭项，使键盘打开后可以直接执行；不重复调用业务命令。
- 标签 ViewModel 实现幂等 Dispose，解除目录订阅、清空标签并拒绝旧命令。
  Workspace 的 Dispose 同步释放标签模型。关闭标签不归档或删除持久化会话。

#### 必要自检

1. 所有权属于 App 共享控件及原有标签模型；模块依赖方向保持不变。
2. 选择/关闭复用现有目录与标签命令；没有新增端口、SQL 或模型请求。
3. 不改变授权、审批、工具超时、取消、输出限制或任务执行；没有迁移或自动重放。
4. 涉及的手写 C# 各一个类型，C#/AXAML 不超过 500 行，方法不超过 80 行；
   `App.axaml` 不新增 StyleInclude，继续复用已有共享样式文件。
5. 45 项新增用例覆盖几何、真实绑定和持久化、不可用/锁定/旧目标拒绝、挂载周期、
   Workspace 释放与迟到关闭回调，不仅检查菜单文字存在。

#### 测试与构建

- 迁移前 Dispose 用例失败，日志为 `disposal-before.log`。迁移阶段真实菜单用例也
  复现了同步隐藏后命令没有执行；修复后相同路径通过。初期测试误把 owned label
  的外层属性当作实际文本，以及误用了默认选中的 Upsert；改为断言实际文字和
  明确的非选择更新，没有为使测试通过修改目录行为。
- 定向标题、标签与选中项滚动测试最终 80 项通过；包括明暗主题真实 MainWindow
  的选中/后台标签菜单、锁定拒绝、关闭后会话仍持久化且未归档，无模型请求。
- 完整解决方案回归、普通 Debug/JIT 构建、原生 QA 宿主构建及格式/AST 检查结果
  以本节后续最终验收记录为准，不用较早运行替代最终源码验证。

#### 原生截图与交互

- 使用仓库外 QA 宿主加载真实 App/MainWindow，普通权限/JIT，独立合成数据库，
  只读且不调用模型。当前预览并非用户真实数据副本。Python 脚本与证据不加入仓库。
- 证据目录：`%TEMP%/MossAgent-conversation-tabs-20261008`。
  `tabs-Light-1280-820` 验证鼠标关闭当前标签、显示另一会话、草稿保留和从侧栏恢复；
  `background-Dark-980-640` 验证关闭后台标签不改变当前会话。
- `keyboard-Dark-980-640` 在最终组件中用 Space 打开后台标签菜单，再用 Space
  关闭目标；日志显示标签从两项变一项，原选中会话和草稿不变。原生 UIA 对弹框
  报告的焦点仍为主窗口，不能据此断言焦点丢失；以实际键盘结果和控件焦点测试验证。
- `keyboard-Light-1280-820` 在最终组件中用 Space 打开并关闭当前标签，转到另一
  会话并恢复单标题；草稿仍在。所有四个合成预览均正常关闭，退出码为 0。
- PrintWindow 捕获主窗口时不包含独立原生弹框。因此 `window-menu.png` 仅是
  主窗口表面，不能用作菜单像素证据。最终 `window-menu-screen.png` 使用已有
  Python 脚本的 passive 屏幕裁片模式，在目标未遮挡时包含弹框，不激活、点击、
  移动或缩放窗口。其他主窗口帧使用带 PID/尺寸/黑图守卫的被动 PrintWindow。
- 当前 Codex 参考截图为 1936×1048；对照图只去掉四边各 8 像素的窗口边缘。
  MossAgent 为原始 1280×820 和 980×640。局部比较均裁片、不缩放。
  Codex 是单标题和文件右栏状态，并非相同合成数据或多标签状态。
- `conversation-tab-comparison.png` 上排为 Codex/最终 MossAgent 单标题，下排为
  最终组件的明暗菜单；使用实际包含独立弹框的屏幕帧，而非主窗口 PrintWindow 帧。

#### 最终验收记录

- 最终代码的定向测试 80 项通过。完整解决方案最后一次回归共 3,056 项，
  3,055 项通过，1 项失败；不是全量全绿。
- 失败为 `SidebarHeadingRhythmTests.PinAndUnpin_RefreshTheInsetWithoutChangingSelectedTask`
  的临时目录清理遇到 Windows sharing violation（`WithWindow` 的 finally，第 124 行）。
  布局断言已经执行；单独复测该类 12 项全部通过。未修改相关清理代码或无关命令测试。
- `regression-final.log`、`results/final-regression*.trx` 和 `sidebar-heading-retry.log`
  保留原始结果，不以重试覆盖失败日志。补菜单焦点前的一次完整运行曾 3,056 项通过，
  但该较早结果不作为最终源码的全绿声明。
- 最终解决方案、普通 Debug/JIT App 和原生 QA 宿主构建均 0 警告、0 错误；
  本轮 C# 的 scoped whitespace 验证通过，13 个 C#/AXAML 文件的 Roslyn 单类型、
  语法/规模审计通过，相关路径 `git diff --check` 通过。

#### 验收边界

- 单标题延续已有文件夹与纯文字呈现；多标签保持 MossAgent 既有功能。
  未取得 Codex 对应多标签/菜单及明暗主题的同状态参考，不能宣称逐像素等同。
- 未验证全部字体、DPI、触摸、悬停状态，或完整产品所有工作流。
  本轮不是全部目标或一比一还原完成证明。
- 没有修改单文件/JIT 发布、恢复 AOT、读用户凭据、暂存、提交或推送。
<!-- /source: CONVERSATION_TAB_COMPONENT_ACCEPTANCE.md -->


---

<a id="danger-button-theme-acceptance"></a>

## DANGER_BUTTON_THEME_ACCEPTANCE.md

<!-- source: DANGER_BUTTON_THEME_ACCEPTANCE.md -->
### Shared danger-button theme repair (2026-10-13)

#### Design and scope

1. App owns this appearance-only behavior. Its existing shared `danger` button
   variant now resolves three theme resources; no dependency direction changes.
2. Existing resource dictionaries and wrappers suffice. Commands, labels, hit
   areas, sizing, disabled treatment and style order remain unchanged.
3. There are no authorization, approval, cancellation, output or persistence
   changes. Native QA uses independent read-only database backups with a paused
   plan and a loopback observer that records and refuses unexpected requests.
   No delete, model fetch, send, run, policy change or permission prompt is invoked.
4. App.axaml is 497 lines, the new resource dictionary 15, and the single-type
   DangerButtonThemeTests 138. Methods remain below the hard limit.
5. Tests cover both themes, live switching, hover/press, hit-area preservation,
   text contrast of at least 4.5:1, and disabled-command nonexecution/recovery.

#### Reproduction and repair

The 980x640 native light provider page exposes maroon delete buttons because
the shared `danger` variant hard-codes dark background/border/foreground colors.
All nine markup consumers, including settings deletion and terminal Stop, use
that same variant. No page-local palette is added.

`DangerButtonResources.axaml` supplies Light and Dark theme dictionaries. Light
uses `#FEF2F2` / `#FECACA` / `#B91C1C`; Dark retains the existing
`#2E1517` / `#6B2127` / `#F87171`. Three DynamicResource setters replace the
hard-coded colors. The existing disabled style still resolves muted resources.

Evidence stays outside source at
`%TEMP%\MossAgent-narrow-fidelity-20261013-1015`.

- The three saved baseline TRX files reproduce four genuine light/switch palette
  failures. They also contain test-authoring failures: two incorrectly assumed
  muted light colors, followed by a resource lookup missing ActualThemeVariant.
  Those harness mistakes are corrected without changing production disabled
  colors or weakening its command nonexecution assertion.
- The first corrected eight-case group passes 8/8. After adding two contrast
  cases, the wider group passes 152/152, including all ten new cases, provider-row
  commands/layout, navigation state, shared-control boundaries and affected
  settings/terminal consumers. None fail or skip.
- The complete pixel assembly passes 24/24; architecture constraints pass 7/7.
  The solution builds with zero warnings/errors, and scoped whitespace formatting
  verification passes. XML parsing, size checks and scoped diff checks also pass.
- The earlier 5,667-case complete regression belongs to the sidebar-icon source
  checkpoint, not this later palette repair. Other full suites are built but not
  fully rerun here. No AOT, JSON, packaging, commit, push or release changes occur.

#### Native evidence ownership

The before default-output App SHA256 is
`1D1FB84D717C9364B8CE95D8414C1445EF733E44744A44FC77C013D2A2558AAB`.
The repaired default-output App SHA256 is
`C22DE2853829104723109774F69E5F24180787A144C78D11079D9366D5006525`.
Their launch manifests, independent data and audits are in `native/` and
`native-after/`; neither is substituted for the earlier E2A392 isolated replay.

- The before build passes actual 980x640 light context/inspector navigation and
  dark settings/provider/model-editor checks. Scrolling model B, Escape and
  reopening preserve its fully visible first label and fixed footer. Those two
  selected label/footer regions are pixel-identical before and after reopening.
- DPI changes between 96 and 120 are observed during this first replay, with
  recorded 1225x800 and 1920x1020 frames. A later screen capture is uniformly black
  and is rejected: only valid=false JSON is retained, not a comparison PNG.
  Inputs stop for fresh selection/observation; subsequent 980x640 PrintWindow
  pixels agree with the refreshed target state. The cause is not established and
  this does not certify a complete high-DPI sweep or a capture-helper fix.
- The repaired build independently reaches the real light/dark provider page at
  980x640, 96 DPI and ordinary permissions. Existing expanded rows update on the
  real theme command. Light deletion hover is exercised without clicking it.
- Equal-size, unscaled native crops show the light button's dominant RGB changes
  from 46,21,23 to 254,242,242. Model metadata pixels remain identical. The selected
  dark button region is pixel-identical across builds. The comparison image is
  `danger-button-native-comparison.png`; detailed regions are recorded in JSON.
- Both copies retain provider/model/proxy/project rows, six tasks, four terminal
  histories, original audits and three hash-verified artifacts. Each observer
  retains only the four historical requests; no new execution is observed.
- Both Apps restore Light and exit via their real close button with code zero.
  Both observers stop normally. All four QA/observer PIDs and port 51792 are absent
  at final cleanup. No forced termination or original-data mutation occurs.

#### Remaining gates

This repairs a shared appearance defect and adds bounded current-build native
evidence. It does not prove all consumers' native states, whole-window Codex or
PI-Desktop fidelity, complete high-DPI/theme/size coverage, or product completion.
The complete product goal remains active.
<!-- /source: DANGER_BUTTON_THEME_ACCEPTANCE.md -->


---

<a id="default-ui-font-fidelity-acceptance"></a>

## DEFAULT_UI_FONT_FIDELITY_ACCEPTANCE.md

<!-- source: DEFAULT_UI_FONT_FIDELITY_ACCEPTANCE.md -->
### Default UI font fidelity

#### Reference and design self-check

Read-only inspection of the installed Codex 26.1007.2314.0 CSS identifies its
default UI stack as platform system fonts, including Segoe UI on Windows. The
reference asset is `webview/assets/app-shared-9a94b907ed47.css`, SHA256
`69f929f71947824fed18365d54c8aa0c975a856649339e585a50f6f547e215d7`.
Archive/CSS inspection and pixel evidence remain external under
`%TEMP%\MossAgent-visual-tokens-20261014`; no Codex files are modified or its UI
automated, and no proprietary font asset is copied into MossAgent.

The original App startup resources and UiFontService explicitly used bundled
Inter for Default. This differed from the Windows reference even before
investigating remaining icon or glyph-rasterization differences.

1. App owns visual font resolution and shared resources. Application owns only
   the portable preference enum. Use Segoe UI for the Windows-first Default;
   retain the existing SegoeUi preference and add an explicit bundled Inter option.
2. Reuse current UI/content/code font services, theme editor, localized resources
   and appearance-store port. Avalonia's WithInterFont registers its font
   collection; keep it for explicit UI/content Inter choices.
3. Do not reorder existing enum values, migrate/rewrite saved defaults on load,
   change the JSON adapter/schema or touch model calls, approvals, paths or tools.
   Existing Default users adopt the new default appearance; explicit Inter remains
   selectable. Existing SegoeUi and content/code customizations stay intact.
4. Each handwritten C# file has one type and remains within existing limits.
   No per-view font overrides, native font files, platform hooks or static service
   locator is introduced.
5. Require a failing default/resource/option baseline, positive independent-theme
   persistence and negative invalid-value/no-write cases, wider typography/font
   regressions and solution build. Native same-size crops must distinguish font
   improvement from complete text/window equivalence.

The shared layout-test host also owns font resolution for assertions. Avalonia
12.1.3's default HeadlessFontManagerStub loads BareMinimum.ttf instead of installed
system fonts; a new guard proves Segoe UI is substituted (`WWW=39`, `iii=39`).
Use the existing Skia backend with headless windowing and UseHeadlessDrawing=false,
as the pixel-test hosts already do. Keep production dimensions and the original
fit/scroll requirements, rather than changing controls to suit stub metrics.
This test-only change introduces no window automation, service or execution-policy
change and preserves the shared production resources/styles initialization.

#### Baselines and deterministic verification

- Old font behavior: 9 failures / 2 passes in the 11-case default/resource/editor
  baseline. The implemented font preference/resource group passes all 11 cases.
- Wider pre-host-correction typography/theme/sidebar/control group: 474 passes.
- The first complete App run has 4,227 passes / 14 failures across 4,241 cases.
  Approval scrolling, breadcrumb fit, project-prompt reflow and response wrapping
  use substituted system-font metrics; a separate 30-case replay reproduces 12 of
  those failures. None of the original assertions or product dimensions is changed.
- The first metrics-guard attempt is interrupted after synchronous headless-session
  disposal stalls; it supplies no completed result. Async disposal then yields a
  completed failing guard (`WWW=39`, `iii=39`) before the shared-host correction.
- After Skia host correction, all 54 font/metrics/approval/breadcrumb/project-prompt/
  response cases pass, as do all 48 pixel cases and seven architecture constraints.
  Two fixture assumptions additionally depend on substituted fonts: the static
  diff sample does not overflow with real Consolas, and a fully readable filename's
  natural Segoe UI width is 97 rather than at least 100. The scroll test now supplies
  a genuinely overflowing left identifier and still proves right-pane independence;
  the filename assertion proves exact full-text fit when space permits, not an
  arbitrary pixel minimum. The completed pre-fixture-correction real-font App run
  has 4,236 passes / these six failures across 4,242 cases. All 17 metrics/diff/
  compact-toolbar cases then pass after fixture correction. The initial final-run
  process/handle later disappears without a TRX or terminal result; that interrupted
  attempt is not counted as a pass. Recovery uses the same recorded App/test assembly
  hashes and a bounded external driver with explicit process identity and exit
  metadata. The recovery completes on October 14, 2026 at 09:47 +08 with exit zero:
  all 4,242 App cases pass, with no failures/skips/aborts. Both the final TRX counters
  (`tests/font-app-complete-recovery.trx`) and `font-gate-exit.json` confirm completion;
  the driver, dotnet, testhost and xUnit child process have all exited. Testhost's low
  CPU usage is not a hang: its xUnit v3 child owns test execution, as the external
  process/stack inspection confirms. Scoped whitespace verification passes.
- A solution build initially reports missing assets in the new artifacts directory.
  Restore and the subsequent solution build pass with zero warnings/errors, as does
  the build after the shared-test-host change. The final test-fixture solution build
  also passes with zero warnings/errors (`solution-build-final-fixtures.log`).

#### Native screenshots and persistence

The production candidate is Debug/JIT, ordinary permission, 96 DPI; its App DLL
SHA256 is `1EDFB91149BFEDB175530B15103058677411173528073FA099CF29685BA0E958`.
The final App test DLL SHA256 is
`B93B49BA9C06074EE6FB29DD537F37EF74814307BF7F298F915FE46920B13326`.
Two independent synthetic data roots use the same assembly, not the older App's
UI automation handle. Python screenshots validate owner PID, foreground, size,
DPI, elevation and non-uniform content without activating the target.

| Evidence | Native state |
| --- | --- |
| `native-default/workspace-light-max.png` | Default light, 1920x1032 |
| `native-default/appearance-light-max.png`, `appearance-dark-max.png` | Default light/dark settings, 1920x1032 |
| `native-default/appearance-light-min.png`, `appearance-dark-min.png` | Default light/dark settings, 980x640 |
| `native-default/workspace-dark-min.png` | Default dark conversation, 980x640 |
| `native-inter/appearance-inter-max.png`, `appearance-inter-normal.png` | Explicit light UI Inter, 1920x1032 / 1280x820 |
| `native-inter/appearance-dark-default-normal.png` | Independent dark Default, 1280x820; the light editor still shows its stored Inter |
| `native-inter/workspace-inter-max.png` | Same candidate/fixture content with explicit Inter, 1920x1032 |

A maximized-button hover produces a snap overlay in one normal-size sample; that
frame is explicitly excluded. An attempted outward drag is rejected by the input
tool's bounds validation; its unchanged 980x640 frame is renamed `resize-rejected-min`
and is not ordinary-size evidence. A later title double-click supplies clean
1280x820 frames. No synthetic resize hook or DPI change is used in this slice.

Native light Inter selection really saves only `ThemeUiFonts.Light=Inter`;
switching to dark uses its independent Default and switching back retains Inter.
Both App and observer pairs exit zero normally; their processes disappear and
loopback port 51792 is released. In both roots, all 19 database tables, four-request
history and three existing artifacts remain unchanged. The default root's light
UI file is byte-identical; the option root has only the expected light font change.
No real user settings, provider request, approval setting or original artifact is
modified.

`font-crop-comparison.png` contains nine unscaled native crops. The candidate's
same-state Latin task text changes width/appearance between Default and explicit
Inter. The title-menu samples retain aligned buttons/text; the settings-shell
title can be compared with the cached Codex General title, not its full page.

#### Remaining limits

The font-comparison Codex capture is a light maximized conversation, not Appearance.
A later recovery capture, after re-enumerating the restarted Codex process, and
the follow-up passive capture both show a 526x601 light conversation with collapsed
sidebar, not an Appearance page. MainWindow still declares
980x640 minimum bounds: supporting the observed narrow reference is an explicit
remaining responsive gap, not proof of the earlier native DPI/clipping cause.
CSS/default-family agreement and localized shell/Latin crops do not
prove glyph-rasterization or full-window equality. Complete same-state Appearance
and PI-Desktop comparisons, native high-DPI/clipping root cause, the full current
theme/size/interaction matrix and the complete product objective remain open.
No JSON adapter, AOT, packaging, execution policy, commit, push or release changes.
<!-- /source: DEFAULT_UI_FONT_FIDELITY_ACCEPTANCE.md -->


---

<a id="input-hit-target-acceptance"></a>

## INPUT_HIT_TARGET_ACCEPTANCE.md

<!-- source: INPUT_HIT_TARGET_ACCEPTANCE.md -->
### Wrapped input and checkbox hit-target acceptance

#### Plan and design self-check

- The previous projection optimization made verified progress. This slice
  addresses the separately observed long-composer and checkbox blank-area gaps;
  the complete product/fidelity goal remains active.
- App owns both behaviors in shared FieldVariants and SharedCheckBoxTheme.
  Existing AppInput/AppCheckBox wrappers and application ports remain unchanged.
  No per-view patch, custom pointer handler, new I/O or execution behavior.
- Human multiline prose, prompt and composer fields share wrapping and vertical
  scrolling. Single-line and JSON editors retain their original code/address
  editing. Composer height stays bounded at 180 DIP; dimensions and typography
  are not duplicated in views.
- Checkbox template background covers its full native bounds, including spacing
  and vertical insets. The 16-DIP surface and 12-DIP glyph remain centered and
  unchanged. Native enabled/disabled, keyboard and three-state behavior is reused.
- No authorization, approval, timeout, output, audit, persistence, credential,
  JIT/Windows-x64 single-file publication or dependency-direction change.
- Both new handwritten C# test files contain one type and stay below size/method
  limits. New appearance rules stay in existing shared styles.
- Focused tests prove wrapping with finite viewports, unbroken text, raw draft
  preservation, vertical scrolling, role-specific input behavior, disabled pointer
  rejection, full checkbox hit regions and native three-state cycling.

#### Reproduced failures and focused regressions

- Original composer variant has no TextWrapping rule. Six narrow/wide light/dark
  long-input cases fail with NoWrap. Two human multiline/prompt cases independently
  reproduce the same omission; JSON editing passes its non-wrapping baseline.
- Original checkbox template has no background on its centered content grid.
  Two enabled hit-region cases and a blank-area three-state case fail. Disabled
  click rejection and single-line editing already pass and remain passing.
- All 174 focused composer, checkbox, field, shared-style, native-control-boundary
  and bound subagent-editor cases pass after the shared-style correction.
- Source snapshots and failing/passing TRX stay outside Git under
  `%TEMP%\MossAgent-input-hit-target-20261012`.

#### Actual native interactions and passive Python screenshots

- The updated external host runs without elevation on PID 17844 with a new
  synthetic root. No daily data, real provider credentials or approval/security
  controls are changed. No model send occurs: parent and child request counts
  remain zero throughout. There is no paid or public-network dependency.
- Clicking the enabled checkbox at x1260/y869, far to the right of its glyph and
  label, changes its bound draft to false. The stored profile remains true. Space
  with that checkbox focused restores true, without saving the synthetic draft.
- Actual typing creates an unsent 1,189-character draft. Its original bounds are
  706 by 180 DIP with 24 wrapped lines, horizontal offset zero and only vertical
  scrolling. Ctrl+Home reveals its start marker; the earlier end marker is visible
  at the bottom. The send/model/approval footer stays visible.
- Restore from 1920 by 1032 to 1280 by 820 DIP preserves the draft. Opening the
  inspector narrows the input to 423 DIP and increases wrapping to 41 lines;
  horizontal extent equals its finite viewport and the footer stays visible.
- Actual appearance-theme selection switches to dark. Returning to the workspace
  retains the same 1,189-character, 41-line draft and vertical-only scrolling.
  Native screenshots cover light/dark, expanded inspector and wide/normal windows
  at measured scale 1; other DPI combinations are not claimed.
- Native geometry compares the live input with the workspace draft but exports
  only lengths, equality flags, dimensions and scroll metrics, not arbitrary text.
  Screenshot text is synthetic. The passive Python helper remains outside Git.
- Evidence in the root's native directory includes checkbox-blank.png/json,
  checkbox-keyboard.json, composer-light/start/narrow-light/narrow-dark.png/json
  and final-assertions.json. Normal close confirms QA_FINAL_REQUESTS=0,0 and
  QA_UI_EXIT=0.

#### Final gates

- Complete App regression passes all 3,667 cases, with zero failures/skips
  (5 minutes 41 seconds). Evidence: input-hit-app-full.trx.
- The test host exits before the serial solution build. `dotnet build
  MossAgent.slnx --no-restore -m:1 -v minimal` succeeds with zero warnings/errors.
  Evidence: solution-build.log.
- Post-build replay passes all 15 new input/hit-target cases, and all seven
  architecture-constraint cases pass. Evidence: input-hit-post-build.trx and
  input-hit-architecture.trx, with their corresponding final logs.
- Final Roslyn audit confirms one type per new test file, both below 250 lines,
  and a longest method of 37 lines. Shared FieldVariants and CheckBoxVariants
  remain 33 and 66 physical lines, respectively, with no trailing whitespace.
  Evidence: source-audit-final.log.
- Included-file whitespace formatting verification and `git diff --check` pass.
  Existing uncommitted work is preserved; nothing is staged, committed or pushed.

This is not a claim of complete Codex visual parity, all editor states,
input-method behavior, all DPI combinations or full product acceptance.
The existing fixed-length held-response diagnosis remains unresolved.
<!-- /source: INPUT_HIT_TARGET_ACCEPTANCE.md -->


---

<a id="navigation-rail-component-acceptance"></a>

## NAVIGATION_RAIL_COMPONENT_ACCEPTANCE.md

<!-- source: NAVIGATION_RAIL_COMPONENT_ACCEPTANCE.md -->
### Navigation rail component acceptance

#### Scope and design self-check

- App owns `AppNavigationRail`, its resource labels and shared styles. MainWindow
  supplies existing workspace/main commands and keyboard-help content. No core,
  provider, tool, OS, persistence or dependency-direction change is introduced.
- Existing NewTask, Inspector, ToggleInspector and ToggleSettings commands remain
  authoritative. The rail does not call storage, tools or providers directly.
  Timed tasks remain explicitly disabled; audit/help are MossAgent capabilities,
  not an implementation of Codex's notification/account or automation services.
- Actions are local navigation only. Existing commands retain their approval,
  cancellation and execution behavior; this component adds no tool permission,
  filesystem access, process launch, network request or unbounded output.
- Component markup/code, shared variants and focused tests stay within 500 lines,
  each handwritten C# file has one type, and methods remain within 80 lines.
  The analyzer's geometry predicate is extracted without changing its rules.
- Focused tests cover measured geometry in both themes, actual transforms,
  workspace command routing, unchanged drafts/context and no provider requests.
  Negative cases cover missing commands/help, CanExecute rejection, owner
  replacement, hidden/disabled ancestors, detachment and forbidden composition.

#### Appearance ownership

- The rail is 52 DIP wide, with 36-DIP hit areas, 10-DIP corner radii, an 8-DIP
  body inset and 8-DIP inter-button spacing. Settings is centered 26 DIP above
  the window's client bottom rather than the previous 30-DIP inset.
- Light home selection uses its own `#E2E9ED` brush; dark uses `#2C333A`.
  This does not change the global settings selection palette.
- Home uses a filled 17-DIP icon and a shared optical translation of (-1,-0.5).
  Its visual center is (25.5,70), with the button center at (26,70). Settings uses
  a 15-DIP icon translated by (+0.5,+0.5); other rail icons retain the common size.
  These are optical corrections from native pixels, not per-view icon overrides.
- `ValidateSharedNavigationRail` and `MOSSUI002` reject Content, Padding and
  geometry replacements, including native-owner property forms. HelpContent and
  semantic command bindings remain supported.
- Explicit TranslateTransform is accepted only as a non-Control value object,
  alongside RotateTransform. Reflection and negative markup tests retain the
  rejection of Button, TextBox, Path, Border, ScrollViewer and other native visuals.
  Business RenderTransform overrides remain rejected.

#### Transform regression

The initial rail's render-transform setters compiled but the actual home icon had
an identity TransformGroup. Replacing the setter's text with a TranslateTransform
object still reproduced the same runtime failure. The broad task-running animation
was installing transform state on unrelated AppIcons during style initialization.
Avalonia 12.1.2 TransformAnimator initializes RenderTransform before observing the
conditional match, so a conditional global animation selector is not isolation.

The animation now belongs to SidebarActivityIconTheme, applied only to the task's
activity icon. Home alignment and the existing running-marker cases pass together.
Four theme/state regression cases also prove that ordinary icons retain their null
or styled translation when an activity marker becomes running or stops. Rotation
is required while running; initially hidden markers may initialize their theme
lazily. Binding jobs are drained before asserting the real visual state.

#### Lifecycle and functional boundaries

- Missing commands or HelpContent disable the corresponding entries. More is
  disabled when both of its commands are missing; command CanExecute is preserved.
- More actions close their own menu. Replacement of a command, help content or
  DataContext, hide/disable, hidden/disabled ancestors and detach close both menus.
  Ancestor subscriptions are removed on detach.
- Help content inherits its business DataContext. No unauthenticated help-page
  navigation, model transmission, automation execution or paid API call is added.
- Ordinary JIT debugging and Windows x64 single-file release behavior remain
  unchanged; there is no AOT or JSON source-generation change.

#### Validation status

Focused visual, navigation, lifecycle, negative-markup and drawing selection:
116 passed, zero failed/skipped. Full build, final native captures and serial
solution regression are recorded below. Compilation alone is not
a claim of pixel-identical UI or of the complete product goal being achieved.

#### Native pixel comparison, 2026-10-08

Python captured the actual ordinary-permission App/MainWindow with isolated
synthetic SQLite data at 96 DPI. Codex was captured passively; only its (8,8)
outer frame offset was cropped. Neither images nor extracted regions were resized.
Helpers, screenshots, synthetic data and the comparison script remain outside Git
in `%TEMP%/MossAgent-sidebar-20261007/rail-pixels-final-20261008`.

| Measurement | Codex | Previous MossAgent | Final MossAgent |
| --- | --- | --- | --- |
| Home ink x/y bounds | 17..33 / 62..77 | 19..32 / 63..76 | 17..33 / 62..77 |
| Home ink dimensions | 17x16 | 14x14 | 17x16 |
| Clock ink dimensions | 16x16 | 16x16 | 16x16 |
| Settings ink dimensions | 16x16 | 14x16 | 16x16 |
| Settings center from client bottom | 26 DIP | 30 DIP | 26 DIP |
| Home selection RGB | 226,233,237 | 236,239,241 | 226,233,237 |

The final light capture is 1280x820; the dark narrow capture is 980x640, bounded
by the real window's minimum height. Footer actions stay inside the rail and the
sidebar retains its own scroll viewport. Dark geometry is validated, but there is
no corresponding native Codex dark-state reference to claim pixel equality.

Native light navigation exercised help/Escape, More's inspector action, audit,
settings/return and New Conversation. A synthetic unsent draft and project context
survived inspector/audit/settings; New Conversation opened a new conversation slot
without transmitting the draft. Send remained disabled with no model selected.
The earlier QA process closed normally with exit code zero before its final host
was rebuilt, avoiding its DLL lock. No user database was seeded or migrated.

These measurements cover the rail, not every sidebar label, icon outline, hover
state, typography or function. Audit/help remain MossAgent-specific, timed tasks
remain unavailable, and the entire product goal is still active.

#### Final regression

- Validation-directory solution build passed with zero warnings/errors. The final
  QA host rebuilt successfully after the previous process released its DLL lock.
- After the analyzer's size-preserving refactor, one complete serial solution
  invocation passed 2779 tests with zero failures/skips: App 2115, Agent 58,
  Browser 76, Infrastructure 92, Platform 50, Providers 241 and Tools 147.
  TRX evidence is in `rail-validation-current/confirmed-current` under the
  temporary evidence directory. No flaky test was edited or excluded.
- Roslyn audited all twelve related handwritten C# files: one declaration per
  file and zero hard file/method-limit violations. Shared markup files remain
  under 500 lines; focused Git whitespace checks passed.
- Final dark help opened and Escape closed it; Python captured its actual popup.
  The desktop helper initially lacked input geometry for the new QA window;
  rebinding and refreshing geometry allowed the single retry to succeed. Its
  cached screenshot pixels were not used as appearance evidence. Both QA hosts
  subsequently shut down normally with exit code zero.
- The actual idle user program, verified to have no tasks and an empty unsent
  draft, closed normally without force termination. The ordinary Debug solution
  build passed with zero warnings/errors, and the same executable restarted with
  a non-elevated process token. Its actual 1920x1032 Python capture is non-black:
  home ink is x17..33/y62..77, settings is x18..33/y998..1013, and home selection
  is RGB (226,233,237), matching the measured light reference at the same size.
  Fresh accessibility retained the same MossAgent project and branch, empty
  composer with input focus, unselected model, AskEveryTime policy and disabled
  Send action. No task or synthetic project was added to the user's workspace.
- No publish, staging, commit or push is performed. Capture helpers and synthetic
  data stay outside the repository. Actual timed automation, Codex dark/hover
  parity, real provider runs and the complete product goal remain unverified.
<!-- /source: NAVIGATION_RAIL_COMPONENT_ACCEPTANCE.md -->


---

<a id="numeric-field-component-acceptance"></a>

## NUMERIC_FIELD_COMPONENT_ACCEPTANCE.md

<!-- source: NUMERIC_FIELD_COMPONENT_ACCEPTANCE.md -->
### 共享数字输入组件验收

#### 2026-10-07 归属与边界

1. 行为和外观归 App 的 `Controls` 与共享 `Styles`，不新增基础设施/UI 反向依赖。
2. 复用 `AppNumericUpDown` 与 Avalonia 原有数值处理、RepeatButton 和输入事件，
   不添加工具或运行时端口；`AppRepeatButton` 只封装连按按钮。
3. 本次没有文件、网络、命令、审批或持久化行为变更；测试不发起真实模型请求。
4. 新增类各自独立文件，共享主题与测试均低于文件/方法硬限制，最终规模审计另行记录。
5. 回归验证明暗主题、左右按钮、禁用、只读、范围边界、隐藏/恢复、主题切换、
   对齐和无障碍名称；C#/AXAML 禁止独立外观覆盖，同时保留合法功能属性。

#### 原因与实现

- 旧包装保留 Fluent 的数字输入模板，内部两枚箭头仍为原生 PathIcon，
  图形 16×8、按钮宽 34 DIP，独立分隔边框使端口字段的外观与共享输入不一致。
- `NumericFieldVariants.axaml` 统一内部表面、两枚 `AppRepeatButton` 和 `AppIcon`：
  图标槽 10×10 DIP、按钮宽 22 DIP、字段高 32 DIP，点击区域不按图形墨迹缩小。
  图标与按钮中心一致，表面圆角裁剪，无独立分隔线，明暗/悬停/按下/焦点/禁用
  使用共享主题颜色。数值解析、限制和连按逻辑仍由框架处理，不改写业务数值。
- 增大/减小使用本地化可访问名称与提示；实际 UIA 返回名称而非 `AppIcon` 类型名。
- C# 已按 Button 继承链覆盖连按按钮；新增 `ValidateSharedRepeatButtons` 补齐 AXAML
  的尺寸、Padding 和内容对齐门禁，包括原生所有者属性元素。共享 Styles 可以配置，
  业务页只提供功能属性。没有放宽原生视觉元素白名单。

#### 失败基线与自动化

- `numeric-before.trx`：新增 12 项中 8 失败、4 通过；真实失败包括 34-DIP 宽度
  与隐藏按钮后仍占 68 DIP，功能边界原先已经通过。
- `numeric-boundary-before.trx`：新门禁查询尚不存在时 10 失败、C# 既有拒绝 3 通过；
  这些是门禁注册检查失败，不能当作 10 个真实编译绕过。
- `components-proxy-focused-final.trx`：组件/邻接样式及代理修改合计 73 项通过，
  包含新增 12 项数值组件、13 项门禁与 21 项代理修改回归。
- 最初共享模板使用未允许的 DockPanel，被现有门禁拒绝；随后试图用 Setter 修改
  CLR ColumnDefinitions 被 AXAML 编译拒绝。最终改为允许的固定三列 Grid 与附加列位置，
  不添加例外或通过业务代码替换列集合。
- 完整解决方案构建、七项目测试、格式和规模审计结果汇总在 [UI_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#ui-component-acceptance)。

#### 普通权限原生验收

- 生产 App/MainWindow/组件、隔离 SQLite，浅色 1280×820、深色 980×820。
  实际点击增大将端口 7890 改为 7891，重启后恢复；深色减小到 7890 后取消，
  列表和只读数据库仍为 7891。没有用代码直接设状态替代这两次鼠标动作。
- Python 被动主窗口截图，同坐标原始端口裁片没有拉伸：
  `numeric-native-comparison.png` 实测箭头墨迹由 16×8 变为 10×6 像素。
  槽位 10×10 是布局度量，不能与墨迹尺寸混为一谈。
- 原始帧：`proxy-editor-validation-before-light.png`、
  `proxy-editor-validation-after-light.png`、`proxy-editor-restarted-dark.png`。
  当前 Codex 被动参考为 `codex-reference-final.png`；两应用的内容、窗口尺寸和检查器
  状态不同，不能据此计算或声称全页逐像素一致。
- 证据位于 `%TEMP%/MossAgent-ui-fidelity-qa-20261007-02/`。
  Python、临时探针和宿主均不入仓库。多 DPI、长按重复计时、键盘实体输入所有分支
  和所有页面仍未完成原生视觉验收；封装与门禁不等于整个 Codex 还原目标完成。
<!-- /source: NUMERIC_FIELD_COMPONENT_ACCEPTANCE.md -->


---

<a id="project-row-component-acceptance"></a>

## PROJECT_ROW_COMPONENT_ACCEPTANCE.md

<!-- source: PROJECT_ROW_COMPONENT_ACCEPTANCE.md -->
### 侧边栏项目行组件验收

#### 归属与约束

- 行为属于 App 的 Controls/Styles；业务页绑定现有目录命令，不改变依赖方向。
- 复用 `AppActionButton`、`AppIconButton`、`AppFlyout` 与 `AppContentControl`，
  不新增应用端口、不直接访问文件或数据库、不调用模型或工具。
- 授权、审批、超时、取消与输出限制仍由现有执行管线负责；组件不能批准执行。
- `AppProjectNavigationRow` 拥有内部组成、图标、有限宽度文本和摘要浮层，
  业务内容仍由页面提供。摘要继承项目 DataContext，卸载或替换目标时关闭。
- `ValidateSharedProjectNavigationRows` 与 `MOSSUI002` 拒绝覆盖 Content、Padding、
  宽高及内容对齐，包括属性元素和原生所有者限定写法。语义数据与命令绑定允许。
- 缺少目标或命令时操作不可用，已有命令的 CanExecute 继续约束实际执行。
- 自检要求为单类型文件、文件不超过 500 行、方法不超过 80 行；仓库结构审计
  在本轮补充完整窗口测试后报告 909 个文件、尺寸/类型/方法违规均为 0。

#### 回归证据

- 项目行封装、布局及真实工作区测试合计 27 项：正常语义绑定、拒绝外观覆盖、
  明暗主题、有限宽度长名称、12-DIP 图标与间距、垂直居中和独立展开命令。
- 完整 MainWindow 的明暗主题测试使用键盘事件验证真实目录展开/收起、摘要打开、
  编辑命令及目标身份、编辑器打开和摘要关闭，并断言未发生模型请求。
- 配合项目选择器和原生组件约束测试，专项复测 37 项全部通过。
- 初次完整窗口测试重现第二个会话的文字装饰跨线程绘制错误。共享样式改为
  每次加载新建文字装饰集合后，两种主题通过；非视觉装饰元数据仍受样式归属约束，
  原生 Button、TextBox、Border 等视觉控件的禁止规则没有放宽。
- 原生组件、局部样式、实例外观和复合组件组成的全面约束见 [UI_COMPONENTS.md](../UI_COMPONENTS.md)。
- 全量首轮发现五处仍要求业务页自行拼装项目行的旧断言；更新为检查共享组件和
  语义绑定后，最终解决方案复测 2034 项全部通过。首轮另有一项平台进程超时测试
  失败，未修改平台代码；该项单独复测与最终全量复测均通过。
- 本轮涉及 C# 的 whitespace 格式验证、`git diff --check` 与结构审计通过；
  没有暂存、提交或推送文件。
- 最终完整解决方案构建通过，0 警告、0 错误。

#### 原生截图

2026-10-06 使用仓库外 Python 脚本，按 PID 被动 PrintWindow 捕获普通权限预览。
测试程序使用生产 App 和 MainWindow，独立合成 SQLite、合成密钥及不可达模型地址。
脚本与截图均保留在系统临时目录，不加入仓库。检查边界稳定、原始尺寸和非黑像素。

| 场景 | 原始尺寸 | 证据 |
| --- | --- | --- |
| 明亮、最大化 | 1920×1032 | `accepted-light-max/workspace.png` |
| 暗色、窄窗口、超长项目名 | 980×640 | `accepted-dark-narrow/workspace.png` |
| 原尺寸局部对照 | 不缩放，只裁切 | `sidebar-reference-before-after.png` |

文件位于 `%TEMP%/MossAgent-sidebar-fidelity-qa-20261006-01`。
Codex 参考原始 1936×1048，仅裁掉外围 8 像素成为有效 1920×1032，未拉伸。
按灰度小于 150 的墨迹边界测量，新对话文本左上角由 `(91,103)` 修正为 `(92,106)`，
与参考左上角一致；项目标签横坐标由 91 修正为 93，与参考一致。
项目行纵坐标因不同数据分组不直接比较；实际图标墨迹与参考仍有约 1 像素差异。
长名称在窄窗口保持省略，不侵占其他操作。两个预览通过关闭信号正常退出，日志退出码 0。

#### 验证限制

前次原生鼠标操作因 Computer Use 的截图 ID/窗口命中检查失败，未取得可靠交互结果，
不能将其称为通过。暗色摘要由外部测试程序调用 ShowAt 设置，日志显示打开；
主窗口 PrintWindow 不证明独立弹出窗口的像素或鼠标操作成功。
摘要、编辑和键盘行为的通过证据来自 Headless 完整窗口测试，不替代真实鼠标验收。
文案、项目数据和参考主题不同，局部截图只能证明相关对齐调整，不能证明完整界面
已经与 Codex 像素级一致。整个 MossAgent 目标仍需继续验收。

同日后续使用唯一新窗口重新绑定，已实际点击验证摘要、编辑入口、重命名保存及
展开/收起；最终版暗色窗口还验证了输入错误和重试保存。详情见
[PROJECT_MANAGEMENT_ACCEPTANCE.md](UI_WORKFLOWS.md#project-management-acceptance)。这只补齐相应操作，不将前次失败或程序 ShowAt
触发的旧截图追认成成功，也不替代其他 DPI、悬停和全页面对照。

#### 2026-10-08 项目行生命周期与原生图标复测

##### 设计自检

- 改动仍属于 App 的共享 Controls/Styles，复用现有选择、展开与排序命令；
  不新增端口、数据库迁移、授权能力或工具调用，也不改变普通 JIT/单文件发布。
- DataContext、选择/展开命令替换、行隐藏/禁用、祖先隐藏/禁用，以及命令
  CanExecute 拒绝时关闭摘要并重置拖动；按钮和祖先订阅在卸载时全部解除。
- 生命周期测试覆盖两种主题的 20 个正常/拒绝场景。旧生产代码在该测试中
  16 失败、4 通过；修复后全部通过，不能只凭鼠标点外面自动关闭证明此修复。
- 三个相关手写 C# 文件分别为 162、136、230 行，各一个类型，Roslyn 审计
  没有文件、类型或方法硬限制违规；共享 AXAML 和外观门禁没有放宽。

##### 原生图标与对照

新图标只用于共享项目行，不替换全局文件夹。闭合/展开分别使用 11/13-DIP 槽，
13/11-DIP 图文间距和 1-DIP 描边；两种状态均保持 24-DIP 总分配与文字 X 起点。
展开资源的原始纵横比为 20×17，闭合为 18×18。布局及图标实际变换均由回归断言保护。
曾尝试移动文字，但原生结果变差，已经撤回；不以修改全局字体掩盖局部差异。

证据目录为 `%TEMP%/MossAgent-sidebar-20261007/project-row-accepted-20261008`。
仓库外 Python 按 PID 被动捕获生产 MainWindow 的独立普通权限 QA，不缩放图像；
Codex 当前参考只移除外围 8 像素。以 RGB 最大分量小于 175 的墨迹边界测量：

| 指标 | Codex | 修正前 MossAgent | 最终 MossAgent |
| --- | --- | --- | --- |
| 首个展开图标 | x69..82、y261..272，14×12 | x67..79、y263..273，13×11 | x69..82、y261..272，14×12 |
| 第二个展开图标 | x69..82、y393..404，14×12 | x67..79、y395..405，13×11 | x69..82、y393..404，14×12 |
| 闭合图标 | x70..81、y463..474，12×12 | x67..80、y465..475，14×11 | x70..81、y463..474，12×12 |
| 首项目文字 | x93..161、y263..275 | x93..160、y264..275 | x93..160、y264..275 |

原图为 `light-resting.png`，三列原尺寸裁片为 `project-row-comparison.png`。
第二版仅调整偏移后墨迹反而变成 15×10，未用它替代最终验收；最终采用专用几何资源。
相同墨迹边界不是路径轮廓、抗锯齿、整侧边栏或整窗像素完全相同的证据。

##### 实际输入与验证范围

- 普通权限原生 QA 为 1280×820；实际点击项目打开摘要，Escape 关闭摘要，
  点击独立展开按钮收起并重新展开任务，选中置顶会话及项目上下文保持不变。
- 深色最小窗口实际为 980×640；项目行、收起行与有限宽度标题可见，摘要实际
  点击打开并以 Escape 关闭。原图为 `dark-narrow.png`、`dark-summary-open.png`。
- 摘要截图使用包含独立弹出窗口的捕获；配置主题和合成数据由 QA 准备，
  不声称是鼠标配置或真实任务执行证据，也没有写入用户数据库或调用付费模型。
- 初次输入缺少可用截图几何而被拒绝；重新绑定唯一 QA 窗口并取得新截图后
  输入成功。失败调用和中间 `light-summary.png` 不作为打开摘要的成功证据。
- 最终专项 147 项通过，完整验证目录和普通 Debug 解决方案构建均为
  0 警告、0 错误；C# whitespace 校验和相关 Roslyn 结构审计通过。
- 最终同一轮完整串行解决方案测试全部通过：App 2135、Agent 58、Browser 76、
  Infrastructure 92、Platform 50、Providers 241、Tools 147，共 2799 项，
  0 失败、0 跳过。最终代码证据为同级 `project-row-validation/accepted-full-solution`；
  前一轮全量只包含第二版图标，不能替代该最终轮结果。QA 正常关闭，退出码 0。
- 用户主程序在重新确认无任务、空草稿后正常关闭，普通 Debug 构建后以未提升
  权限启动；新鲜原生状态保留 MossAgent 项目、分支、每次询问、未选模型、
  禁用发送及输入焦点。`normal-current.png` 为非黑色 1920×1032 原图，
  标准错误日志为空。没有设置用户数据目录覆盖，也没有暂存、提交、推送或发布。
- 本轮记录首项目文字约 1 像素墨迹差异、“置顶”标题约 4 像素纵向差异。
  后续标题已通过共享分组间距修正，证据见 [SIDEBAR_HEADING_ACCEPTANCE.md](UI_WORKFLOWS.md#sidebar-heading-acceptance)；
  未通过全局字号替换或不可验证的局部偏移掩盖。暗色没有相同状态的 Codex
  原生参考，其他 DPI、所有悬停/按下状态与全页面仍未逐像素验收，产品目标继续。
<!-- /source: PROJECT_ROW_COMPONENT_ACCEPTANCE.md -->


---

<a id="selector-focus-appearance-acceptance"></a>

## SELECTOR_FOCUS_APPEARANCE_ACCEPTANCE.md

<!-- source: SELECTOR_FOCUS_APPEARANCE_ACCEPTANCE.md -->
### Shared selector keyboard-focus appearance

#### Reproduced defect and correction (2026-10-16)

Headless rendering of the actual production styles confirms square black/white
focus frames inside ordinary and compact dropdown menus. The selected row already
has six-DIP corners, so the native rectangular focus adorner breaks its shape.
Pill selectors already own a rounded background highlight and must retain it.

The UI/UX skill's cross-platform visible-focus guidance and Avalonia styling
guidance inform this correction. Web-only cursor rules and mobile target sizes
are not imposed on this Windows desktop UI. Existing Codex-oriented geometry,
palette and interaction density remain unchanged; no new design system is added.

`Styles/ComboBoxVariants.axaml` assigns a shared native-based item-container theme
to `AppComboBox`. Its noninteractive `AppBorder` focus adorner uses the existing
`BorderFocusBrush`, a two-DIP stroke and the adorned element's current radius.
It changes no dimensions, padding, content position or command behavior. Theme
and radius changes update the existing adorner without rebuilding the row.

Crucially, this is an item-container **theme default**, not a global visual style
that overrides explicit component ownership. Existing pill-selector themes and
local focus owners can still opt out. The pill selector's production file is
unchanged, and its original background highlight remains intact.

#### Ownership and safety self-check

- App owns appearance through its existing shared styles and native Avalonia
  focus lifecycle. No domain, provider, persistence or dependency direction changes.
- No new service, global state, tool, file operation, network call, approval policy
  or asynchronous lifetime is introduced in the application.
- Production changes are twelve AXAML lines in one shared style file. The two
  new handwritten C# test files contain one type each, 90/174 physical lines,
  with maximum method sizes of 26/45 lines. No new application C# is needed.
- Pixel and routed-input checks cover the successful paths and the boundaries:
  explicit opt-out, existing pill appearance, disabled input, Escape without
  selection, hit-tested pointer selection and exact selection-change counts.

#### Final verification

- Final 24-case regression baseline uses an identity-verified original App DLL:
  all twelve ordinary/compact rounded-corner cases fail at the corner pixels;
  four theme/input cases fail at the absent shared adorner. The eight existing
  pill/local-owner preservation cases already pass. Baseline: 8/24, not all green.
- Final complete pixel assembly: **260/260**, including all 24 new cases, across
  Light/Dark and 100%/125%/200% render scaling where specified.
- Related App tests: **198/198**, matching independent discovery. This includes
  existing theme choices, selector native behavior, settings editors and connected
  provider/automation workflows. Architecture constraints: **7/7**.
- The three final disjoint scopes total **465** passing cases, with matching
  individual TRX rows/unique IDs and no failures, skipped or aborted cases. Neither
  baseline failures nor preliminary candidate runs are added to this total.
- Solution build: zero warnings/errors. Scoped formatting and source-size checks
  pass. Final post-test build and assembly-identity checks are recorded alongside
  the raw results; generated evidence and probe scripts stay outside the repository.

#### Preserved failed attempts

The first candidate applies the adorner with a global `ComboBoxItem` style. Its
pixel tests pass, but two existing `ThemePaletteChoiceInteractionTests` fail
because the style overrides the pill theme's explicit null adorner. Those old
assertions are not weakened or changed. Moving the default into the native-based
item-container theme repairs that regression. The final new tests explicitly
preserve both pill and local ownership; all original related tests pass.

The first baseline fixture also tried changing the navigation method while
refocusing the same already-focused item. That did not remove its old focus
state. The final fixture first moves focus to another item and then back; the
identity-verified final baseline above is the acceptance baseline. The initial
formatting check reports one initializer line break; the corrected check passes.

#### Evidence and limits

Evidence: `%TEMP%\MossAgent-dropdown-review-20261016`.

- `selector-baseline-final.trx`, `baseline-final/`: final unchanged-style baseline.
- `pixels-final.trx`, `final/`: complete final pixel results and raw rendered frames.
- `app-related-final.trx`, `architecture-final.trx`, `final-test-summary.json`:
  final related results; `app-related-discovery.txt` verifies the 198-case scope.
- `app-related.trx`: the preserved two-failure first candidate. Earlier baseline,
  candidate and pixel reports remain separate, not successful final evidence.
- `before-after.png`: labeled, enlarged off-screen Light/Dark comparison.
- `format-final.log`, `source-audit.json`, build logs and hash CSVs: hygiene,
  compilation and tested-assembly identity evidence.

The separately completed 4,794-case App run starts before this correction and
retains the earlier App DLL throughout; it is not a full run of this new UI
revision. Its allocation investigation is recorded in
[SIDEBAR_REFRESH_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#sidebar-refresh-optimization-acceptance). No causal repair of those historical
intermittent allocation failures is claimed.

Existing desktop windows, user databases and Codex are not operated, captured or
restarted. These synthetic Headless checks do not replace the user-postponed
native captures, same-state Codex comparison, native DPI transitions or complete
product acceptance. No staging, commit, push or publish is performed. Windows x64,
JIT, single-file publishing and existing JSON behavior remain unchanged.
<!-- /source: SELECTOR_FOCUS_APPEARANCE_ACCEPTANCE.md -->


---

<a id="settings-item-component-acceptance"></a>

## SETTINGS_ITEM_COMPONENT_ACCEPTANCE.md

<!-- source: SETTINGS_ITEM_COMPONENT_ACCEPTANCE.md -->
### 设置列表统一组件验收

本记录仅覆盖本轮组件封装和拒绝约束，不代表整个 MossAgent 或 Codex 像素验收完成。

#### 设计与范围

- 行的外观与交互组成由 App 的 `AppSettingsItemRow` 拥有，复用现有 Controls/Styles。
  网络代理、MCP、自定义代理与子代理四个列表改用同一组件，不再各自拼装文字与操作。
- 标题、说明、协议和状态徽标采用有限列、省略与完整工具提示；同一行文字居中。
  设置项、提供商和模型操作共同使用 `ProviderSettingsRowVariants.axaml` 中一项
  24-DIP 高度、最小高度和垂直对齐规则，避免两个尺寸来源。
- 默认行、`compact`、`multiline`、`endpoint` 的尺寸和字体由共享样式维护。
  测试发现继承 UserControl 的样式键不会匹配派生类型选择器，改用组件拥有的
  `app-settings-item-row` 标识后，紧凑与两行变体实际生效，而不是仅存在于源码。
- 页面传语义数据、标签与原有命令。动作缺少命令或目标时禁用，无标签时隐藏；
  CanExecute 仍生效。启用/停用只切换同一个按钮的标签，动作始终携带当前行数据。
- 不新增应用端口，不在 UI 读写文件、数据库或启动进程；不改变持久化结构，
  工具授权、审批、超时、取消、输出限制和审计策略不变。类型独立成文件且不超限。
- 代理仍只展示主机和端口；MCP 继续使用安全摘要，不展示 URL 凭据、路径、查询、
  命令参数、环境变量与请求头。已有保存、切换与测试行为保留。

#### 自动检查

- 本轮新增 30 项回归通过，覆盖明暗主题、320/640-DIP 组件布局、长文字、动作居中，
  隐藏槽位、紧凑和两行变体、标签更新、当前目标、CanExecute、缺少目标/命令，
  真实代理页的测试与编辑绑定、真实 MCP 页的合成切换/编辑命令绑定，以及安全摘要。
- C# 通过 MOSSUI002 拒绝 Content、Padding、尺寸、内容对齐和颜色覆盖；
  AXAML 原生所有者限定、属性元素和替换内部子控件同样被现有行门禁拒绝。
  数据、命令、状态、共享变体及 Subtitle 的 MultiBinding 允许通过。
- 仓库外验收项目真实编译：原生 Button 被 MOSSUI001 拒绝；共享行的 Content 和
  Height 赋值被 MOSSUI002 拒绝。AXAML 真实执行导入生产 App 的规则，分别拒绝
  原生 Button 与 `Control.Height` 覆盖；合法用法通过 C# 编译及 15 项通用 AXAML 门禁。
  每次拒绝均核对预期诊断标识或规则原因，不将任意构建失败当作拒绝验证。
- 完整解决方案 1,856 项测试通过，其中 App 1,367 项；构建 0 警告、0 错误。
  本轮 C# 空白格式检查、差异空白检查、872 个 C#/AXAML 的文件/类型/方法体积审计通过。
  本轮没有暂存、提交或推送，既有改动保留。

#### 真实像素证据

临时宿主使用生产 App/MainWindow、独立数据目录和仅内存中的合成配置。
日志确认普通权限运行，没有代理探测、模型请求、MCP 连接或连接配置保存；启动的
空配置加载不包含外部服务。主题选择只涉及隔离的外观设置，真实应用数据不变。
临时宿主均通过关闭信号正常退出。
六个独立数据目录的只读 SQLite 复核确认代理、提供商、模型、MCP、代理配置、
任务、消息及工具审计均为零条；合成列表没有持久化，最终没有遗留的验收进程。
Python 仅被动获取指定拥有 PID 的窗口像素，不自动操作 Codex 或 ChatGPT。

证据与脚本均在仓库外：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-settings-item-qa-20261006-01`。

- `native-before-proxy.png`：980×700，复现长代理名称覆盖测试/编辑操作。
- `native-after-proxy-light.png`、`native-after-proxy-dark.png`：980×700，名称省略，
  协议与默认状态保留，测试/编辑在独立区域，深浅主题均未返回黑图。
- `settings-row-before-after.png`：同一坐标区域的前后原始像素裁片，未缩放。
- `native-after-mcp-light.png`：1920×1032，MCP 长名称与启用/编辑操作分离。
- `native-after-agent-light.png`、`native-after-subagent-dark.png`：980×700，
  紧凑代理行与两行触发条件复用同一组件，长名称不会遮住状态。

没有将截图或合成键盘事件当作物理鼠标/键盘验收。本轮没有真实执行代理测试或
MCP 启用连接；命令目标由 Headless 页面和既有 ViewModel 回归覆盖。全部 DPI、
所有页面/交互状态和与 Codex 同状态的逐像素对照仍未完成；整体目标保持进行中。
<!-- /source: SETTINGS_ITEM_COMPONENT_ACCEPTANCE.md -->


---

<a id="shared-interaction-states-acceptance"></a>

## SHARED_INTERACTION_STATES_ACCEPTANCE.md

<!-- source: SHARED_INTERACTION_STATES_ACCEPTANCE.md -->
### Shared field focus and disabled primary states

#### Design and scope

This slice follows the UI/UX skill's desktop-relevant guidance: keyboard focus
must remain visible, hover must not displace it, and disabled controls need native
disabled behavior and theme-appropriate visual treatment. It does not replace the
existing Codex-inspired visual system or apply mobile hit-target requirements.

Required design self-check:

1. App owns the shared appearance. Dependency directions stay unchanged.
2. Existing AppInput/AppEditableChoice and AppButton/AppIconButton wrappers,
   native focus states and command availability are reused; no new service port.
3. No tool execution, file/process/network access, authorization or approval
   behavior changes. Test screenshots render isolated Headless windows only.
4. Production changes are shared AXAML selectors and semantic theme resources;
   no handwritten production C# or page-local appearance overrides are added.
   Source files remain under the repository limits.
5. Pixel/state tests cover focus plus hover, editing, blur, theme changes,
   unavailable-command pointer/keyboard rejection and re-enabled execution.

#### Reproduced baseline

The unchanged production UI fails all 20 new pixel/state cases:

- Twelve text/password/multiline/editable-choice cases lose their focused border
  as soon as the pointer enters. The later pointerover template selector changes
  the focus brush from #8E8E8E to the idle #E5E5E5 (Light) or #424242 (Dark).
- Eight primary/send-button cases use #D9D9D9 in Dark, whether starting in Dark
  or switching from Light. Native template presentation is verified, not merely
  the wrapper's property values.

An initial test compile required the Avalonia 12 physical-key parameters and
ContentPresenter namespace; that was a fixture correction, not a product defect.
The final baseline TRX records 20 executed failures, not a compile-only baseline.

#### Change

- Idle input hover no longer matches a field with keyboard focus within it.
  The existing focused border, radius, thickness and editing behavior remain.
- Disabled primary buttons and the composite ghost/primary send variant share
  theme-aware semantic brushes. Light retains its original #D9D9D9/#7A7A7A;
  Dark uses #3A3A3A/#8B8B8B instead of a bright Light-mode surface.
- Command availability, enabled appearance, dimensions, caption controls,
  composer borderless editing and custom color-editor surfaces remain owned by
  their existing components/styles. No alternate template is introduced.

#### Validation

- All 20 new cases pass after the shared-style correction. They cover actual
  Light/Dark rendering, 100/125/200-percent render scales, hover/focus coexistence,
  keyboard editing, stable geometry, blur and disabled-command behavior.
- The final complete pixel suite passes 208/208 after final formatting/build,
  including those 20 cases. The earlier 20-case and 208-case runs are not added
  again to the final result. The two shared tests use the production style tree.
- The focused App run passes 437/437, covering fields/choices, composer behavior,
  checkbox interactions, custom theme-color fields, shared control styles,
  caption controls, focus/dialog behavior and visual-component enforcement.
  Together these two non-overlapping final scopes pass 645 cases, with no
  failures, skipped/not-executed or aborted cases. This is not a full App or
  eight-assembly result; the previous unexplained allocation failure is not
  investigated or claimed repaired by this appearance-only slice.
- `dotnet build MossAgent.slnx --no-restore -v:minimal -m:1 /nodeReuse:false`
  succeeds with zero warnings/errors. Scoped whitespace-format verification,
  XML parsing, source-size, one-type and whitespace/conflict checks pass.
  Two test assemblies and three matching production App copies retain their
  verified identities across their respective final validation boundaries.

Evidence is outside the repository at
`%TEMP%/MossAgent-interaction-states-20261016`: `interaction-before.trx`,
`interaction-after.trx`, `app-related.trx`, `pixels-current.trx`, build/format
logs, assembly hashes and raw state PNGs. `before-after.png` combines unchanged
100-percent crops for presentation, not a reference-window pixel comparison.
Existing desktop windows and user settings are not accessed. Native screenshots
remain user-postponed; Headless render scaling does not establish native Windows
DPI or same-state Codex parity. Nothing is staged, committed, pushed or released.
<!-- /source: SHARED_INTERACTION_STATES_ACCEPTANCE.md -->


---

<a id="shared-navigation-typography-acceptance"></a>

## SHARED_NAVIGATION_TYPOGRAPHY_ACCEPTANCE.md

<!-- source: SHARED_NAVIGATION_TYPOGRAPHY_ACCEPTANCE.md -->
### 共享导航字号与分组文字验收

本文记录 2026-10-07 的历史状态；当前任务字号为 14 DIP、行高 18 DIP，
项目字号为 13 DIP。后续原生基线证据见 [SIDEBAR_REFRESH_OPTIMIZATION_ACCEPTANCE.md](ENGINEERING.md#sidebar-refresh-optimization-acceptance)，
不以本文旧字号和图标数据覆盖当前实现。

日期：2026-10-07。此记录证明共享导航外观的一次实测修正，
不代表 MossAgent 已与 Codex 整窗逐像素一致。完整产品目标继续进行。

#### 设计自检

- 行为属于 App 的共享 Styles；基础和组合组件、页面绑定及依赖方向不变。
- 继续复用 `AppSettingsNavButton`、`AppActionButton` 和 `AppText` 的语义变体，
  没有新增包装层、服务定位器、设置端口或页面独立样式。
- 不改变工具授权、审批、超时、取消、输出限制或审计。使用普通权限临时宿主，
  Codex 仅通过 Python 被动截图；没有对 Codex 输入、移动或激活操作。
- 共享分组文字增加独立主题资源，不修改所有控件使用的 `TextSubtleBrush`。
  App.axaml 为 491 行；971 个 C#/AXAML 文件的规模、单类型和方法审计无违规。
- 新增八项明暗主题回归，补强四个设置导航状态的字号与行高断言；
  检查真实组件内的字体和图标中心，不仅检查 AXAML 中出现了某个数值。

#### 修改与原因

- 设置导航和主侧边栏操作的字号从 13 DIP 改为共享 14 DIP。
  文字行高为 18 DIP，保持图标和文字中心一致，避免默认奇数行高造成半像素差异。
  不放宽原有中心一致的测试要求。导航图标仍为 14 DIP，主侧边栏操作图标仍为 12 DIP。
- 设置导航字号归 `SettingsLayoutVariants.axaml`，移除组件主题中的旧字号。
  主侧边栏操作归 `NavigationActionVariants.axaml`；文字变体归 `TypographyVariants.axaml`。
- “置顶”“项目”“最近”共享 14-DIP/18-DIP 分组文字变体。
  浅色 `SidebarSectionTextBrush` 为实测 `#686A6C`，深色保持原有 `#8E8E8E`。
  不修改其他弱提示、操作图标或整个主题的弱文字颜色。
- 项目和会话行、品牌、字段和其他文字层级未作无证据的全局字号替换。
  会话标题继续为 13 DIP，并通过独立断言防止本次变体误改。
- 单独将 Inter 换为 Segoe UI 的基线实验没有解决导航文字宽度差异，
  且设置标题墨迹 Y 从 106 变为 108；因此本次不替换默认字体，
  不改变 `UiFontPreference.Default` 的旧含义，也不覆盖任何已保存字体或配色。

#### 原始像素证据

证据目录：`C:\Users\wzheng\AppData\Local\Temp\MossAgent-font-qa-20261007`。
Python `PrintWindow` 截图不调整窗口或输入，校验 PID、稳定原生尺寸及非黑色帧。
对照脚本在原图上裁片，灰度低于 160 的阈值用于以下文字墨迹尺寸，不缩放图片。

| 项目 | Codex 参考 | MossAgent 修改前 | MossAgent 最终 |
| --- | --- | --- | --- |
| 设置导航“外观”墨迹 | 28×12 | 26×12 | 28×12 |
| 主侧边栏三字操作墨迹 | 42×12 | 39×12 | 42×12 |
| 主侧边栏“项目”墨迹 | 26×12 | 23×12 | 26×12 |
| 主侧边栏分组文字实色 | #686A6C | #8A8A8A | #686A6C |
| 设置“外观”标题墨迹 | 47×23 | 47×23 | 47×23 |
| 设置标题墨迹顶部 Y | 106 | 106 | 106 |

设置参考为用户原图 `codex-clipboard-0a8430e8-0091-4d07-a447-4acaa1ba177d.png`。
主侧边栏参考为本轮取得的 `codex-current.png`，GUI 所属进程为 ChatGPT.exe，
路径对应 Codex 26.930.7945.0。主操作的两边文字分别为“新聊天”和“新对话”，
三字墨迹尺寸一致不是字形轮廓完全相同的证据。

最终对照为 `settings-comparison-accepted.png/json` 和
`sidebar-comparison-accepted.png/json`，修改后源图分别为
`settings-final-light.png` 和 `sidebar-final-accepted.png`。
`segoe13.png` 是宿主通过现有 ViewModel 准备的字体实验，不是鼠标选择字体的验收。
早期 `baseline-segoe.png` 的输入被窗口边界拒绝，仍然是 Inter；
它及 `compare-font-families.png/json` 不作为字体选择或最终结果证据。
证据目录中的 `evidence-status.md` 明确区分这些中间产物。

#### 原生交互与自动回归

- 独立宿主运行生产 App/MainWindow，数据只写临时目录，所有进程报告未提升权限。
  配置由宿主准备；本轮没有模型请求，不声称通过真实鼠标创建了提供商和模型。
- 在 1280×820 主页面实际打开设置、切换深色、返回主页面，
  保存 `settings-final-light.png`、`settings-final-dark.png` 和 `sidebar-final-dark.png`。
- 请求 960 DIP 宽度，生产最小宽度约束实际为 980×820。
  实际打开设置、切换深色、进入 AI 提供商、返回主页面；
  保存四张 `*-narrow-light/dark.png` 及 `providers-narrow-dark.png`。
  JSON 确认 Theme=Dark，临时宿主正常关闭，最终没有存活的 QA 进程。
- UIA 文本偶有滞后，输入使用当前截图坐标，每个动作后刷新；
  结果以新截图及已保存主题确认，不根据旧索引或焦点信息宣称操作成功。
- 修复前，补强的设置导航断言四项失败；八项新侧边栏用例全部因旧字号失败。
  中间 14-DIP 默认行高仍有半像素中心差异；共享 18-DIP 行高修复后通过。
  最终相关专项 71 项通过，完整解决方案 2,386 项通过，0 失败、0 跳过。
  `solution-results` 中七份独立 TRX 已逐份核对，App 为 1,794 项。
- 完整解决方案构建 0 警告、0 错误；两份 C# 测试的限定文件格式检查通过。
  没有暂存、提交或推送；Python 脚本、截图和宿主不加入仓库。

#### 仍未证明的范围

- 设置导航图标起始 X 仍比参考小 1 像素；“主题”节标题墨迹宽仍为 26 而参考为 27。
  导航文字与图标的光学偏移、默认字体选项、项目/会话行、其他图标与浮层仍需继续测量。
- 没有新鲜的 Codex 深色设置参考；深色截图证明运行与布局回归，不证明深色逐像素一致。
- 本轮只覆盖两个原生窗口宽度及上述状态，没有穷尽所有页面、DPI、悬停、焦点和错误交互。
  原生窗口大小和内容不同，若干裁片指标相同不能替代整体保真验收。
<!-- /source: SHARED_NAVIGATION_TYPOGRAPHY_ACCEPTANCE.md -->


---

<a id="sidebar-search-component-acceptance"></a>

## SIDEBAR_SEARCH_COMPONENT_ACCEPTANCE.md

<!-- source: SIDEBAR_SEARCH_COMPONENT_ACCEPTANCE.md -->
### Sidebar search component acceptance — 2026-10-08

#### Reproduced baseline

- The previous ordinary-permission real App host accepted a query with no matches,
  leaving a project heading and an otherwise blank sidebar. Its popup supplied no
  empty-result explanation, and a closed popup did not clearly identify an active
  filter. Baseline pixels are in `%TEMP%/MossAgent-sidebar-20261007/search-baseline`.
- The new focused layout test caught a 14-DIP clear icon. The compact button class
  only controls the hit area, not its child's icon size. The owning shared search
  variant now sets that icon to 12 DIP instead of changing every compact button.

#### Required design self-check

1. App owns catalog-search presentation and the shared visual component. It introduces
   no concrete Infrastructure or platform dependency into a view or view model.
2. Filtering and clearing reuse `WorkspaceTaskSearchViewModel` and its command.
   No new repository, tool or persistence port is necessary.
3. Search changes only local presentation, never sends a model request, authorizes
   tools or performs file/SQL/process operations. Existing task policy and limits
   remain unchanged; the search query is not newly persisted or logged.
4. The adapter and control have one type per handwritten C# file and remain below
   250 physical lines. Layout and appearance stay in Controls and shared Styles.
5. Focused tests cover live catalog updates, real view bindings, disabled/missing
   commands, lifetime rejection, popup focus and encapsulation in C# and AXAML.

#### Behavior and boundaries

- The adapter forwards the existing query and considers visible projects, pinned
  tasks and recent tasks together. A match outside the selected project is not an
  empty result. Collection replacement and live result updates refresh the state;
  disposal removes subscriptions and rejects direct late writes/clear invocation.
- The component owns its trigger, popup, two-way field, clear action and localized
  notice. Business views only bind semantic properties. The trigger highlights an
  active query and exposes it in its accessible name and tooltip.
- Ctrl+K opens and selects the query only when attached, enabled and visible,
  including ancestors. Ctrl+Shift+K is not intercepted. Escape closes without
  clearing the query or changing conversation selection/draft.
- Generation checks invalidate deferred focus after closure, context replacement,
  hidden/disabled ancestors or detach. Closing restores a still-valid previous
  editor only when doing so does not replace a newer editor's focus.
- Clearing reuses the existing command, hides the clear icon for an empty query and
  keeps the search open and focused. A missing command or false CanExecute disables
  the action. Search subscriptions and top-level key handling follow visual lifetime.
- Shared styles own the 270-DIP popup, flat input, 12-DIP clear icon, centered controls
  and active brush. `ValidateSharedSidebarSearch` and `MOSSUI002` reject business
  content/child, padding and dimension replacement, including qualified properties.

#### Runtime evidence

- An ordinary-permission App/MainWindow host used isolated synthetic SQLite projects
  and tasks. No synthetic records were inserted into the user's application data.
- Native interaction covered Ctrl+K, selecting the previous query, unmatched text,
  closing while preserving the filter, clearing/restoring all rows, an external
  project match and a pinned-only match. The selected MossAgent conversation and
  its project context remained unchanged. A synthetic composer draft survived.
- Light 1280x820 and dark 980x640 states were inspected at 96 DPI. The first launch
  was captured at 120 DPI before the desktop automation environment changed scaling;
  that frame is not used for the same-DPI comparison or a 120-DPI search claim.
- Passive Python capture rejects black/uniform frames. The popup captures use
  passive desktop capture with owned-popup bounds, since PrintWindow of the parent
  alone does not capture separate native popup windows. No input or resize is used
  by either capture command. Scripts and evidence remain outside the repository.
- `search-verified-20261008/sidebar-search-comparison.png` uses current Codex and
  restored MossAgent native crops at 96 DPI, without resizing. Their final project
  text band is at y=750; sampled project text-band centers differ by at most half
  a pixel. This confirms the search wrapper retained the corrected row rhythm,
  not identical font rasterization, folder paths or complete icon fidelity.
- Popup input focus is absent from the main window's accessibility tree; native
  input/clear/select-all and draft preservation have separate screenshot evidence.
  The updated ordinary main-program Escape smoke check reported the composer edit
  as focused again. More complex restoration and late-focus rejection remain
  covered by headless tests, not independent native race/detach interaction.
- A Codex opened-search screenshot was not available. The search popup is not claimed
  to be a pixel-identical Codex search surface. Rail workflows, remaining icon/font
  details and whole-product acceptance are incomplete; the overall goal stays active.

#### Validation

- Focused search tests including the actual MainWindow bindings passed 31 cases.
- Solution artifacts builds passed with zero warnings and errors.
- Focused whitespace verification passed. Roslyn audited eight related C# files:
  one type per file and zero file/method hard-limit violations.
- The first full run exposed two obsolete source-contract assertions after moving
  search markup into Controls. Header-order verification now locates the shared
  component; multi-step flyout verification includes its owned popup and retains
  the original exact count and dismissal checks. The corrected focused run passed
  all 37 search/header/flyout cases.
- One final serial solution test invocation passed App 2070, Agent 58, Browser 76,
  Infrastructure 92, Platform 50, Providers 241 and Tools 147: 2734 passed, zero
  failed and zero skipped. TRX files are under
  `%TEMP%/MossAgent-sidebar-20261007/search-tests/confirmed-final`.
- The ordinary Debug solution build also passed with zero warnings and errors.
  The updated main program was started from its ordinary Debug path; its actual
  process token was not elevated. Python captured a non-black 1920x1032 frame and
  its opened search popup. Ctrl+K/Escape smoke checks left no query or draft behind.
  The isolated QA host closed normally with exit code zero. No AOT, publish,
  staging, commit, push, paid model call or synthetic user-data seeding occurred.

#### Header icon fidelity — 2026-10-10

Fresh passive native screenshots at 96 DPI reproduce oversized header glyphs:
Codex's bell and search ink bounds are both 12x12 pixels, while MossAgent's are
12x15 and 15x15. Their ink centers are also 2.5 pixels higher in MossAgent.
Only Codex's eight-pixel external window frame is cropped; neither app is scaled.

Design self-check:

1. App's existing shared navigation appearance owns the header variant; views
   retain semantic classes rather than local colors, dimensions or transforms.
2. Search retains its existing catalog, shortcut, clear and focus contracts.
   The bell still opens the audit inspector, not a fabricated notification feed.
3. No model request, tool execution, approval/policy change, filesystem access or
   persistence migration is introduced by these appearance changes.
4. Related handwritten files retain one type each and stay within size limits.
   Bell geometry is header-specific; the general bell/search resources are unchanged.
5. Focused tests cover the real MainWindow in both themes at three widths, action
   bindings, retained hit areas and isolation from general icon-button appearance.

Final header evidence is under
`%TEMP%/MossAgent-header-fidelity-20261010-0800`:

- `moss-light-final.png` retains native 1920x1032 dimensions. Bell ink bounds are
  `(251,64)-(263,76)` and search bounds are `(283,64)-(295,76)`, matching the
  current light Codex reference at the measured threshold. Stroke curves and
  antialiasing are not claimed to be pixel-identical.
- Dark native 1920x1032, 1280x820 and 980x700 screenshots retain the same 12x12
  window-relative bounds. No current dark Codex reference was captured, so these
  prove MossAgent's geometry consistency, not dark-theme Codex color fidelity.
- Header-specific geometry leaves the general icon resources unchanged. Styles
  target only direct child glyphs; a descendant selector initially affected the
  search popup's clear icon and was rejected by the existing tests.
- Native clicks opened the actual audit inspector and search popup. Ctrl+K from
  the composer and Escape restored its focus and preserved the synthetic draft.
  Clicking the search trigger instead retains trigger focus on close; that path
  is not reported as composer restoration.
- The ordinary-permission, isolated QA host closed normally with `QA_EXIT=0` and
  `QA_REQUEST_COUNTS=0,0`. No model request or tool execution was performed.
- The icon-markup contract now recognizes unconditional, explicitly typed shared
  variants only when their referenced geometry exists. Negative cases reject
  hover-only defaults, incorrect target types, missing resources and bindings;
  the label and manual-child restrictions remain enforced.

Validation: the final header/search/command/markup subset passed 84 cases; the
same solution run passed 3,888/3,888 before the subsequent model-picker lifetime
refactor. The final source's broader focused run passed 147 cases, its solution
build had zero warnings/errors, and whitespace plus one-type/size audits passed.
The subsequent full run passed 3,889/3,890 with no skipped tests: one unchanged
`ProviderProxyRuntimeTests` case reached its five-second fixture cancellation at
SQLite connection opening, before discovery/streaming. Its unchanged 12-case
isolated run passed, but this is not a full-suite pass or a root-cause fix. Logs
and TRX are in `final-optimized-full` and `provider-proxy-isolated.trx` under the
evidence directory. Overall visual/product acceptance remains incomplete.
<!-- /source: SIDEBAR_SEARCH_COMPONENT_ACCEPTANCE.md -->


---

<a id="theme-thumbnail-artwork-acceptance"></a>

## THEME_THUMBNAIL_ARTWORK_ACCEPTANCE.md

<!-- source: THEME_THUMBNAIL_ARTWORK_ACCEPTANCE.md -->
### Theme thumbnail artwork acceptance

#### Scope and ownership

This slice changes only the shared App theme-preview scene and appearance
variants. It corrects the miniature conversation artwork, not the whole settings
page, theme catalog, preset-action semantics or complete Codex fidelity.

1. App owns the artwork; no module dependency or service contract changes.
2. Existing theme selection, palette Save/Reset and preference persistence remain
   authoritative. No new tool, port, file access, SQL, HTTP or process work is
   introduced in UI code.
3. Native checks use independent synthetic data directories and ordinary
   permissions. Approval/security controls and real user data remain untouched.
4. Shared wrappers and variants remain mandatory. The scene stays 80x60 with a
   66x46 inner screen and two clipped 40x60 halves for System. No production C#,
   JSON, JIT, AOT or Windows x64 single-file packaging changes are introduced.
5. Tests cover geometry, colors, System clipping, application-theme independence,
   selection outline and existing visibility/hit-test boundaries. Compositor
   pixel cases cover Light/Dark application themes at 100%, 125% and 200% scale.

#### Measured corrections

- Move the conversation inset from `3,4,3,2` to `4,4,4,3`.
- Narrow the third user line from 24 to 23 pixels; keep other line widths.
- Narrow the miniature composer to 44 pixels and raise it one pixel. Move its
  four-pixel send dot right/up one pixel while keeping its circular shape.
- Use measured artwork ink: accent `#339CFF`, light response `#E5E5E5`, dark
  response `#565656` and light backdrop `#F4F4F4`.
- Preserve the real picker commands, 80x60 hit areas, spacing, focus, disabled
  behavior and selection outline. Generic assistant branding is not replaced.

The reference is the saved 2026-10-14 Codex Appearance screenshot, 1920x1032 at
96 DPI, version 26.1007.2314. A fresh passive capture of version 26.1007.9446 is
481x601 and not Appearance; it is explicitly excluded from same-state evidence.
Unidentified download/clone-looking reference actions are not guessed or wired
to unrelated operations.

#### Native and comparison evidence

Candidate: `.scratch/appearance-controls-20261015`.
App SHA-256:
`1FDB53C157E1905B2BDEF6C6CB9A196C911DC3C22AC7152DF76125BDBBBB5235`.
External evidence and Python helpers:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-appearance-actions-20261015`.
They are not repository files or release assets.

`preview-comparison.json` records exact source hashes and fixed unscaled 80x60
regions. The composite is enlarged only for inspection. Mean absolute RGB error
against that historical reference decreases for all three unscaled crops:

| Mode | Before | After | Different pixels before/after |
| --- | ---: | ---: | ---: |
| System | 3.6118 | 2.7876 | 2013 / 1586 |
| Light | 4.1519 | 3.6951 | 2351 / 1596 |
| Dark | 4.2215 | 1.8815 | 1707 / 1243 |

These metrics apply only to the recorded crops, not the whole window or a new
version sign-off. Inner-screen border/shadow, logo and remaining page typography,
catalog/actions, states and native high-DPI equivalence remain open.

Python PrintWindow validates the exact process/path/hash, rejects minimized/black
captures and records 96 DPI and ordinary permissions. Nine accepted frames cover
the baseline and candidate, System/Light/Dark, keyboard Tab/Space activation,
1920x1032, 1280x820, 526x601 and close/reopen. Candidate frames retain the corrected
miniature ink. A failed post-restart navigation frame (`08-restored-system`) shows the
workspace and is excluded; fresh geometry observation followed by one retry
opens Appearance in `09-restored-appearance-system`.

The baseline and two candidate processes exit normally with code zero and empty
stderr. Candidate data keeps all nineteen table hashes unchanged after the
mid-run snapshot, produces no artifacts, and restores the exact original System
preference bytes. Independent fresh databases differ only in schema_versions
migration timestamps; they are not falsely treated as byte-identical databases.
`audit-native.py` independently validates the accepted captures, preferences,
native pixels, process identities, normal exits and bounded crop improvements.

#### Test correction history

The initial App regression run started before its DLL was ready and is excluded.
A valid rerun fails four old-artwork cases and passes eight unchanged cases.
Initial pixel diagnostics incorrectly use offscreen RenderTargetBitmap scaling
with nested rounded clips. The final cases instead set the headless window's
render scaling and capture its actual compositor frame. Interior sample points
avoid fractional-DPI antialiasing edges; geometry has separate exact assertions.

The final compositor tests reject the unchanged baseline App in all 18 cases,
then pass all 18 with the candidate App. Focused App tests pass 69 cases. These
subsets are not additional tests beyond their complete-assembly runs. A temporary
external diagnostic executable is interrupted after producing no usable output;
it is not acceptance evidence. No production workarounds are added for it.

#### Final validation

The complete solution run passes 6,143/6,143 cases: App 4,498, visual 102,
Agent 119, Browser 76, Infrastructure 199, Windows 142, Providers 516 and Tools
491. There are no failures, skips or aborted cases. The focused 69/18 subsets
are already included in these totals. Initial diagnostic and expected-red runs
remain separate from this complete gate.

The final solution build has zero warnings/errors. Scoped whitespace formatting,
explicit source/type/line-limit/AXAML/whitespace checks and independent
TRX/binary/native-data verification pass. `verify-complete.py` confirms that the
native candidate and both UI test assemblies use the same production App bytes.
No default output, real user data, approval policy or packaging changes; nothing
is staged, committed, pushed, released or installed. The overall goal stays active.
<!-- /source: THEME_THUMBNAIL_ARTWORK_ACCEPTANCE.md -->


---

<a id="theme-thumbnail-shadow-acceptance"></a>

## THEME_THUMBNAIL_SHADOW_ACCEPTANCE.md

<!-- source: THEME_THUMBNAIL_SHADOW_ACCEPTANCE.md -->
### 主题缩略图阴影验收

#### 设计自检

1. 行为属于 App 共享预览控件和样式，不改变模块依赖方向。
2. 复用 `AppThemePreviewScene`、`AppBorder` 和 `SharedSurfaceShadow`，不增加端口、
   服务定位器、全局状态或视图文件/进程访问。
3. 仅更改绘制所有权，不改变主题命令、授权、审批、超时、取消、输出限制或持久化。
4. 新增测试每个 C# 文件只有一个类型，文件不足 80 行，方法不足 80 行。AXAML 使用
   既有封装；不增加独立阴影参数，Debug/JIT、JSON 与 Windows x64 单文件发布不变。
5. 以真实合成器像素证明修复前失败、修复后阴影可见，同时覆盖主题、缩放、内容裁剪、
   系统模式拼接和控件外部不被污染；布局测试验证阴影不参与命中。

#### 根因与修复

缩略图内层屏幕同时设置了 `ClipToBounds=True` 和外阴影。圆角内容裁剪也裁掉了
自己的阴影，背景上的采样点始终是纯色。将阴影放到同一 Grid 中的独立后置绘制层，
使其位于屏幕内容后方；屏幕仍负责裁剪内容，阴影层不可命中且不裁剪。

两层共用原来的 6-DIP 内边距、6-DIP 圆角和 `SharedSurfaceShadow`；没有通过加深
阴影来掩盖裁剪。预览仍为 80×60，内屏幕仍位于 `(7,7)`、尺寸 66×46 DIP。
模式按钮、蓝色选择框、原图案及 System 的两个 40×60 半幅保持原有所有权。

#### 定向与完整验证

- 未修复基线的 12 项新增合成器测试全部失败，浅色背景采样值实测 244。
- 修复后的像素定向 30/30、布局定向 33/33 通过，其中新增 12+6 项。覆盖 App
  明暗主题、三种模式及 100%/125%/200% 渲染缩放；后者不等同于原生 DPI 切换。
- 完整解决方案八个程序集共 6,211 项：6,210 通过、1 失败，无跳过或中止。
  App 4,553/4,554，像素 114、Agent 119、Browser 76、Infrastructure 199、
  Windows 142、Providers 516、Tools 491 全部通过；18 项新增均包含在总数中。
- 唯一失败为本次未修改的 `ModelPickerSelectionTests` 暖读取分配量断言，期望 0，
  实测 5,864 字节。同一未重建程序集的五例测试类独立复跑三次，15/15 通过。
  不放宽断言、不禁用测试，也不以复跑覆盖完整失败或宣称其根因已经修复。
- 解决方案构建 0 警告、0 错误；修改作用域的空白格式、文件规模、单类型、AXAML
  可解析性和差异空白检查通过。定向测试和复跑不重复加入完整总数。

#### 原生与历史参考对比

Python、截图、原始 TRX、日志及独立校验脚本保存在仓库外：
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-sidebar-fidelity-20261015`。
捕获验证 PID、可执行路径、普通权限、生产 App 哈希、96 DPI、尺寸和非黑帧。
候选 App SHA256 为
`9D733D4576AC3A7B8659C6212CD88E5ABCC8FC1C109C9C81A0EF3715D405DC56`；
与生产输出、两个 UI 测试依赖副本及两个原生副本一致。

12 张接受的候选帧覆盖 Light/Dark/System、鼠标切换、Shift+Tab/Space 激活，以及
1920×1032、1280×820、526×601 窗口。Light 初始/恢复偏好字节一致；新建独立数据
目录的 19 张表哈希未变、0 工具产物。基线、候选和首个干净复测进程均正常退出 0。
没有为了截图更改审批策略或调用工具。

固定历史参考为 Codex 26.1007.2314 的 1920×1032、96-DPI 外观页，参考 SHA256 为
`6717e3f33b88288cc247ade2d2b1d8122d1e1bb3c06a4901dd07a8ac8986c519`。
原尺寸 80×60 区域的比较如下，放大拼图只供观察，不用于计算：

| 模式 | RGB 平均绝对误差：前 → 后 | 不同像素数量：前 → 后 |
| --- | --- | --- |
| System | 2.787639 → 2.705278 | 1586 → 1771 |
| Light | 3.695069 → 3.507569 | 1596 → 2019 |
| Dark | 1.881458 → 1.868125 | 1243 → 1279 |

平均误差下降，但精确差异像素数量增加：恢复浅阴影改变了更多原本纯色的像素。
这不是像素一致性通过，不能只报告改善的平均误差。局部 `(30,54)` 的 Light/System
像素从 244 变为 240，Dark 从 38 变为 37，确认外阴影现在实际到达背景。

用户最大化后，新取得的 Codex 26.1007.9446 被动截图为 1920×1032、96 DPI，左栏
可见，右侧预览仍展开。它是当前版本的对话页参考，不是同状态外观页，不能替代上述
历史参考作整页验收。安装资源的只读 CSS 检查也不等同于已计算样式或最终像素证明。

#### 中断与验收边界

原计划重启复测时，检测到用户在旧独立窗口配置提供商；该目录后续表哈希比较失败。
保留配置和窗口，不覆盖、删除或打印提供商数据；此前的数据校验不冒充其最终状态。
另建干净目录取得两张接受的外观帧并正常关闭。再启动实例准备验证恢复时，界面被
切换到工作区/检查器，不能把文件名带 `appearance` 的帧当成外观证据。

`restored-appearance-light.png` 实为提供商页，`final-restored-appearance-light.png`
实为工作区/检查器；两者均排除。用户随后明确选择“稍后再截图”，因此停止截图及
桌面输入，保留旧配置实例 PID 16180 和新恢复实例 PID 25032，不虚报正常关闭或
重启外观验证完成。新目录最后一次已完成的只读表/产物校验仍单独保留。

独立脚本核对八份 TRX 的唯一用例身份、完整计数、18 项新增、原始失败、截图元数据
和历史输入哈希。程序集/源码身份清单首次在完整运行结束后生成，后续重复核验不变；
不声称它是运行前封存的身份。`full_run_all_green=false` 始终保留。

另观察到紧凑浅色窗口标题栏偶尔缺少侧栏切换图标，而深色绘制正常；未在本次修复，
不能因缩略图测试通过而关闭该原生绘制缺口。首次拖动返回过期 screenshotId 后已
重新观察，未盲目重复点击。剩余缩略图边框/品牌、当前同状态参考、原生高 DPI、
侧栏/检查器完整视觉矩阵及产品验收仍开放。没有暂存、提交、推送、发布或安装。
<!-- /source: THEME_THUMBNAIL_SHADOW_ACCEPTANCE.md -->


---

<a id="ui-component-acceptance"></a>

## UI_COMPONENT_ACCEPTANCE.md

<!-- source: UI_COMPONENT_ACCEPTANCE.md -->
### 统一 UI 组件验收

#### 2026-10-07 泛型构造封装门禁

- 复核现有共享组件和全目录 AXAML 门禁，不再建立第二套包装层。业务页继续只传
  数据、命令和状态，纯结构布局保留；字体、图标、尺寸、圆角和阴影由共享样式维护。
- 发现带 `new()` 约束的泛型工厂可接受原生控件而不报错。新增 19 项回归先得到
  8 项拒绝路径失败，修复后原生工厂实例、方法调用、方法组委托和类型别名均被
  `MOSSUI001` 拒绝。共享控件、布局、普通对象、无构造约束的集合及功能泛型仍允许。
- 修改仅属于 `App.Analyzers` 编译期边界，复用既有规则；不变更运行时控件模板、
  仓储端口、审批、授权、超时、取消、输出限制或持久化结构。
- 57 项相关边界专项通过。仓库外探针实际 App 构建拒绝 4 种 C# 原生构造/外观
  违规和 2 种 AXAML 原生/字体违规，2 种合法共享组件用法通过；随后不带探针的
  完整解决方案构建 0 警告、0 错误，限定格式验证通过。
- 最终七个测试项目共 2,537 项通过，0 失败、0 跳过，其中 App 1,928 项。
- 997 个源文件规模、单类型和方法审计 0 违规。证据和独立测试结果保存在
  `%TEMP%/MossAgent-component-generic-boundary-20261007/`，探针不进入仓库，未暂存或提交。
- 本轮没有修改运行时外观或新增截图验收；强制封装不代表所有页面、状态和 DPI
  已逐像素匹配 Codex，整个产品目标继续进行。

#### 2026-10-07 结构化回复封装与原生验证

- 继续复用现有基础和组合组件，不局限于四张示例截图；业务 C#/AXAML 的原生视觉
  控件、页面视觉继承和独立外观仍由不可降级的构建错误拒绝，结构布局保留。
- 主回复和子代理回复收敛为 `AppResponseContent`、`AppResponseTable`，代码、表格、
  图标按钮、复制和 compact 字体统一在 Controls/Styles。工具输出保持原始文本。
  针对实际 StyleKey 修正代码滚动变体，未通过页面实例属性绕过共享样式。
- 新增 27 项回归；相关专项 92 项通过。真实 App 构建另外拒绝 AXAML 回复尺寸覆盖和
  C# 替换回复内容两项探针，随后移除探针重新正常构建。
- 普通权限浅色和深色窄窗口贯通保存、父子模型、授权文件读取、审计、结构化回复、
  真实滚动和代码复制；重启恢复保持相同组件，未重放模型请求。Python 原始截图
  与当前 Codex 原图均已查看，内容不同，不宣称全页或全部 DPI 一比一一致。
- 七项目完整 2,518 项通过，构建 0 警告/0 错误，996 个源文件规模、单类型和方法
  审计 0 违规；限定格式与空白检查通过。完整证据和具体未覆盖范围见
  [SUBAGENT_EXECUTION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-execution-acceptance) 的 2026-10-07 记录。临时文件不入仓库，未暂存或提交。

#### 2026-10-07 组件门禁与真实模型请求复核

- 复核现有 `Controls` 和 `Styles`，不重复建立包装层。业务视觉组件及组合组件
  继续统一图标槽、图文布局、字段、按钮、圆角和四周浅阴影；原生元素仅保留结构
  布局及框架生成模板内部的用途，不能以封装名称掩盖页面独立外观。
- 394 项组件、样式与原生控件边界专项通过；仓库外真实 App 构建探针拒绝 C#
  原生构造、业务视觉继承、外观覆盖及 AXAML 原生按钮、字体覆盖共 5 种写法，
  2 种合法共享组件用法通过。随后去掉探针的完整解决方案构建 0 警告、0 错误。
  专项和探针证据在 `%TEMP%/MossAgent-component-review-20261007/`。
- 实际共享提供商弹窗、下拉、模型行和工作区输入贯通模型发现与流式请求。
  普通权限浅色窗口验证代理与显式直连；深色窄窗口验证重启恢复和实际编辑入口。
  多协议成功与拒绝的 12 项集成测试通过；详情及未覆盖范围见
  [PROVIDER_PROXY_ACCEPTANCE.md](PROVIDERS.md#provider-proxy-acceptance)。不把设置保存等同于运行链路验收。
- 本轮没有修改运行时视觉模板，不将历史截图或当前不同尺寸、内容的 Codex
  原图当作全界面像素一致证明。整个产品目标仍进行中。
- 最终七项目串行完整 2,491 项通过，其中 App 1,882 项；正常完整构建
  0 警告、0 错误，986 个源文件规模/单类型/方法审计 0 违规，限定格式与空白
  检查通过。最终测试和构建证据在提供商路由验收目录，未暂存、提交或推送。

#### 2026-10-07 数字输入封装与全局禁用最终复核

- 复用现有组件库，继续禁止业务 C#/AXAML 原生视觉控件、局部外观和独立样式。
  保留纯结构布局与框架内部生成控件，不将原生模板内部默认外观当作已经统一。
- 数字输入内部改用共享 `AppRepeatButton`、`AppIcon` 与统一主题；新增尺寸/Padding/
  对齐门禁，不允许经 `Button.Width` 等原生所有者属性元素绕过。普通权限原始裁片
  实测箭头墨迹 16×8→10×6，字段仍高 32 DIP，按钮宽 22 DIP，读屏名称本地化。
- 浅色 1280×820、深色 980×820 实际加减、HTTP/SOCKS5 保存、默认选择、重启恢复和
  取消未保存改动通过；仅对白名单 SQLite 元数据作只读验证，无凭据/真实请求验收。
- 仓库外探针实际 App 构建拒绝 7 种原生/业务继承/外观违规，2 种合法组件用法通过。
  临时检查器最初把更早的 appearance-token 门禁文案误认为拒绝失败；按真实错误文案
  修正预期后通过，没有修改产品门禁来迁就探针。最终不带任何探针重新构建。
- 新增 46 项回归，其中数字组件 12、数字门禁 13、代理修改 21；专项 73 项通过。
  最终七项目串行完整 2,481 项通过，0 失败、0 跳过，App 1,872 项。
  完整构建 0 警告/0 错误，985 个源文件规模/单类型/方法审计 0 违规，
  限定格式复核、相关差异和新文件空白检查通过。保留无关文件的已有尾随空白。
- 证据 `%TEMP%/MossAgent-ui-fidelity-qa-20261007-02/`，最终计数使用各项目独立的
  `final-*.trx`，不使用可能被逐项目覆盖的同名结果。截图/脚本/临时宿主不入仓库；
  未暂存、提交或推送。详见 [NUMERIC_FIELD_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#numeric-field-component-acceptance) 和
  [PROXY_EDITOR_MUTATION_ACCEPTANCE.md](PROVIDERS.md#proxy-editor-mutation-acceptance)。
- 最新 Codex 与 MossAgent 内容、尺寸及检查器状态不同，未宣称全页逐像素一致。
  全部 DPI、页面和工作流仍须持续验收，完整产品目标保持进行中。

#### 2026-10-07 全局边界与标题并发最终复核

- 复用现有组件库，不重复建立包装层。真实 App 构建重新拒绝 5 种原生视觉控件、
  业务视觉继承和独立外观覆盖，2 种合法共享组件及结构布局用法通过。
- 标题专用事务及同目标发送状态继续只使用现有共享行、输入、按钮与错误变体。
  新增 29 项回归；定向任务操作 32 项、标题与失败元数据 45 项通过。
- 七个测试项目串行完整 2,435 项通过，其中 App 1,826 项；构建 0 警告、0 错误。
  978 个 C#/AXAML 文件的规模、单类型与方法审计 0 违规，限定格式及空白检查通过。
- 本轮无新视觉模板或原生输入验收，不使用历史截图声称当前全界面逐像素一致。
  完整证据和初次探针脚本错误文案匹配的纠正见 [TASK_TITLE_PERSISTENCE_ACCEPTANCE.md](CONVERSATIONS.md#task-title-persistence-acceptance)。

#### 2026-10-07 全组件边界与会话菜单操作

- 复核现有封装，不另建一套包装层：538 项组件边界专项通过，真实 App 构建
  拒绝 5 种原生组件、页面视觉继承或外观覆盖探针，2 种合法共享用法通过。
- 会话菜单继续使用共享行、输入、按钮与错误提示变体；补齐失败重试、写入排他、
  新草稿保留、置顶元数据合并、协作取消和工作区关闭后的迟到结果保护。
- 新增 20 项回归及相邻 49 项通过。普通权限浅色 1280×820、深色 980×820 下
  实际置顶、键盘输入重命名、归档、重启恢复和 Escape 关闭通过，SQLite 消息仍保留。
- 最终串行完整 2,406 项测试及构建通过，973 个源文件规模审计无违规。
  首次并行 MCP 失败、全仓无关尾随空白、Python 主窗口截图不包含独立浮层等限制
  均保留在 [TASK_MUTATION_ACCEPTANCE.md](CONVERSATIONS.md#task-mutation-acceptance)，不把封装或测试通过当作全界面一比一完成。

#### 2026-10-07 共享导航字号与分组文字

- 通过原始截图区分字号与字体族问题，设置导航和主侧边栏操作统一使用共享
  14-DIP 字号与 18-DIP 行高；分组标题复用独立主题颜色，不引入页面专属样式。
- 设置导航“外观”墨迹从 26×12 变为参考的 28×12；主侧边栏操作从 39×12
  变为 42×12，“项目”从 23×12 变为参考的 26×12，分组文字实色匹配 #686A6C。
  默认字体、已保存字体、项目/会话行及其他文字层级没有被无声替换。
- 普通权限 1280×820 与 980×820 下实际打开设置、切换深色、导航到提供商和返回通过。
  71 项相关专项、完整 2,386 项测试、完整构建及 971 个源文件规模审计通过。
  具体原始尺寸、失败基线、临时产物排除与剩余像素差异见
  [SHARED_NAVIGATION_TYPOGRAPHY_ACCEPTANCE.md](UI_COMPONENTS.md#shared-navigation-typography-acceptance)；完整产品目标仍未完成。

#### 2026-10-07 Gemini 多轮界面复核

- 签名续接不增加页面独立样式或原生视觉控件；工具记录、失败提示和输入状态
  继续复用共享组件，内部推理与不透明签名不进入可见对话。
- 普通权限实际输入、发送、工具展开、浅色完成及深色重启/失败截图已取得，
  具体生产链路、数据库证据和物理输入范围见 [GEMINI_CONTINUATION_ACCEPTANCE.md](PROVIDERS.md#gemini-continuation-acceptance)。
- 最新完整 2,371 项测试、构建和 969 个源文件规模审计通过；上述截图不是
  Codex 全页面、所有 DPI 或逐像素一致的验收。完整目标仍保持进行中。

#### 2026-10-07 当前工作树强制边界复核

- 保留已有共享基础组件、组合组件及样式门禁，没有再创建第二套包装层。
  [UI_COMPONENTS.md](../UI_COMPONENTS.md) 增补最短使用规则：业务页传数据、命令与状态，
  图标、对齐、尺寸、字体、圆角、阴影及交互外观统一归共享实现。
- 重新向实际 App 构建注入仓库外探针，原生 C# 构造、业务专用视觉继承、
  C# 字体/宽度覆盖、AXAML 原生按钮及字体覆盖共 5 种写法全部被拒绝；
  合法共享组件及纯布局的 C#/AXAML 用法均通过。
- 移除探针输入后正常构建完整解决方案：0 警告、0 错误。
  组件专项 238 项及七个项目的完整 2,333 项测试全部通过，0 失败、0 跳过。
  960 个 C#/AXAML 文件的类型、文件与方法规模审计 0 违规，`git diff --check` 通过。
- 日志和独立 TRX 在 `%TEMP%/MossAgent-ui-wrapper-recheck-20261007/`。
  本次没有修改运行外观或重新截图；上述结果证明组件边界与回归通过，
  不代表所有页面已经与 Codex 逐像素一致。未暂存、提交或推送。

#### 2026-10-07 组件归属与继承门禁

- 复核现有基础和组合组件、共享变体及 C#/AXAML 构建规则，没有再新增一套同名包装层。
  业务页面继续只传数据、命令和状态；图标、字体、尺寸、圆角、阴影与交互外观归共享实现。
- `MOSSUI001` 补齐继承声明的归属检查：业务代码不能派生共享按钮或可选中文本
  建立页面专用视觉组件，也不能继续派生业务页形成另一套可复用视觉基类。
  页面宿主必须在 `MossAgent.App.Views`，直接继承 `AppUserControl` 或 `AppWindow`。
  共享组件内部继承、正常组件实例和纯结构布局仍允许；普通与主构造函数声明规则一致。
- 新增 8 项回归，修复前有效基线为 7 失败、1 通过；修复后相关 42 项全部通过。
  既有不可禁用/降级、原生组件、反射构造和合法功能属性规则继续通过。
- 通过仓库外 `Boundary.targets` 向实际 App 构建注入临时源码或标记门禁输入，
  验证 C# 原生构造、自定义视觉继承、独立字体/宽度，以及 AXAML 原生按钮和字体覆盖
  共 5 种违规写法确实使构建失败；2 种合法共享组件用法通过。
  最后不带临时输入重新构建整个解决方案，临时探针不进入正常应用输出或仓库。
- 完整解决方案 2,243 项测试通过，0 失败、0 跳过，其中 App 1,754 项。
  正常完整构建 0 警告、0 错误；限定文件格式化及复核通过，947 个 C#/AXAML 文件
  的类型、文件和方法规模审计 0 违规，`git diff --check` 通过，未暂存或提交。
- 证据在 `%TEMP%/MossAgent-ui-component-boundary-20261007/`：
  `inheritance-before-valid.trx`、`inheritance-after.trx`、各 `*-build.log`、
  `solution-tests.log`、`solution-build.log`、`source-audit.log` 和格式检查日志。
  全量测试以七个项目的完整 `solution-tests.log` 为准，固定名称的 `solution.trx`
  由测试运行器逐项目覆盖，仅包含最后一个项目，不作为全量结果文件。
- 本次没有修改运行布局或进行新截图验收。封装与强制门禁防止样式再次分叉，
  不代表所有页面已经与 Codex 逐像素一致；完整产品目标仍未完成。

#### 2026-10-07 工作区代理菜单与目录状态

- 输入区继续复用共享上下文条、按钮、文字和滚动组件，页面只绑定目录状态。
  加载/失败禁用选择与新发送，失效身份显示安全提示与重试；运行中的停止保持可用。
- 可用代理不再截断为前 16 项；菜单使用共享 240-DIP 有界滚动变体。
  明暗主题、窄/宽页面回归和普通权限实际刷新、默认选择及第 29 项滚轮选择通过。
- 四个失败基线、39 项新增回归、实际 SQLite/HTTP 请求、完整 2,180 项测试与
  构建/格式/规模审计证据见 [WORKSPACE_AGENT_SELECTION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#workspace-agent-selection-acceptance)。
  原始截图对比是最终实现的失效与恢复状态，不是 Codex 逐像素验收。

#### 2026-10-06 自定义代理编辑器和实际请求

- 自定义代理页面继续复用共享行、字段、按钮和状态样式，不新增独立包装层。
  操作与提示分成两个有限宽度的结构行，明暗主题回归验证全宽按钮、完整反馈和
  刷新后真实列表/模型选择。普通权限实际键盘编辑、刷新及窄窗口滚轮保存均有截图。
- 编辑修订号、稳定草稿身份、互斥、取消和安全提示复用既有设置基础设施，
  模型目录通知不再清空代理草稿；存储提交后的通知/读取失败也保持明确状态。
- 设置经工作区发送到真实本机 HTTP 请求，验证所选模型、思考强度和提示；
  运行中保存保持已捕获的请求，下一轮才使用新配置。详见
  [AGENT_CONFIGURATION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#agent-configuration-acceptance) 的失败基线、自动化与原生验收范围。
- 组件一致性、合成协议和原生正向设置输入不能替代所有页面、DPI、异常交互及
  Codex 逐像素对照；整个目标仍未完成。

#### 2026-10-06 组件封装及强制边界复核

- 现有业务界面统一使用 `MossAgent.App.Controls`，基础按钮、图标、文字、字段、
  列表、浮层和卡片，以及侧边栏、设置行、工具记录与输入区组合都有共享组件。
  图标尺寸、图文对齐、点击区域、字体、圆角、四周浅阴影及交互状态由共享样式维护，
  页面只传数据、命令、状态并选择语义变体，不重新拼装相同视觉结构。
- AXAML 的 App 构建规则禁止原生及未包装的第三方视觉控件、局部样式、视觉模板、
  本地视觉资源及实例外观覆盖；C# 的 `MOSSUI001`、`MOSSUI002` 同时禁止构造、
  继承和直接外观修改。现有负向测试覆盖别名、目标类型构造、泛型、属性元素、
  原生所有者限定属性及集合修改；诊断不能通过 pragma 或诊断配置关闭、降级。
- 只保留 Grid、StackPanel、WrapPanel、Panel、ContentPresenter 等纯结构布局，
  以及模板和绑定元数据；布局元素不能承担颜色、边框、圆角或阴影。
  组件实现可继承原生控件，框架生成的子控件仍通过共享主题统一，而非要求修改框架。
- 复核没有重复添加同名包装层。本次子代理页面继续复用共享组件，修复的是草稿、
  刷新后真实选择绑定及窄窗口反馈布局，详见 [SUBAGENT_EXECUTION_ACCEPTANCE.md](AGENTS_AUTOMATIONS.md#subagent-execution-acceptance)。
- 最新完整测试 2,109 项通过，其中 App 1,620 项，包含组件用法、允许与拒绝路径；
  解决方案构建 0 警告、0 错误。924 个 C#/AXAML 文件的类型、文件及方法规模审计通过。
  封装和门禁能防止样式重新分叉，但不等同于所有页面与 Codex 已经逐像素一致。

#### 2026-10-06 配置写入安全状态与实际输入

- 提供商、模型与发现结果共用写入执行器；失败不退出命令或清空草稿，
  同一目录的操作互斥，迟到成功不能覆盖新编辑器。
- 页面继续复用共享禁用和错误样式，不添加局部颜色、尺寸或原生视觉控件。
  4 项异常基线实际失败后修复，新增安全/竞争/页面回归和完整验证见
  [CONFIGURATION_MUTATION_ACCEPTANCE.md](ENGINEERING.md#configuration-mutation-acceptance)。
- 普通权限实际点击、系统键盘输入、保存失败与重试恢复均得到原生截图。
  这次输入针对合成模型配置，不外推为所有主工作流、DPI 或 Codex 像素一致。

#### 2026-10-06 提供商与模型共享目录状态

- 页面复用共享卡片、文字、按钮、提供商/模型行与设置弹窗，不建立第二套外观。
  加载、失败、成功空结果分别呈现；旧列表与草稿保留，失效目标不能换绑或复活。
- 配置读取完整快照、晚到结果拒绝、30 秒读取超时、生命周期取消与安全错误提示
  统一由共享目录负责。重试恢复操作；模型发现目标删除后停止保存并给出可重试状态。
- 实际 C#/AXAML 编译再次拒绝原生 Button 和实例 FontSize，合法组件与绑定通过。
  明暗主题、普通与最大化原生截图和页面命令测试见 [PROVIDER_CATALOG_ACCEPTANCE.md](PROVIDERS.md#provider-catalog-acceptance)。
- 完整产品目标仍未完成；不将共享组件门禁、Headless 命令和程序触发的原生状态
  当作全部 Windows 实体输入、DPI 与 Codex 像素一致的证明。

组件目录和业务页面使用规范见 [UI_COMPONENTS.md](../UI_COMPONENTS.md)。本记录区分构建约束、
运行界面检查和仍待完成的像素验收，不能作为整个产品完成的证明。

#### 2026-10-06 代理、MCP 与代理配置列表共用组件

- 新增 `AppSettingsItemRow`，四个列表复用有限列、省略、协议/徽标与当前目标动作，
  提供商、模型和设置项操作共享同一 24-DIP 高度规则，而非各页面单独调尺寸。
- 真实浅色截图复现代理长名称覆盖动作，前后原始像素裁片验证修复；明暗主题与
  紧凑、两行变体补充运行复核。原生组件与实例外观的编译拒绝保持强制。
- 专项及完整回归、真实构建拒绝、截图和输入验收限制见
  [SETTINGS_ITEM_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#settings-item-component-acceptance)。不将组件边界通过当作全面像素一致。

#### 2026-10-06 提供商与模型行封装和长文字修复

- 实际浅色运行截图复现长提供商名称与模型名称覆盖右侧编辑/删除操作的问题。
  新增 `AppProviderSettingsHeader`、`AppModelSettingsRow`，将两类行的组成与
  有限宽度省略封装；徽标、模型数量及操作区保持独立。
- 回归进一步发现编辑与删除按钮中心相差 1 DIP，统一共享 24-DIP 高度后通过。
  页面不设置这些尺寸，C# 和 AXAML 同时禁止替换内部内容与覆盖外观。
- 最新专项、全量回归、原始像素前后裁片，以及浅色最大化和深色普通窗口证据
  见 [PROVIDER_ROW_COMPONENT_ACCEPTANCE.md](PROVIDERS.md#provider-row-component-acceptance)。原生编辑点击未获得可验证结果，
  不把 Headless 页面命令测试当作真实鼠标/键盘验收；整体目标仍未完成。

#### 2026-10-06 失败提示统一组件与恢复验收

- 新增 `AppTaskFailureNotice`，工作区不再自行拼装警告图标与水平文字行；
  共享组件拥有有限宽度自动换行、图标对齐和卡片，外观继续使用共享样式。
  页面只传提示和可见状态，C# 与 AXAML 拒绝内容、内边距与尺寸覆盖。
- 仓库外真实构建再次验证：原生 Button 被 MOSSUI001/原生 AXAML 门禁拒绝，
  失败提示 Content 赋值被 MOSSUI002 拒绝，原生所有者限定高度被组合门禁拒绝。
  合法共享提示、结构 Grid、数据和命令通过 C# 编译及八项 AXAML 通用门禁。
- 明暗主题、320/640-DIP 组件布局验证换行、不溢出和共享 12-DIP 图标；
  普通权限原生浅色 1280×820 与深色恢复后的 1920×1032 截图验证安全提示。
  临时宿主已正常关闭，脚本、数据和图片均在仓库外。
- 安全失败类别的持久化、V7 向前迁移、专项与完整回归结果见
  [TASK_FAILURE_ACCEPTANCE.md](CONVERSATIONS.md#task-failure-acceptance)。该组件及编译约束验证不是所有页面、DPI、
  悬停状态和 Codex 逐像素一致性的证明，整体目标仍未完成。

#### 2026-10-06 共享组件样式身份防绕过

- 沿用现有基础与组合组件，不重复建立封装层。复核到业务代码可通过清空或移除
  `Classes` 中的 `app-*` 标识退回原生外观，现由 `MOSSUI002` 拒绝；索引替换、
  集合接口与局部别名、批量修改、重排、泛型控件及未知动态类名也受检查。
  业务仍可用明确的共享变体名执行 Add、Remove、Set，以及只读检查类集合。
- 新增 `ValidateSharedStyleClassIdentity`，拒绝业务 AXAML 的 `Classes.app-*`
  绑定和保留标识声明，包括原生所有者限定、属性元素及不同空白分隔。
  共享组件实现继续拥有自己的标识，选中、错误等正常状态绑定不受影响。
- 设计复核：行为由 App.Analyzers 和 App 构建规则拥有，不增加运行时 UI 依赖，
  复用既有 Controls 与 Styles，不需要新应用端口。没有业务文件、数据库、进程、
  网络或工具操作，不改变授权、审批、超时、取消、输出限制或持久化结构。
  新增类型独立成文件；全仓库 841 个 C#/AXAML 文件的类型、文件及方法体积审计无失败。
- 新增专项 42 项通过，包含允许与拒绝路径。明暗主题 Headless 布局验证切换多行
  变体后仍保留共享 32-DIP 字段和复选框主题，草稿与选中状态不丢失。
- 仓库外验收项目真实编译：原生 Button 被 MOSSUI001 拒绝，清空共享输入类集合被
  MOSSUI002 拒绝；实际导入生产项目的 AXAML 规则同样拒绝原生 Button 和保留类绑定。
  改为合法共享输入、结构布局与状态绑定后，编译及选用的八项 AXAML 门禁通过。
  检查对应错误标识和规则原因，不把其他构建失败当作门禁验证成功。
- 最新完整解决方案 1,725 项通过，其中 App 1,262 项；构建 0 警告、0 错误。
  本次修改的 C# 空白格式检查与差异空白检查通过。没有暂存、提交或推送。
  日志及临时验收源码位于仓库外
  `C:\Users\wzheng\AppData\Local\Temp\MossAgent-style-identity-qa-20261006-01`。
- 本轮只补强组件使用约束，没有更改渲染样式或新增原生窗口截图。
  Headless 状态检查和编译门禁不能证明所有页面、DPI 和交互细节已与 Codex 逐像素匹配；
  整个产品目标仍未完成。

#### 2026-10-06 组件边界与工作区外框复核

- 继续复用 `Controls` 与共享样式，不新增另一套包装层。工作区外框也采用共享变体，
  同时修复侧边栏展开状态误从目录 DataContext 取值的问题；见 [WINDOW_FRAME_ACCEPTANCE.md](UI_WORKFLOWS.md#window-frame-acceptance)。
- 仓库外 MSBuild 验收项目直接导入生产 App 项目规则：原生 Button 被原生视觉门禁拒绝，
  指定 Width、FontSize 的 AppIconButton 被实例外观门禁拒绝；合法结构布局、共享组件、
  数据和命令绑定通过 15 项通用门禁。拒绝原因核对到对应规则，不将任意构建失败计为成功。
  首次执行全部规则因临时宿主缺少设置页而失败，改为通用规则后完成上述验证；
  最终生产解决方案构建仍执行全部规则，0 警告、0 错误。
- 最新完整解决方案 1,644 项测试通过。新增外框布局与标记回归覆盖 980/1280/1920-DIP
  窗口、明暗主题、收起侧边栏及展开检查器；相关 C# 空白格式和类型/体积检查通过。
- 普通权限运行复核明暗主题、1280×820 普通窗口及 1920×1032 最大化窗口；
  实际点击收起侧栏、展开工具结果、收起检查器并恢复项目卡片，完成合成工具往返。
  Python 被动截图及不缩放的 Codex/修改前/修改后角落裁片都放在仓库外。
- 封装和门禁验收通过，不代表所有图标、页面、DPI 或悬停状态已经像素一致。
  全仓库文件/类型审计无失败，两个既有测试方法超过行数硬限制，记录在外框验收中；
  本轮相关方法没有超限，不把全量构建通过等同于所有工程约束通过。
  后续流式安全验收已修复这两处测试体积超限，最新全仓库审计无失败，
  见 PROVIDER_STREAM_LIMIT_ACCEPTANCE.md；这不代表新的 Codex 像素验收。

#### 2026-10-06 字段封装与门禁补齐

- 复用已有输入、数字与选择组件，不另建重复包装层。新增 `FieldVariants.axaml`
  集中默认 32-DIP 最小高度及内容居中；多行配置统一 72 DIP，提示词统一 150 DIP，
  输入区最大 180 DIP 移至共享样式。业务页不再单独指定这些高度或内容对齐。
- `ValidateSharedFieldSizing` 与 `MOSSUI002` 拒绝实例高度及对齐覆盖；
  结构宽度、功能数据、只读、禁用和命令仍允许。原生控件禁用门禁继续有效。
- 实际临时插入原生 Button，原生组件构建检查按预期失败；换成使用
  `TextBox.MinHeight` 属性元素的共享输入，字段检查同样失败。只指定共享变体、
  文本绑定、结构宽度和命令的合法用法通过三项构建检查。临时文件已移除。
- 新增 29 项回归（包含目录范围检查），覆盖 C#/AXAML 拒绝路径、明暗主题、
  640/980-DIP 组件布局、禁用与只读、72/150-DIP 多行变体、输入区增长上限，
  以及浏览器字段仍保留 28-DIP 紧凑高度。完整解决方案 1,623 项通过，
  构建 0 警告、0 错误；本次 C# 空白格式、类型/体积约束与差异空白检查通过。
- 普通权限生产 App 使用独立临时数据，原生输入验证打开设置、未保存的提供商
  表单、Tab 从名称移动到协议、Escape 取消，以及深色主题和窗口还原。
  使用仓库外 Python 被动截图，复核浅色/深色 1920×1032 和深色 1280×820，
  包含普通字段、禁用模型选择、多行配置和顶部对齐的提示词；未触发模型请求，
  没有保存提供商或子代理，真实应用数据不变。
- 截图目录：`C:\Users\wzheng\AppData\Local\Temp\MossAgent-field-components-qa-20261006-105654`。
  本轮原生数字字段编辑、全部 DPI、全部页面和 Codex 逐像素对照尚未覆盖；
  组件封装与构建门禁通过不等于所有视觉细节已一比一还原。

2026-10-05 侧边栏统一会话行、逐行菜单目标与截图对比的最新验收，
见 [TASK_NAVIGATION_ACCEPTANCE.md](CONVERSATIONS.md#task-navigation-acceptance)，包含新增回归、最终测试结果和原生输入限制。

模型入口、模型菜单与思考强度入口统一复用，以及目录可用性到实际请求的验收，
见 [MODEL_SELECTION_ACCEPTANCE.md](PROVIDERS.md#model-selection-acceptance)；截图与原生输入验收分别记录，不混为同一证据。

2026-10-06 输入区与消息附件统一组件、构建拒绝和运行截图验收见
[ATTACHMENT_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#attachment-component-acceptance)。发送失败、草稿保留和停止持久化验收见
[MESSAGE_SEND_ACCEPTANCE.md](CONVERSATIONS.md#message-send-acceptance)；整体像素与物理输入验收仍未完成。

#### 实现边界

- 视觉组件由 `MossAgent.App.Controls` 拥有；共享样式在 `Styles/`，
  业务页面只传数据、命令、状态和结构布局，不自行指定颜色、字体、图标尺寸或阴影。
- 保留 Grid、StackPanel 等结构元素；原生和第三方视觉控件只能在共享组件内部实现。
- 图标按钮、图标文字操作、设置导航、工具行和设置弹窗封装自己的内部组成。
  项目行、项目菜单、项目选择器和输入区选择器复用同一套图标文字布局。
- 长标签在有限宽度列内左对齐并省略，徽标和尾图标使用独立列；
  无前图标时不预留空槽，选中标记不被长标签挤出。
- 修改不改变模块依赖、工具授权和审批管线，不涉及持久化结构变更。

#### 构建约束

- C# 的 `MOSSUI001` 禁止业务代码构造或继承原生、第三方视觉控件。
- C# 的 `MOSSUI002` 禁止业务代码覆盖共享外观或创建页面局部样式。
- 弹出层呈现器主题和类名也属于组件内部外观；AXAML 属性、属性元素和 C# 赋值、
  绑定、集合修改不能覆盖它们，内容、位置、关闭行为及只读检查仍被允许。
- AXAML 构建规则禁止原生视觉元素、非共享视觉命名空间、页面局部样式、
  外观属性覆盖和替换封装组件的内部内容。
- 2026-10-04 临时在 App 根目录加入原生 Button，实际运行
  `ValidateSharedVisualControls`，构建按预期失败。
- 将同一临时文件换成指定 Width、FontSize 的 AppIconButton，实际运行
  `ValidateSharedComponentAppearance`，构建按预期失败。
- 换成结构 Grid、共享 AppActionButton 和绑定数据的 AppInput，七项相关
  构建检查通过；临时验证文件随后移除，不保留在仓库。
- 自动测试覆盖别名、泛型约束、属性元素、Avalonia 属性赋值、共享组合布局、
  卡片阴影、功能绑定及允许使用的结构元素。
- 追加弹出层回归测试时，原有规则漏掉的 12 种违规写法均被实际复现；补强后，
  包含原生属性所有者限定写法、集合别名和 `Set` 的 17 项弹出层测试全部通过。
- 临时加入覆盖 `FlyoutPresenterClasses` 的 AXAML，实际执行
  `ValidateSharedVisualTemplates` 按预期失败；换成仅指定内容、位置和关闭行为的
  共享弹出层后，模板、组件、外观和原生组件四项检查通过。临时文件已移除。

```powershell
dotnet test MossAgent.slnx --no-restore --disable-build-servers -m:1
dotnet build MossAgent.slnx --no-restore --disable-build-servers -m:1
```

2026-10-05 UI 组件基线完整复核 900 项通过（App 609 项），解决方案构建 0 警告、0 错误，
本次修改的 C# 空白格式检查和 `git diff --check` 通过。
构建拒绝验证是主动插入违规用法的预期失败，
不属于最终源码的构建失败。

#### 实际界面检查

2026-10-04 使用普通权限应用和独立临时数据目录，未修改生产配置、
真实项目文件或提供商凭据。截图脚本和图片均放在仓库外。

- 检查浅色和深色主题，1280×820 普通窗口及 1920×1032 最大化窗口。
- 使用长中文项目名称检查侧边栏、输入区、上下文摘要和项目选择器；
  标签保持省略，箭头和选中勾保持可见，按钮具有实际文字可访问名称。
- 通过真实项目编辑和保存操作修改验收项目名称，重启后名称仍保留。
- 检查设置导航的图标文字对齐，项目选择菜单打开及 Escape 关闭。
- 最后一次构建后复核明暗主题的主页图标：选中时使用共享实心圆角图标，
  不是仅在源码中修改但运行界面仍显示轮廓图标。
- 使用仓库外 Python 脚本的 `--passive --include-popups` 保存最终界面，
  保留原始像素尺寸，不拉伸截图；本轮没有黑屏截图。

#### 2026-10-05 图标组件与边界复核

- 修复共享图标的实际绘制偏移：原生 Path 在方形布局槽内没有居中非方形几何，
  单纯设置控件垂直居中不足以解决箭头、文件夹和最小化图标的高低差异。
  现在由 AppIcon 统一缩放、居中和保持描边粗细，空几何及无效视口不产生无效变换。
- 最小化采用水平描边，不再用加粗实心矩形掩盖零高度几何的问题。
  窗口操作与历史导航尺寸集中在 WindowVariants，不在业务页面设置尺寸或描边。
- 使用仓库外 Python 被动截图，最大化有效窗口均为 1920×1032；
  参考只裁去屏幕外 8 像素窗口边缘，没有缩放截图。
  标题栏同一坐标区域以背景差值大于 45 的掩码测量，修正后的边界如下：

| 图标 | Codex | MossAgent | 同坐标边界 |
| --- | --- | --- | --- |
| 返回 | 10×10 | 10×10 | 相同 |
| 侧边栏 | 14×12 | 14×12 | 相同 |
| 最小化 | 10×1 | 10×1 | 相同 |
| 还原 | 10×10 | 10×10 | 相同 |
| 关闭 | 10×10 | 10×10 | 相同 |

- 参考的前进按钮处于更浅的禁用状态，未落入该阈值，不将其当作已匹配证据。
  掩码边界一致只证明尺寸与位置，不证明颜色、抗锯齿或整个界面逐像素一致。
- 本轮运行复核外观设置的明暗主题、1280×820 普通窗口和最大化窗口，
  检查返回应用、设置图标、标题栏还原、长项目名选择浮层及 Escape 关闭。
  截图包括 `titlebar-final-light-comparison.png`、`settings-final-light.png`、
  `settings-final-dark.png`、`settings-normal-light.png` 和 `project-picker-normal-light.png`，
  保存于独立临时验收目录，脚本和图片均不加入仓库。
- 临时再次插入原生 Button 和覆盖 Width、FontSize 的 AppIconButton：
  原生组件检查和实例外观检查分别按预期拒绝；换成结构 Grid、共享操作按钮及
  数据绑定输入框后，十项 ValidateShared 检查通过。临时文件随后移除。
- 新增确定性图标布局测试覆盖非方形、负原点、水平线、垂直线、空几何、
  零尺寸、负尺寸和非有限尺寸；补充共享标题栏变体和尺寸更新回归测试。
- 使用隔离数据副本，仅调整外观和打开界面，不调用已保存的提供商、工具、
  浏览器或终端，不修改生产数据和审批策略。验收进程结束后再完成全量构建。

#### 2026-10-05 共享绘制外观门禁补强

- 现有组件回归先通过 292 项；新增负向测试实际复现了 22 种绘制外观覆盖。
  页面不能再用渲染位移、缩放、变换原点、描边端点、连接或虚线破坏共享外观。
- AXAML 普通属性、原生所有者限定属性和属性元素均受检查；C# 直接赋值、
  Avalonia 属性赋值及绑定、虚线集合修改也会报 `MOSSUI002`。
  补齐带括号或空值抑制符的集合别名解析，接口转换和索引替换不能绕过检查。
- 共享组件实现仍可定义绘制外观；标题栏的共享像素偏移未改动。
- 临时加入原生 Button 和覆盖绘制外观的 AppIcon，分别实际执行对应构建检查，
  两者均按预期拒绝；换成共享组件后全部 13 项门禁通过。临时文件已移除。
- 本轮 25 项新增回归及全部 634 项 App 测试通过；解决方案构建 0 警告、0 错误。
  本轮没有重新运行其他测试项目，也没有重新截图，不将构建门禁等同于像素验收。

#### 2026-10-05 组合组件内部集合约束

- 复核现有共享组件与全部业务 AXAML，不重复创建另一套控件。
  原生视觉控件及页面独立外观继续由构建规则和 C# 分析器禁止，
  Grid、StackPanel 等元素仅承担结构布局。
- 新增负向测试实际复现 16 种内部集合覆盖：业务 C# 可通过子控件、行列定义集合
  改坏 AppIconLabel，AXAML 也可用 Children 属性元素绕过原有组合检查。
- 补强后，集合添加、删除、清空和索引替换均被拒绝，
  集合别名、接口转换和转换成原生 Grid 的稳定别名同样受检查。
  组件内部实现、只读检查、数据与状态绑定及真正的页面布局集合仍被允许。
- 临时加入 Panel.Children 覆盖的共享组件 AXAML，实际执行
  ValidateSharedComposedControls 按预期失败；改为共享组件和结构布局后，
  全部 13 项构建门禁通过。临时验证文件已移除。
- 新增 20 项回归通过；完整解决方案 983 项测试通过，其中 App 662 项。
  解决方案构建 0 警告、0 错误，本轮 C# 空白格式检查和 git diff --check 通过。
- 本轮只修改 App 的开发约束，不修改运行样式、模块依赖、持久化或工具审批。
  没有重新截图，不将组件封装和门禁验收等同于 Codex 逐像素还原。

#### 2026-10-05 当前工作区自动化复核

- 复用现有共享组件与门禁，没有重复创建控件或改动无关功能。
  24 个业务 AXAML 页面受项目构建检查覆盖；原生视觉控件只允许在共享组件内部实现。
- 组件、共享样式及原生控件边界的定向测试 302 项通过。
- 当前完整解决方案 1030 项测试通过，其中 App 679 项；解决方案构建 0 警告、0 错误。
  `git diff --check` 通过。
- 本次复核未修改运行样式，也未重新截图；自动化通过不代表所有页面已与 Codex 逐像素一致。

#### 2026-10-05 页面切换与键盘焦点

- 实际设置页复测发现，遮罩显示后焦点仍停在工作区的设置按钮上，
  Tab 会进入被遮挡的 MossAgent 项目按钮，并在设置页上画出错误的焦点框。
- 新增共享 `AppPageSwitcher`，主窗口只传两页内容和当前页状态；
  隐藏整页而不是仅覆盖像素，保留页面实例、草稿和编辑值。
  进入设置页聚焦可用字段，返回时恢复仍然有效的原控件；设置页内 Tab 循环。
  页面移除和快速切换会使过期的焦点回调失效。
- AXAML 构建门禁和 C# `MOSSUI002` 禁止覆盖该组件拥有的 Content。
  临时加入违规 AXAML，实际执行 ValidateSharedPageSwitchers 按预期报错，
  临时文件随后移除。正常语义属性和真实业务页面均通过检查。
- 普通权限隔离预览验证搜索设置初始焦点；反向 Tab 从搜索到返回应用，
  再循环到设置页最后的代码字体字段，不进入标题栏或隐藏工作区。
  可访问树不再暴露隐藏的输入框，返回对话后恢复设置按钮焦点并保留未发送草稿。
- Python 被动截图保留原始像素：最大化 1920×1032，以及普通窗口
  1280×820 的浅色、深色设置页。截图在仓库外验收目录，
  包括 fixed-settings-focus.png、fixed-draft-return.png、
  fixed-settings-normal-light.png 和 fixed-settings-normal-dark.png。
- 新增 14 项组件和边界回归；定向测试 29 项通过。
  完整解决方案 1046 项测试通过，其中 App 695 项。
  解决方案构建 0 警告、0 错误，相关 C# 空白格式检查与 git diff --check 通过；
  本轮 4 个 C# 文件通过单类型、文件规模和方法规模检查。
  本轮修改不涉及数据库迁移、付费模型调用、真实项目操作或审批策略调整。
- 加载期设置图标的物理坐标点击在部分复测中成功，但仍有未切换的观察；
  元素定位打开和加载完成后的坐标点击可用。本次焦点修复不代表鼠标偶发问题已定位。

#### 2026-10-05 标签截断与组件边界收口

- 复核已有 `MossAgent.App.Controls` 封装和构建门禁，不重复实现另一套控件。
  原生视觉控件不能在业务 AXAML 和 C# 中直接构造或继承；布局元素仍可使用。
- 新增负向测试复现 8 种 C# 标签宽度覆盖，随后补齐 `MOSSUI002` 和
  `ValidateSharedLabelSizing`。直接属性、属性元素、所有者限定写法、绑定、
  属性索引器和稳定属性别名均不能覆盖共享 `LabelMaxWidth`。
  共享组件实现、数据与命令状态、语义类及真正的结构布局宽度仍然允许。
- 输入区分支和项目摘要的 3 处实例宽度移入共享样式，保留原有 200 和 260 DIP
  截断上限。已有项目、代理选择器的共享宽度变体继续有效。
  更新旧组件回归，使其检查共享样式而不是要求页面内联尺寸。
- 临时 AXAML 中的原生 Button 和 AppIconLabel 宽度覆盖，实际运行各自构建门禁
  均按预期拒绝；临时文件已移除。新增 18 项边界回归通过。
- 普通权限隔离预览使用合成项目数据、禁用 MCP 自动连接，不调用付费模型。
  验证长项目名在侧边栏、输入区选择器和项目摘要内省略，不覆盖相邻图标。
  项目摘要保持完整可访问文本；外观切换及返回工作区可用。
- Python 被动截图保存原始像素：浅色最大化 1920×1032，以及浅色、深色
  普通窗口 1280×820。仓库外 `%TEMP%/MossAgent-component-qa-ervjddju` 保存
  shared-label-light.png、shared-label-normal-light.png、shared-label-normal-dark.png
  和 shared-label-popup-dark.png；测试脚本没有加入仓库。预览已正常关闭。
- 完整解决方案 1064 项测试通过，其中 App 713 项；解决方案构建 0 警告、0 错误。
  相关 C# 空白格式检查与 git diff --check 通过；本轮 3 个 C# 文件满足单类型、
  文件规模和方法规模硬限制，3 个修改的 AXAML 均不超过 500 行。
- 本轮补强组件封装和开发约束，不表示全页面、全 DPI 或 Codex 逐像素还原完成，
  也不表示此前观察到的加载期偶发鼠标输入问题已经定位。

#### 2026-10-05 所有者限定属性边界

- 在已有共享组件上补强门禁，不新增重复组件、不改动业务运行链路或渲染样式。
  普通属性、所有者限定属性和属性元素使用相同尺寸与组成检查，修复
  Button.Width、Border.Padding、Grid.ColumnSpacing 和 Button.Content 等绕过方式。
  复选、切换按钮尺寸以及图标、颜色预览的最小和最大尺寸也由共享变体维护。
- 新增 30 项回归，其中 28 项违规用法在修复前均失败、修复后通过。
  正常按钮内容、输入值、弹出菜单、语义类和结构布局不受影响；全部业务 AXAML
  通过扫描。相关定向测试 86 项通过。
- 在 App 根目录临时加入 AXAML，分别执行 ValidateSharedVisualControls、
  ValidateSharedComponentAppearance 和 ValidateSharedComposedControls；三个真实
  MSBuild 门禁均按预期拒绝原生 Button、限定宽度和限定内容覆盖。探针文件已移除。
- 完整解决方案 1094 项测试通过，其中 App 743 项；构建 0 警告、0 错误。
  新测试文件空白格式检查和 git diff --check 通过；文件 109 行、单类型、最长方法 26 行。
- 本轮没有修改运行样式或重新截图，不将约束检查等同于 Codex 逐像素还原。

#### 2026-10-05 组件阴影统一与全局门禁复核

- 复核已有的共享组件，不另造一套封装。按钮、图标、图文组合、字段、卡片、
  弹窗和浏览器等使用 `MossAgent.App.Controls`；原生与第三方视觉控件的
  AXAML、C# 构造及继承仍受构建门禁和分析器约束，结构布局元素保留。
- 找到仍独立定义的下拉浮层阴影：原先透明度为 24/255、模糊半径为 12，
  与卡片的 10/255、14 不一致。现在二者引用 `SharedSurfaceShadow`，
  保持零水平与垂直偏移、零扩张和四周浅阴影；设置弹窗和操作浮层继承卡片样式。
- 新增 9 项回归验证统一资源、真实样式引用、全局共享样式扫描和违规写法。
  修复前新增测试均失败；修复后包含既有组件边界的定向测试 385 项通过。
- 临时探针实际验证原生 Button、AppIconButton 实例尺寸覆盖和独立阴影
  分别被对应 MSBuild 门禁拒绝；改用共享组件后全部 16 项 AXAML 门禁通过。
  探针已移除。非视觉 `BoxShadows` 资源允许使用，不扩大原生视觉控件白名单。
- 普通权限独立数据预览实际打开外观设置，检查浅色、深色卡片和字体下拉浮层，
  Escape 关闭下拉有效，字体选择未改变。首次设置入口点击没有打开页面，
  刷新定位后打开成功；本轮不声称修复此前的偶发输入问题。
- 仓库外 Python 脚本被动截图保留 1920×1032 原始像素，图片位于
  `%TEMP%/MossAgent-component-qa-u8xo29un`，包括 `shared-dropdown-light.png`、
  `shared-settings-dark.png` 和 `shared-dropdown-dark.png`。脚本与截图不加入仓库。
- 完整解决方案 1137 项测试通过，其中 App 786 项；相关 C# 空白格式检查
  与 `git diff --check` 通过。预览正常关闭后，最终解决方案构建 0 警告、0 错误。
- 修改仅涉及 App 共享外观、开发约束、测试与文档，不涉及模型调用、工具审批、
  数据库迁移、生产数据或无关功能。未穷尽全页面、全 DPI 和所有交互状态，
  不能将封装门禁或本轮截图当作 Codex 逐像素还原完成。

#### 2026-10-05 组件属性重置边界

- 沿用已有的 `MossAgent.App.Controls` 组件体系，不新增重复封装。
  按钮、图标与文字组合、字段、列表、卡片、弹窗、页面和窗口宿主使用共享组件，
  原生视觉组件在业务 AXAML、C# 构造及继承中均受强制检查。
  Grid 等结构布局仍允许使用，尺寸、外观与组成的变体继续集中在共享样式中。
- 发现 C# 的 `ClearValue` 未进入外观检查，业务代码可清空图标按钮自带内容。
  现在该调用复用 `MOSSUI002` 和已有属性策略，拒绝清空组件拥有的内容、图标尺寸、
  标签截断、阴影和浮层主题，覆盖原生基类转换、属性别名与泛型约束。
  普通按钮内容、输入数据、可用状态和结构布局尺寸仍可重置，组件实现内部也不受影响。
- 所有权自检：变更属于 App.Analyzers 的编译期规则，依赖方向不变；复用现有策略，
  没有新增运行服务、文件或网络访问、审批、超时、取消或输出行为。
  分析器 132 行，新测试文件 90 行，均为一个类型，方法没有超过硬限制。
- 新增 18 项回归：修复前 12 项拒绝用法失败、6 项允许用法通过；修复后全部通过。
  包含既有组件约束的定向检查 93 项通过，App 全部 804 项测试通过。
- 在 App 中临时加入 C# 探针，真实构建分别以 `MOSSUI001` 拒绝原生 Button，
  以 `MOSSUI002` 拒绝清空图标按钮内容；探针已移除。
- 解决方案构建 0 警告、0 错误。首次并行全量检查有两项无关进程测试失败：
  MCP 同步上下文用例没有发现工具，以及 Shell 超时用例读取 PID 文件发生共享冲突。
  相关 3 项单独复测通过，随后串行全量 1155 项测试全部通过；未修改这些无关用例，
  不将复测成功当作上述波动已被修复。相关空白格式检查与 `git diff --check` 通过。
- 本轮只补强开发约束，没有修改渲染样式或重新截图，不表示 Codex 逐像素还原完成。
  未提交、推送或暂存改动，保留已有工作区修改。

#### 2026-10-05 全页面边界与共享宿主复核

- 复核现有 24 个业务 AXAML 页面和共享组件目录，沿用已完成的统一封装，
  不重复另建一套控件。App 构建继续拒绝原生视觉组件、第三方直接控件、
  页面独立样式、实例外观覆盖以及组件拥有内容的修改或重置。
  Grid 等纯结构布局和功能绑定仍允许，外观变化通过共享样式变体实现。
- 浏览器原生呈现也由共享宿主维护，隐藏检查器和后台任务不能抢占当前页面；
  审批状态订阅的六项新增回归覆盖批准、拒绝、取消、队列、初始状态及释放。
- 当前串行全量 1,172 项测试通过，其中 App 820 项，解决方案构建
  0 警告、0 错误。没有重新截图全部页面，不声明视觉逐像素还原完成。

#### 2026-10-05 紧凑工具栏与选中标签封装

- 沿用现有共享组件与门禁，浏览器页改用 `AppBrowserToolbar`，地址输入与操作
  分为两行，图标按钮复用 28×28 变体。页面只传递地址与命令，不能替换组件内容。
  会话标签使用 `AppSelectionScrollViewer`，布局完成后有界滚动到选中项，
  卸载时解除订阅，不在每次布局中抢回用户手动滚动位置。
- 实际构建探针分别以 `MOSSUI001` 拒绝原生 Button，以 `MOSSUI002` 拒绝
  图标按钮实例宽度覆盖和清空工具栏内容；探针已移除。AXAML 门禁和所有页面扫描
  随最终 App 构建通过，Grid 等结构布局和功能绑定仍允许。
- 新增组件用法、240/277/345/600 宽度布局、滚动边界和原生事件归属回归。
  组合执行最初有五项绑定断言失败：Avalonia 12.1.2 的首个 UI 调度线程与
  xUnit 工作线程不同，未运行其消息循环。工具栏测试改用隔离 Headless 会话，
  保留双向绑定、Enter 和命令断言；实际附加窗口后的禁用、启用状态也通过。
  业务 DataContext 不能劫持内部绑定，清空命令后 Enter 不执行旧命令。
- 普通权限合成预览重新执行 14 项真实浏览器工具，全部成功；地址栏 Enter、
  返回、前进均通过实际页面检查，状态更新为“页面加载完成”。修复的根因是
  NativeWebView 转发后台实现作为事件 sender，而非视图本身；事件订阅现在
  捕获拥有者并在卸载时释放，旧视图不能更新当前面板。
- 仓库外 Python 脚本被动保存原始 1920×1032 和 1280×820 像素截图，目录为
  `%TEMP%/MossAgent-native-browser-qa-aun12f7c`。最终截图包含
  `toolbar-final-back-dark.png`、`toolbar-final-forward-dark.png`、
  `toolbar-final-normal-dark.png`、`selected-third-final-dark.png` 和
  `toolbar-final-empty-light.png`。第三个选中标签在普通窗口内完全可见，
  地址与操作没有重叠。浅色最终截图为会话结束后的空状态，不是活跃网页验收。
  普通窗口截图右上角有小范围遮挡，不据此证明标题栏全部细节。
- 合成服务的 300 秒检查等待结束后会话正常关闭，没有重新启动服务来绕过等待。
  预览正常关闭，合成服务已停止，对应 WebView2 进程均已结束。
  脚本、截图和合成数据不加入仓库，没有触碰真实账号或用户数据。
- 最终串行全量 1,203 项测试通过，其中 App 851 项；解决方案构建
  0 警告、0 错误。相关 C# 空白格式与本次范围的 diff 空白检查通过，
  单类型及文件、方法规模检查通过。全仓严格空白扫描另发现无关
  `SearchTextTool.cs` 的三处行末空白，本轮不修改该文件。
- 所有权属于 App 的共享呈现和测试宿主，依赖方向不变；复用现有会话与命令，
  不改变授权、审批、超时、取消、输出限制或持久化策略。没有暂存、提交或推送。

#### 2026-10-05 工具详情换行与历史恢复

- 真实窗口复现详情横向滚动导致的裁切：正文虽设置 Wrap，ScrollViewer 的 Auto
  横向模式仍提供无限测量宽度，长文件路径和子代理回复撑宽整块详情。
- 改为共享 tool-details-scroll：最大高度 260，纵向按需滚动，横向禁用。
  详情缩进及内边距也移到共享表面样式；代码块继续独立横向滚动。
  不改变工具内容、持久化、审批、授权或输出预算。
- 新增九项回归，其中八项在正确配置共享资源和字体的宿主中先复现 Auto
  与 Disabled 的预期差异。修复后覆盖 280/740 宽度、明暗主题、普通输出及
  子代理正文，验证长无空格文本换行、有限宽度、纵向范围和原文保留。
  另验证页面使用共享变体且代码块滚动未改变；相关定向合计 21 项通过。
- 布局测试使用真实 App 样式、Inter 字体和独立 Headless 消息循环，
  不调用生产 App 的服务装配或数据库初始化。早期宿主的资源重复挂载及字体
  注册问题仅是测试夹具问题，修正后没有删减布局和滚动断言。
- 使用原先完成链路的独立数据副本重新打开普通权限应用，真实恢复七项子代理
  对话、成功工具结果和父任务汇总，没有重跑模型、工具或开启本机模型服务。
  明暗主题、普通和最大化窗口检查长路径换行、详情纵向滚动及折叠后重新展开。
  原先种子消息角色的旧呈现缓存也不再存在。
- 仓库外 Python 被动截图保留原始像素：修复前 1920×1020，修复后最大化
  1920×1032，普通窗口 1280×820。窗口可用高度不同，没有缩放来制造匹配。
  `%TEMP%/MossAgent-tool-detail-qa-3pcuxkrj` 保存 tool-details-before-light.png、
  tool-details-fixed-light.png、tool-details-fixed-dark.png、
  tool-details-fixed-normal-light.png 和 tool-details-fixed-normal-dark.png。
  初次普通浅色截图的右上角有小范围外部窗口遮挡，不用于证明标题栏细节。
- 同时被动保存当前 Codex 参考，参考内容和窗口边缘不同，只用于视觉观察，
  不作为逐像素差分或子代理内容相同的证据。没有操作 Codex 的原生界面。
- 完整解决方案 1,222 项测试通过，其中 App 870 项；构建 0 警告、0 错误。
  本轮两个 C# 文件通过格式、单类型、文件及方法规模检查，两个修改的 AXAML
  均不超过 500 行。本轮范围的 diff 及新文件行末空白检查通过，没有提交或推送。

#### 2026-10-05 本地组件资源覆盖门禁

- 复用既有 `Controls` 封装，不重复新增按钮、输入框、卡片等组件。业务页继续只传递
  数据、命令和结构布局；图标、文字、按钮尺寸及卡片阴影由共享样式维护。
- 补上通过控件 `Resources` 改 Fluent 外观资源的绕过路径。业务 C# 不能写入、替换、
  清空或删除本地资源，也不能通过集合初始化、扩展 `TryAdd`、别名、合并字典或
  主题字典绕过；只读查询、组件内部实现和应用级主题、字体服务仍允许。
  业务 AXAML 不能声明本地画刷、字体、阴影、图标几何或导入资源，不能绑定替换
  资源字典；转换器和使用共享组件的 `DataTemplate` 仍允许。
- 新增 40 项回归。原有分析器未拒绝的 12 种 C# 资源写入先复现失败，扩展检查还
  发现集合初始化和 `TryAdd` 绕过；修复后全部通过。相关组件、外观、图标及视觉
  规则共 399 项通过，包含这 40 项。全业务 AXAML 扫描也通过新资源门禁。
- 临时加入真实 App 构建探针，分别观察到 `MOSSUI001` 拒绝原生 Button、
  `MOSSUI002` 拒绝图标按钮宽度覆盖和控件资源写入；另一个 AXAML 探针被
  `ValidateSharedComponentResources` 拒绝本地画刷。两份探针均已移除。
- 完整解决方案 1,308 项测试通过，其中 App 956 项；构建 0 警告、0 错误。
  本轮 C# 空白格式、范围内 diff 检查、单类型及文件、方法规模检查通过。
  分析器文件最长 140 行，测试 137 行；未暂存、提交或推送，也未改动用户数据。
- 本轮所有权是 App 编译约束，依赖方向不变；复用既有分析器和构建检查，无新增
  运行端口，不改变授权、审批、取消、超时、输出限制或持久化。没有修改渲染样式
  或重新截图，不能把封装和门禁通过当作 Codex 逐像素还原完成。

#### 2026-10-05 编辑菜单组合封装

- 复用既有基础组件，新增 `AppEditMenu` 收拢标题栏编辑菜单的内容、浮层、命令
  和生命周期。页面不再维护六套点击分支，也不再用空格拼接快捷键。
  行高 32、正文 13、快捷键 12 和禁用透明度由共享操作按钮变体维护，
  普通操作徽标仍为 10；浮层继续使用统一圆角和 `SharedSurfaceShadow`。
- 菜单依据真实输入或可选文本的状态启用操作。隐藏、禁用、移出窗口或关闭
  的目标不可执行；只读禁止修改，密码禁止复制和剪切，直接调用命令也不能绕过。
  卸载解除订阅、关闭浮层并废弃旧命令；重新挂载可重新跟踪同一窗口的编辑目标。
- 原生预览发现 Windows 的 `ClearSelectionOnLostFocus=True` 会在菜单获得焦点
  时清空选区，导致已选中文字仍不能复制。修复为组件在菜单焦点转移时恢复选区，
  不改变输入原有失焦策略；正文被外部替换后不把旧选区恢复到新正文。
  正向、反向选区及替换后的边界均有回归覆盖。
- 新增 40 项测试，相关菜单、操作按钮与浮层定向合计 47 项通过。
  真实 App 构建探针确认原生 Button 被 `MOSSUI001` 拒绝，菜单内容和浮层
  替换被 `MOSSUI002` 拒绝；AXAML 的原生属性所有者限定替换也被构建目标拒绝。
  两份临时探针已移除，合法共享组件与结构布局继续允许。
- 普通权限真实 App/MainWindow 预览，独立临时数据，Python 仅被动截图。
  浅色最大化 1920×1032 验证剪切、复制启用；深色普通窗口 980×700 验证
  只读仍可复制、不能剪切或粘贴，以及禁用状态的视觉区分。
  `%TEMP%/MossAgent-edit-menu-qa-1791207347255` 保存修复前后图片与日志，
  最终图片为 `edit-menu-light-fixed.png`、`edit-menu-dark-fixed.png`。
  预览宿主在仓库外设置合成输入、焦点和菜单状态，不是原生鼠标、键盘验收。
- 完整解决方案 1,377 项测试通过，其中 App 1,018 项；构建 0 警告、0 错误。
  本轮格式、单类型、文件及方法规模检查通过；没有暂存、提交或推送。

#### 2026-10-05 反射创建门禁与退出弹窗复核

- 原生视觉控件反射创建的 4 项负向测试先复现缺口，再由 `MOSSUI001` 拒绝。
  覆盖泛型 `Activator.CreateInstance`、`typeof`、只读局部类型别名和非公开构造参数；
  共享控件、结构布局与非视觉对象的 4 项正向测试通过。
- 临时真实 App 构建探针确认反射创建 Button 被 `MOSSUI001` 编译错误拒绝；
  探针已移除。不是只在测试中检查规则，也不是仅改组件名字。
- 退出等待与失败提示复用 `AppSettingsDialog`、`AppText` 和 `AppButton`；
  不添加局部尺寸、颜色或阴影。等待时没有关闭 X、Escape 关闭命令或强制退出按钮。
- 本轮定向 24 项通过；完整解决方案 1,430 项通过，其中 App 1,071 项。
  构建 0 警告、0 错误；本轮涉及文件的格式检查通过。
  Roslyn 审计 781 个 C#/AXAML 文件，未发现文件超过 500 行或多类型 C# 文件。
  仍有 3 个预先存在的超长方法，不在本轮改动范围，不声称仓库方法规模全部合格。
- 普通权限真实 App/MainWindow 配合仓库外合成数据、Python 被动截图，
  保存浅色最大化 1920×1032 和深色普通窗口 1280×820 主界面、
  浅色设置页及明暗退出弹窗。没有拉伸、点击、调整其他应用或读取真实凭据。
  图片与宿主位于 `%TEMP%/MossAgent-component-shutdown-qa-20261005-01`，
  各预览均通过正常窗口关闭完成退出，日志确认未提升权限。
- 设置页截图出现鼠标原位置触发的主题提示，不当作无提示状态验收。
  Codex 对照使用此前保存的 `codex-current-reference.png`，本轮未取得新的目标窗口。
  弹窗状态由验收宿主设置，不是原生鼠标、键盘交互验收。
  没有暂存、提交、推送，也没有将截图脚本加入仓库。

#### 2026-10-06 当前组件约束与工作区复核

- 当前业务 AXAML 和 C# 已通过全量组件边界检查：基础视觉控件使用
  `MossAgent.App.Controls`，图标文字操作、附件、工具行、会话行、浏览器操作区
  与弹窗复用各自共享组合组件；没有为了本次复核重复建立第二套封装。
- `MOSSUI001` 拒绝业务代码构造或继承原生及第三方视觉控件，`MOSSUI002`
  拒绝实例外观覆盖；两者是不可配置的错误。AXAML 构建目标同时检查原生元素、
  非共享命名空间、局部样式、视觉资源和封装内容覆盖。Grid、StackPanel 等
  结构布局及数据、命令、状态绑定仍允许，原生实现只在共享组件内使用。
- 全量复测 1,508 项通过，其中 App 1,128 项；解决方案构建 0 警告、0 错误。
  803 个 C#/AXAML 文件审计未发现超过 500 行的文件或多类型 C# 文件；
  仍有三个预先存在的超长方法，未作为本次组件封装范围修改。
- 附件的专项验收见 [ATTACHMENT_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#attachment-component-acceptance)；消息和共享工具
  详情在普通权限、明暗主题、普通及最大化窗口中的运行复核见
  [HISTORY_CONTEXT_ACCEPTANCE.md](CONVERSATIONS.md#history-context-acceptance)。四张最终历史截图非黑屏，宿主正常退出，
  脚本只被动捕获，不通过缩放图片掩盖布局差异。
- 这些结果证明组件复用和开发门禁，而非所有页面视觉细节已匹配 Codex。
  本轮没有新增渲染样式或取得新的 Codex 对照窗口；整个产品目标保持未完成。

#### 2026-10-06 组件约束最终复测

- 沿用现有共享基础组件、组合组件和不可降级的编译门禁，没有重复建立包装层。
  原生及第三方视觉控件、独立外观覆盖继续被拒绝，结构布局和功能绑定继续允许。
- 组件、编译门禁、字段与对话标题布局专项 108 项通过，包含明暗主题及不同宽度。
  新增清理重试回归：Headless 回调返回后仍能释放被占用的临时文件，不需要已经
  停止的 UI 调度循环；保留原有全部布局和真实命令断言。
- 最新完整解决方案 1,637 项通过，其中 App 1,212 项；构建 0 警告、0 错误。
  本轮修改文件的空白格式检查通过。没有暂存、提交或推送。
- 本轮没有新增渲染样式或取得新的 Codex 参考截图，不将编译门禁或测试结果
  当作所有页面已逐像素匹配的证明。兼容接口运行和恢复验收另见
  [COMPATIBLE_STREAM_ACCEPTANCE.md](PROVIDERS.md#compatible-stream-acceptance)。

#### 2026-10-06 解构赋值门禁设计自检

1. 行为由 App.Analyzers 所有，不引入运行时依赖或反向模块引用。
2. 复用 `MOSSUI002` 的属性、组件身份和外观集合规则，不建立第二套判断。
3. 只检查编译期业务外观赋值，不改变授权、审批、取消、超时、输出或持久化策略。
4. 在已有分析器内递归检查元组目标，新测试独立一类一文件，保持文件和方法规模限制。
5. 先复现普通及嵌套解构、索引器、基类转换和样式集合绕过，再验证功能状态、
   结构布局和组件自身实现不被误拒绝。修复前 13 项拒绝回归失败，5 项合法用法通过。

##### 实测结果

- `MOSSUI002` 现在逐项检查解构目标，嵌套元组复用相同属性和集合策略；
  新增 18 项回归全部通过，相关组件、分析器、样式及配置编辑专项共 191 项通过。
- 仓库外探针临时导入真实 App 编译：原生 Button 被 `MOSSUI001` 拒绝，
  同一嵌套解构中的 Width、Height 和图标 Size 分别被 `MOSSUI002` 拒绝。
  合法探针的共享控件、功能状态、结构布局和语义样式通过，0 警告、0 错误。
  探针仅由单次命令行参数导入，没有写入 App 项目；最终普通构建不包含探针。
- 完整解决方案 1,917 项测试通过，其中 App 1,428 项，无失败或跳过。
  解决方案构建 0 警告、0 错误，修改的 C# 空白格式检查和全工作区差异空白检查通过。
  审计 884 个 C#/AXAML 文件，文件规模、单类型和方法规模没有违规。
- 日志与编译探针保留在仓库外
  `C:\Users\wzheng\AppData\Local\Temp\MossAgent-component-boundary-qa-20261006-01`。
  本轮未修改渲染样式或重新操作窗口、截图；结果证明封装和门禁，不证明 Codex 像素一致。
  没有暂存、提交、推送或更改用户数据，完整产品目标仍保持未完成。

#### 2026-10-06 可靠异步验收与审批有限布局

- 部分早期异步 Headless 回调选择了 Action 重载，其绿色结果不能独立证明
  所有对应 UI 断言已执行完成。已迁移 10 个测试文件的 15 个入口到真正等待的
  `DispatchAsync`，并新增异常、取消和完成等待探针及禁止旧调用的源码门禁。
- 可靠复测确认共享操作按钮的明确可访问名称被自动名称覆盖，以及奇数行高
  使图标与徽标出现 1-DIP 错位；在共享组件与共享变体中修正，没有放宽对齐阈值。
- 主窗口原始截图复现审批正文将决策按钮挤到窗外。共享样式限制可用宽高，
  正文滚动，按钮固定。普通权限浅色 980×700 和深色 980×640 原始帧复核通过，
  没有通过 Windows 自动化点击权限请求或执行合成请求对应的修改。
- 最终专项 132 项通过，完整解决方案 1,938 项通过，其中 App 1,449 项。
  构建 0 警告、0 错误，格式与差异空白检查通过；889 个 C#/AXAML 文件的
  文件、方法和单类型审计没有违规。详情和范围限制见 [TOOL_APPROVAL_LAYOUT_ACCEPTANCE.md](TERMINAL_TOOLS.md#tool-approval-layout-acceptance)。

#### 2026-10-06 共享面板与组件门禁复核

- 既有原生组件、外观、组件身份和内部组合门禁保持启用，业务页面继续仅绑定数据、命令与状态。
  没有通过重建组件、页面独立偏移或新增原生允许列表来修正浏览器面板。
- 新视口回归先复现祖先位置改变时原生页面停留在旧位置，再在共享组件内修复订阅与解除。
  拖动、收起重开、审批阻塞与取消、任务独立取消已经普通权限原生窗口复核；
  截图脚本、合成数据和图片均在仓库外，详见 [BROWSER_EXECUTION_ACCEPTANCE.md](BROWSER_MCP.md#browser-execution-acceptance)。
- 最终完整解决方案 2,007 项测试通过，App 1,518 项，无失败或跳过；构建 0 警告、0 错误。
  904 个 C#/AXAML 文件的文件、方法和单类型规模审计均通过，修改的 C# 格式检查通过。
- 未新增 Codex 同内容参考截图；本次原生复测仅浅色 1280×820，不声称所有页面和状态已逐像素一致。

#### 2026-10-06 组件封装与强制门禁最终复核

- 当前全部业务 AXAML 继续使用 `MossAgent.App.Controls` 视觉组件；基础控件和
  项目、会话、设置列表等组合控件沿用同一套共享实现，不重复增加包装层。
  业务页面负责数据、命令、状态和结构布局，图标、字段、表面与浮层的外观集中维护。
- `MOSSUI001` 和 AXAML 原生元素/命名空间门禁拒绝直接使用原生或第三方视觉控件；
  `MOSSUI002` 与共享外观门禁拒绝页面独立覆盖样式、模板、尺寸和内部组成。
  编译诊断不能被降为警告或通过 pragma 禁用。Grid、StackPanel 等结构布局仍允许，
  共享组件的底层原生实现和框架生成的模板内部元素不被混同为业务绕过。
- 本次重新执行组件边界、共享样式、原生控件及项目编辑专项，402 项通过；
  最终 C# 状态的解决方案构建 0 警告、0 错误。完整回归 2,047 项通过，App 1,558 项。
- 项目摘要、实际鼠标重命名保存、输入值设置、错误重试、展开与收起的明暗主题
  原生验收及限制见 [PROJECT_MANAGEMENT_ACCEPTANCE.md](UI_WORKFLOWS.md#project-management-acceptance)；截图脚本和合成数据留在仓库外。
- 本轮复核没有新增 Codex 同内容对照截图，不将封装与编译通过称作所有细节已经一比一还原。
  仍需继续校准不同页面、窗口尺寸、DPI 和全部交互状态；完整产品目标保持进行中。

#### 2026-10-06 输入区上下文条封装与运行越界修复

- 新增 `AppComposerContextBar`，项目、分支与代理的组合、浮层和响应式列宽归共享组件。
  页面仅传数据、命令与项目选择业务内容；C#/AXAML 门禁禁止独立内容和尺寸覆盖。
- 长项目/代理名称在窄布局中先复现分支宽度 0、代理越界；首次 Headless 通过后，
  原生截图继续暴露迟到代理加载的旧测量。补充迟到加载失败回归，并保留既有列定义
  更新宽度，最终原生截图确认三列有界、分支恢复；不靠隐藏或裁剪操作通过验收。
- 实际鼠标打开代理菜单、切换默认代理、收起检查器与打开项目选择器；草稿保留。
  明暗主题、原始尺寸截图和临时宿主超时/关闭限制见 [COMPOSER_CONTEXT_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#composer-context-component-acceptance)。
- 最终完整测试 2,090 项通过，App 1,601 项，包含最后补充的可见图标与垂直中心断言；
  构建 0 警告、0 错误，格式检查通过。
  920 个源文件的文件、方法和单类型规模审计无违规。本轮未取得 Codex 同状态逐像素对照。

#### 2026-10-07 对话标题栏继续收口与原生排列修正

- 新增共享 `AppConversationHeader`，标题/状态分配、菜单生命周期和右上角操作归组件；
  页面只传业务内容、语义状态和现有命令。C#/AXAML 门禁拒绝内部组合和尺寸覆盖。
  缺失目标/命令、CanExecute、换会话、换上下文和卸载均有成功或拒绝回归。
- 真正运行状态加长 MCP 警告先复现明暗主题标题坍缩。原生菜单继续暴露 StyleKey
  选择器不匹配，补充实际宽度断言并修正共享选择器，没有放宽断言或隐藏操作。
  检查器展开时不重复显示对话区展开入口，收起/重开、菜单和 MCP 设置入口实际复核。
- 原生最大化复测又发现早期测量约束使检查器仅 296 DIP；在共享面板最终排列中
  使用实际宽度配置列。六项新回归在旧 DLL 上四项失败，修复后全部通过；
  最终明暗最大化帧均为 752-DIP 检查器，不在页面添加独立 Width。
- 完整解决方案 2,229 项通过，App 1,740 项；构建 0 警告、0 错误，
  修改的 11 个 C# 文件格式验证通过，945 个源文件规模与单类型审计无违规。
  全工作区差异空白检查通过，没有暂存、提交或推送。
- 取得被动 Codex 参考帧和未缩放标题栏对照，但业务内容、检查器用途和运行状态不同；
  不宣称逐像素一致。实际证据和早期间歇浏览器测试异常见
  [CONVERSATION_HEADER_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#conversation-header-component-acceptance)；完整产品目标仍进行中。

#### 2026-10-07 自适应子内容最终复核

- 外层列宽正确仍不能证明内部布局正确。新六项回归先全部复现欢迎标题使用旧测量模式，
  共享面板配置最终列宽后重新测量模板，专项 106 项通过；不为明暗主题各写一套页面补丁。
- 最终原生深色展开检查器帧的标题实际宽 812 DIP，三列一行；同尺寸浮动上下文帧也已复核。
  未缩放前后图与中途有效缩放变化、工具像素错误的限制见 [WORKSPACE_PANE_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#workspace-pane-component-acceptance)。
- 完整解决方案 2,235 项通过，App 1,746 项；构建 0 警告、0 错误。
  新修改 C# 格式检查通过，946 个源文件的规模和单类型审计无违规；没有暂存、提交或推送。
  原生项目菜单新增输入验收未通过，不将 Headless 命令回归冒充实际鼠标操作成功。

#### 2026-10-07 设置共享外观实测

- 对照用户设置截图和 Python 被动窗口截图，在共享层修正标题字号/字重、导航图标、
  图文间距、选中行宽度及底色；保留已保存的配色和工作区原有选中表面。
- 标题墨迹从 54×26 改到参考的 47×23 像素，导航图标从 14×16 改到 14×14；
  选中底色为参考的 #E2E9ED。原生验证覆盖 1280×820、实际最小宽度 980×820，
  明暗切换、提供商导航/模型展开以及返回主界面，临时宿主正常退出。
- 最终专项 63 项、完整解决方案 2,378 项通过，构建 0 警告、0 错误。
  详见 [SETTINGS_SHARED_APPEARANCE_ACCEPTANCE.md](UI_WORKFLOWS.md#settings-shared-appearance-acceptance)；尚有字体、字距和像素偏移差异，
  深色只有运行回归证据，不能宣称所有页面已经与 Codex 一比一一致。

#### 尚未证明的范围

- 对照截图的内容和窗口状态并不完全相同，不能视为逐像素一致性证明。
- 普通窗口截图右上角存在其他窗口的小范围遮挡，不用于证明全部窗口控件细节。
- 本轮未穷尽全部页面、DPI、滚动、悬停、焦点、禁用和错误状态。
- 编译和组件检查通过不等于功能端到端验收或 Codex 界面一比一还原完成。
- 追加弹出层约束只补强开发边界，没有修改渲染样式或新增逐像素截图验收。
<!-- /source: UI_COMPONENT_ACCEPTANCE.md -->


---

<a id="workspace-pane-component-acceptance"></a>

## WORKSPACE_PANE_COMPONENT_ACCEPTANCE.md

<!-- source: WORKSPACE_PANE_COMPONENT_ACCEPTANCE.md -->
### 工作区面板组件验收

#### 2026-10-06 实现与边界

- 全项目已有共享视觉组件、AXAML 编译门禁和 C# 分析器，不另建一套组件体系。
  本轮把主工作区的列宽和可调整分隔条继续收口到共享组件，页面不再维护这套尺寸。
- 原检查器展开后固定 300 DIP，最大化时与 Codex 当前约 750-DIP 的检查区差异明显。
  修改前两项明暗主题 MainWindow 回归均失败，实际检查器宽度为 300。
- `AppWorkspacePanes` 接收现有对话和检查器实例，拥有列定义、比例和浮动卡片位置。
  展开时默认占可用工作区 47%，对话/检查器最小宽度为 360/280 DIP。
  `AppPaneSplitter` 封装原生 GridSplitter，4-DIP 命中宽度、悬停和键盘焦点状态统一维护；
  方向键每次调整 10 DIP。框架的默认 6-DIP 最小宽度显式收口为共享 4 DIP。
- 已调整的检查器宽度在临时限幅、收起重开和重新附着后保留，不将限幅结果覆盖偏好。
  收起后既有上下文卡片仍为 300 DIP，顶部 58、右侧 16 DIP；关闭时分隔条不可见、不可用。
  对话、检查器及其内部浏览器不因调整尺寸而重建，列宽订阅随视觉树附着/卸载。
- `MainWindowViewModel` 仅维护开关和用户上下文偏好，移除列宽、列位置与浮动外边距属性。
  `WorkspacePaneVariants.axaml` 统一持有几何和分隔条外观。
- `ValidateSharedWorkspacePanes` 覆盖全部业务 AXAML；`MOSSUI002` 检查 C# 对应写法。
  拒绝替换 Content、Padding、尺寸、内容对齐、默认比例、最小宽度、浮动位置及分隔条配置。
  普通属性、属性元素和原生属性所有者限定均受检查，语义页面组合和可见状态仍允许。

#### 实现自检

1. 所有权在 App Controls、Styles 和本地化资源；分析器仅负责编译约束，依赖方向保持有效。
2. 复用现有面板、ViewModel 命令和 Avalonia 布局契约，不增加应用端口或直接调用工具。
3. 不执行用户任务、不读取真实凭据、不改变授权、审批、超时、取消和输出限制。
   没有持久化迁移；本轮宽度偏好仅在组件生命周期内保留，不声称跨应用重启保存。
4. 每个新 C# 文件仅一个类型，文件和方法规模按现有硬上限审计。
5. 成功路径覆盖明暗主题、真实鼠标与键盘、限幅及恢复、收起重开和卸载订阅。
   拒绝路径覆盖实例覆盖；既有浏览器宿主、任务隔离和审批呈现回归保持通过。

#### 测试方式与证据

- 外部证据目录：`%TEMP%/MossAgent-pane-layout-qa-20261006-01`。
  Python 截图/对照脚本、编译探针、预览宿主和合成 SQLite 数据都不加入仓库。
- 原生预览运行真实 App/MainWindow，使用独立数据与不可请求的合成提供商。
  `QA_ELEVATED: False`，没有发出模型请求或执行文件、命令、浏览器工具。
  Python 被动 PrintWindow 仅捕获指定 PID，并检查稳定边界、实际尺寸和非黑像素。
- `final-narrow-light/workspace.png` 与 `final-narrow-dark/workspace.png` 为 980×640；
  对话 360、检查器 296 DIP，标题完整、输入区及面板操作可见。
  `final-max-light/workspace.png` 为 1920×1032，对话 844、检查器 752 DIP。
- `pane-before-after.png` 并排放置同尺寸的真实最大化前后帧，不拉伸。
  `codex-mossagent-panes.png` 并排比较 Codex 和 MossAgent；Codex 原始 1936×1048
  只裁去四边各 8 像素，得到 1920×1032。正文、数据和检查器用途不同，不能据此宣称像素相同。
- `native-interaction/stdout.log` 与原始帧验证了实际鼠标拖动：检查器从 451 到 551 DIP；
  分隔条获得可访问焦点后按左键，到 561 DIP；收起变为浮动 300，重开恢复 561。
  最大化后仍为 561；通过还原按钮返回 1280×820 后仍为 561。
  原生缩窄至 980×820 后限幅为 296，重新最大化恢复 561，证明临时限幅不覆盖用户偏好。
- Computer Use 拒绝将拖动终点放到当前窗口外，因此没有将该失败动作当作扩回成功；
  重新观察同一窗口后改用最大化验证扩展。输入只作用于任务自建预览，没有操作 Codex 界面。
- MainWindow 的明暗/宽窄初始布局分为四项独立测试。Headless 的整窗异步 resize 需要
  调度绘制，先前碰到共享下划线的线程异常，不以未更新的 ClientSize 证明缩放成功。
  同一共享控件的真实 Headless resize、恢复及原生窗口缩放另有明确覆盖，不删减几何断言。
  既有外框回归的固定 300-DIP 断言更新为独立比例与最小宽度预期，保留圆角和各边界检查。
- `policy-probe` 直接导入实际 App 构建规则：原生 Button 和面板 Width 覆盖均被拒绝，
  两次退出码 1 且错误来自对应门禁；共享页面组合退出码 0。没有临时修改仓库页面。
- 交互预览和最终三份预览都通过关闭信号正常退出，日志与进程状态验证关闭。
  较早的 `after-max` 预览由四分钟验收超时关闭，其截图仍有效，不将超时日志作为正常关闭证据。

#### 验收范围

- 组件体系覆盖全部业务页，不仅四张示例；原生结构布局和框架生成的模板元素仍允许。
  视觉控件只在共享 Controls 中封装，页面通过语义属性与共享变体选择外观。
- 编译约束和当前截图通过不代表所有 DPI、所有工作流或 Codex 一比一还原已经完成。
  本轮没有原生 WebView2 页面运行和浏览器工具端到端验收，只完成宿主与隔离回归。
- 不暂存、提交或推送；整个产品目标保持进行中。

#### 最终验证结果

- 定向组件、布局、浏览器宿主/任务隔离/审批呈现 72 项通过；新增窗口布局和组件约束
  共 28 项通过。更新外框断言后的布局专项 45 项通过。
- 完整解决方案测试 2,002 项全部通过，其中 App 1,513 项；没有失败或跳过。
- 解决方案构建 0 警告、0 错误。本轮 11 个 C# 文件的解决方案级空白格式检查通过。
- Roslyn 审计 903 个 C#/AXAML 文件，文件规模、单类型及方法硬上限均无失败。
- `git diff --check` 通过，暂存路径为 0。四个本轮交互/最终预览的关闭日志均返回 0，
  对应 PID 均已确认不存在；临时脚本和截图仍只保留在仓库外。

#### 2026-10-07 原生最大化排列回归

- 后续标题栏验收发现另一种启动顺序：主窗口先完成最大化，随后仍设置普通窗口 Width，
  再展开检查器。实际工作区宽 1600 DIP，但测量约束为 660 DIP，检查器仍被限幅为 296。
  旧的初始宽窗口及拖动测试不能独立覆盖这一排列与测量宽度不同的路径。
- 共享 `AppWorkspacePanes` 在最终排列时重新按实际宽度配置几何；没有在页面补 Width，
  没有改变默认比例/最小宽度，也没有将临时限幅结果写入用户偏好。
- 最终六项新测试在仓库外旧 App DLL 上复现四项失败，再在当前构建全部通过；
  连同既有拖动、键盘、浏览器和主窗口布局共 21 项通过。
  普通权限明暗主题最大化原生日志均为工作区 1600、对话 844、检查器 752、分隔条 4 DIP。
- 同一最终状态的完整回归 2,229 项通过，构建 0 警告、0 错误；证据、生命周期和限制见
  [CONVERSATION_HEADER_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#conversation-header-component-acceptance)。不将该布局修复称作所有页面像素一致。

#### 2026-10-07 自适应子内容复测

- 上述列宽修正仍不充分：原生宽窗口的欢迎标题继续使用窄测量时的三行模式。
  只读日志确认标题实际宽 812 DIP，但仍为一列三行。不能只检查外层面板 Bounds。
- 新增六项明暗主题回归：宽测量/窄排列、窄测量/宽排列和反复切换。
  修改前六项全部失败，既有列宽回归仍通过；实际文字节点和项目命令明确被断言。
  共享面板在配置最终列宽后重新测量模板，修复后全部通过，相关布局与标题专项 106 项通过。
- 没有修改 `AppProjectPrompt`、业务页面、断言阈值或字体；保留现有组件、命令和面板身份。
  所有权仍在 App Controls，复用 Avalonia 布局契约，不新增端口、工具副作用、授权例外或迁移。
  新测试独立一类一文件，继续审计文件和方法规模；既有浏览器、封装与权限回归保持启用。
- 原生复测时有效缩放发生变化；同为 1920 像素窗口不等于相同 DIP 宽度。
  因此旧帧与新帧不作为同尺寸像素差分证据。最终原生帧使用当前缩放及独立合成数据库。
- 本轮 Computer Use 先返回几何不可用，再拒绝窗口边界变化；刷新后的像素帧又显示 Codex，
  与 MossAgent 的目标元数据和 UIA 树不一致，立即停止自动点击。
  `prompt-after-dark-inspector-closed.png` 来自失败点击后的诊断帧，不能证明面板收起成功。
  本轮新增的交互验收仅限可靠 Headless 命令路径，不声称原生项目菜单点击已经通过。
- 仓库外 Python 捕获增加线程级 Per-Monitor DPI 上下文并在结束后恢复，防止 PrintWindow
  在高 DPI 下仅绘制位图左上区域；不更改 Windows 缩放、隐私或安全设置，不修补 Codex 插件。
  最终指定 PID 的被动截图与工具像素帧分别处理，仍不声称 Codex 全界面一比一还原完成。
- 后续重新启动的最终宿主恢复有效缩放 1。`prompt-final-docked-dark.png` 为 1920×1032：
  对话宽 844、标题宽 812 DIP，三列一行；`prompt-final-float-dark.png` 为同尺寸浮动上下文状态。
  `prompt-same-width-before-after.png` 使用同样 812-DIP 标题的原生前后帧未缩放裁剪。
  中途有效缩放不同的诊断帧被排除，不将最终缩放 1 的结果当作高 DPI 全覆盖证明。
- 最终完整解决方案 2,235 项通过，App 1,746 项；构建 0 警告、0 错误，
  修改的共享面板与新测试空白格式验证通过，946 个 C#/AXAML 文件规模与单类型审计无违规。
  最终真实 App DLL 与仓库构建 SHA256 相同；新建四个预览均正常关闭，返回 0，PID 均确认不存在。
  全工作区差异空白检查通过，暂存路径为 0。脚本和图片留在仓库外，目标保持进行中。
<!-- /source: WORKSPACE_PANE_COMPONENT_ACCEPTANCE.md -->


---

<a id="workspace-prompt-component-acceptance"></a>

## WORKSPACE_PROMPT_COMPONENT_ACCEPTANCE.md

<!-- source: WORKSPACE_PROMPT_COMPONENT_ACCEPTANCE.md -->
### 工作区欢迎标题与权限组件验收

#### 2026-10-06 实现与边界

- 原有原生控件门禁、共享外观规则保持有效，不重复实现另一套组件体系。
  本轮继续消除业务页中的欢迎标题和权限菜单拼装。
- 普通权限 980×640 的真实 MainWindow 开启侧栏和检查器后，欢迎标题原来被左右截断。
  修改前新增的四项可靠 Headless 回归中，360-DIP 会话宽度的明暗两项均失败，
  640-DIP 两项通过。测试断言两个真实文字节点存在，避免无模板控件造成空循环误报。
- `AppProjectPrompt` 拥有三段标题的组成。可用宽度不足时分行；宽窗口连续居中，
  不把项目名称和前后文推向三处。共享变体拥有字体、标签上限和对齐，
  长项目名省略而保留完整可访问名称；切换动作和可搜索菜单复用既有命令与浮层。
- `AppApprovalPolicyButton` 拥有权限图标、标签与三项菜单。所有文字使用资源文件，
  标签和图标复用 `AppIconLabel`，单个图标尺寸统一为 12 DIP。
  菜单选择仅转发现有 Workspace 命令并显示外部状态；没有组件内部的权限升级或审批。
  缺失命令和已附着菜单上 CanExecute 拒绝的动作不可用，当前项使用共享高亮。
  显式可访问名称不会被策略切换覆盖；脱离页面时关闭自己的浮层。
- `ValidateSharedWorkspacePrompts` 在 App 编译前覆盖全部业务 AXAML，拒绝内容、
  内边距、尺寸和内容对齐替换，并拒绝权限按钮 Flyout/Command/CommandParameter 覆盖。
  普通属性、所有者限定属性和属性元素同样受检；C# 由现有 `MOSSUI002` 执行对应约束。

#### 实现自检

- 所有权是 App Controls、Styles 和 Resources；依赖方向没有变化。
- 项目选择与权限切换复用已有 ViewModel 命令，不新增端口、工具或直接运行的副作用。
- 授权目录、审批管线、超时、取消、输出限制与持久化保持原样；没有迁移或自动重放。
- 每个新增 C# 文件仅一个类型，文件及方法规模按工程硬上限审计。
- 负向测试拒绝页面覆盖；功能测试覆盖真实 Workspace 绑定、缺失/拒绝的命令、
  长名称、明暗主题、260/360/980 宽度反复缩放，以及宽布局不得出现大段标题空隙。

#### 证据目录与验收范围

- 外部临时目录：`%TEMP%/MossAgent-workspace-fidelity-qa-20261006-01`。
  Python 截图脚本、对照图、预览宿主和合成数据库不加入仓库。
- 预览运行真实 App/MainWindow，使用独立 SQLite 数据和不可请求的合成提供商；
  未读取真实 API Key，未发送模型请求，未执行文件、命令或浏览器工具。
- Python 仅调用 PrintWindow，被动捕获选定 PID，并检查实际尺寸、稳定边界和非黑像素。
  首次捕获遗漏进程名 `.exe` 后缀而被拒绝；确认同一窗口仍存活后修正参数，
  没有重启应用或用其他窗口冒充失败结果。
- `before-narrow/workspace.png` 与 `final-narrow-light/workspace.png` 是相同 980×640
  原始帧；`workspace-before-after.png` 并排放置它们，未改变截图比例。
  `final-narrow-dark/workspace.png` 覆盖同尺寸深色界面；`final-light-max/workspace.png`
  是 1920×1032 最大化浅色界面，确认标题连续居中而不是向两侧拉开。
  所有预览通过自身正常关闭，日志返回退出码 0，目标 PID 均确认不存在。
- Codex 当前参考是原始 1936×1048；整窗比较仅裁去四周各 8 像素，得到 1920×1032，
  不拉伸截图。其正文和侧栏数据与预览不同，也未包含 Codex 的空会话欢迎标题，
  因此参考不能证明本轮标题已与 Codex 像素相同。
- 原生截图验证渲染；Headless 验证绑定和命令。不以原生输入操作权限策略或审批请求。
  不声称所有 DPI、全产品工作流、所有界面细节或 Codex 一比一还原已完成。
- 本轮不暂存、提交或推送，整个产品目标继续保持进行中。

#### 最终验证结果

- 新增四个测试类共 36 项定向回归通过；既有项目选择、权限状态和浮层契约随全量复测。
- 最终完整解决方案测试 1,974 项全部通过，其中 App 1,485 项；无失败或跳过。
- 解决方案构建 0 警告、0 错误。解决方案范围的本轮 12 个 C# 文件空白格式检查通过；
  初次检查发现的多行对象初始化换行已使用最小补丁修正。
- Roslyn 审计 897 个 C#/AXAML 文件，文件规模、单类型及方法硬上限均无失败。
  整个工作区 `git diff --check` 通过，暂存路径为 0。

#### 2026-10-07 测量与排列宽度不同时的复测

- 后续原生截图仍发现实际 812-DIP 的标题使用窄布局。原因在父面板：外层列宽按最终尺寸
  修正了，但自适应子内容仍保留早期窄测量的分行模式，不是需要明暗主题各写一套样式。
- 新增六项明暗主题回归全部先失败；共享 `AppWorkspacePanes` 在配置最终列后按实际尺寸
  重新测量模板，六项全部通过。相关布局、欢迎标题、输入区和标题栏专项共 106 项通过。
  保留原有 Prompt、按钮和命令，不修改页面、欢迎标题自身、字体或 720-DIP 分行阈值。
- 仓库外合成宿主只读记录当前缩放和真实布局。最终深色浮动上下文状态为标题宽 1568 DIP，
  三列一行；深色展开检查器状态另有独立指定 PID 截图，不用失败的自动点击假称切换成功。
- Windows 自动化返回的像素与目标元数据不一致，已停止点击；本轮没有新增原生项目菜单
  输入成功证据。Python 截图使用线程级 Per-Monitor DPI 上下文，不改变 Windows 设置。
  详细失败和限制见 [WORKSPACE_PANE_COMPONENT_ACCEPTANCE.md](UI_COMPONENTS.md#workspace-pane-component-acceptance)，仍不证明 Codex 空会话像素一致。
<!-- /source: WORKSPACE_PROMPT_COMPONENT_ACCEPTANCE.md -->
