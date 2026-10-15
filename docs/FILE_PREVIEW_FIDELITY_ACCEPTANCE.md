# File preview fidelity acceptance

Verification: 2026-10-08/09. This supplements `FILE_PREVIEW_ACCEPTANCE.md`;
it is not whole-product or complete Codex-fidelity acceptance.

## Design self-check

1. App owns the shared breadcrumb, file-tab and preview presentation. Shared
   styles own fonts, icons, tab dimensions and image spacing. Module dependencies
   are unchanged; no platform API or storage operation is added to a view.
2. Existing preview-file execution and task-artifact reader ports are reused.
   Path presentation derives from the existing display path, not another file read.
3. Authorization, read-only approval, bounded snapshot creation, cancellation,
   task ownership and artifact verification remain in the standard pipeline.
   Zoom commands only change the current decoded snapshot's presentation.
4. Every affected handwritten C# file has one type and stays below 500 lines;
   methods stay below 80 lines. Shared markup/analyzer gates also protect the
   new breadcrumb's composition and geometry against business-view overrides.
5. Tests cover measured filename priority, shrinking/expanding layout, themes,
   compact tabs, inactive/keyboard-focused close actions, empty notices, zoom
   commands, no additional tool calls and popup invalidation on owner changes.

## Changes and regression evidence

- `AppFileBreadcrumb` preserves the filename first, fades the parent path and
  hides the parent/separator when there is insufficient room. It remeasures on
  resize; natural font measurements, not assumed glyph widths, drive allocation.
- File actions are 30 DIP high inside a 32-DIP selected-tab outline. Image files
  have a dedicated vector icon. Inactive close actions retain their slot but are
  not visible, hit-testable or focusable until the row is hovered/focused.
- Image zoom moves from a permanent extra row to the existing toolbar's image
  options flyout. Minus/plus, actual size and fit use real tab commands. Text
  search keeps its existing action and is mutually exclusive with image options.
- Empty notices no longer consume margin or a row. Image content starts at native
  Y=180 after the header/toolbar and shared viewport padding; the reference image
  starts at Y=188 including its eight-pixel DWM frame offset.
- The image flyout closes on tab/context replacement, hidden or disabled
  ancestors and detachment. It shares the preview's existing subscription lifetime.
- Four initial fidelity cases fail against the old 34-DIP buttons. Those cases
  pass after the compact-tab change. Eight resize cases also exposed a bad test
  assumption that the natural filename width exceeded 180 pixels; the final test
  compares arranged width to measured desired width and retains overflow,
  narrow-layout and parent-restoration assertions.

## Native screenshot and interaction evidence

Evidence root, outside the repository:
`%TEMP%/MossAgent-preview-fidelity-20261008`.
The Python capture/comparison scripts and native QA host are not product files.

- Passive PrintWindow captures the user's current Codex window without changing
  its UI, and independently captures a production MossAgent window. Pixel capture
  rejects black/uniform results and mismatched or changing native bounds.
- Both apps preview the same synthetic `combo-focus-comparison.png`. The native
  host uses the production App, MainWindow, isolated SQLite configuration and
  `FilePreview.OpenAsync` through the real authorized read-only tool pipeline.
  A loopback port-nine provider is seeded but never called; no model message is sent.
- `codex-current.png` is 1936x1048 with DWM edges; `light-wide/moss-final.png` is
  1920x1032, scale 1 and ordinary permissions. `preview-header-comparison.png`
  places original-pixel 754x238 and 752x238 crops side by side, without scaling.
  Crops align each inspector's own origin and remove the reference frame offset.
  They are not a whole-window pixel-difference score; task and tab counts differ.
- Rebuilt host, Light 1920x1032: click the image options action, then plus. The
  label changes from fit to 125%, the actual image enlarges and horizontal
  scrolling appears. Fit restores the smaller image. Clicking outside dismisses
  the flyout; clicking the text tab then shows the real code/syntax preview.
- Final Light 1280x820 and Dark 980x640 captures use the same synthetic file,
  ordinary permissions and scale 1. Narrow paths truncate within their allocated
  space and retain the complete path in the tooltip/accessibility presentation.
  The dark image-options popup stays inside the narrow window and Escape closes
  it; a fresh accessibility snapshot confirms the zoom actions are gone.
- All four native preview hosts close normally with exit 0. The three final
  host stderr logs are empty; no acceptance process is left running.
- Main-window PrintWindow may omit separate popup HWNDs. Popup interactions are
  also observed directly through Computer Use; they are not inferred from a
  main-window image alone. No Codex UI input is automated.

## Verification

- Solution build succeeds with zero warnings/errors.
- Final relevant App/component/analyzer filter: 251 passed. It includes eight
  light/dark image-menu owner invalidation cases and eight breadcrumb layouts.
- Full App run: 2398 executed, 2396 passed, two sidebar-heading cases fail only
  during temporary-directory deletion with a file-sharing violation. All 12
  sidebar-heading cases pass on isolated rerun. Unrelated cleanup code is unchanged.
- Other full suites: Agent 63, Browser 76, Infrastructure 124, Platform.Windows
  54, Providers 283 and Tools 174 all pass. Combined latest full-suite evidence is
  3172 executed, 3170 passed and two failed; this is not a clean full-suite result.
  App evidence is in `app-complete`, other full reports in `full-final`, and
  final focused/isolated reruns in `verified-final` beneath the evidence root.
- Scoped whitespace verification, Roslyn single-type/file/method audits and
  AXAML/XML validation pass. No staged files, commit, push or release is created.

## Remaining fidelity limits

- The Codex reference has more tabs. Add-tab and expand/restore controls now have
  real behavior, covered in `FILE_PREVIEW_WORKFLOW_ACCEPTANCE.md`.
  External-editor selection is not implemented;
  the existing open action only reveals an authorized original file in Explorer.
- The follow-up below corrects missing shared toolbar shadows, rectangular image
  corners and very narrow breadcrumb allocation. The later copy/image optical
  sizes and multilayer Explorer badge are verified in `PREVIEW_ICON_ACCEPTANCE.md`;
  this does not establish pixel equality for every action. Dark/narrow Codex
  same-state references and Codex's image-options menu still require comparison.
- Automation same-state list/editor references remain outstanding. This preview
  slice does not validate scheduling behavior or close the broader product goal.

## 2026-10-09 toolbar follow-up

### Design and boundaries

- App's shared `AppFilePreview` owns a read-only `IsToolbarCompact` property.
  Below 400 DIP it preserves the breadcrumb, copy split button and search/image
  options, and puts reveal/refresh in a real localized action menu. Wider panes
  restore the existing visible actions. No action, authorization or port is removed.
- The compact menu uses the existing pane commands; reveal remains disabled for
  read-only tasks and stored snapshots. Refresh still uses the authorized preview
  tool and artifact reader, with their unchanged cancellation/resource limits.
- Resize, tab/context/policy changes, hidden/disabled ancestors and detachment
  close the menu. The preview continues using its existing subscription lifetime.
- Breadcrumbs, split-action borders and outline-button content presenters use
  the same `SharedSurfaceShadow`, with zero horizontal/vertical offset. Toolbar
  action vectors are 14 DIP; chevrons remain 10 DIP, with unchanged hit areas.
  Image options use a dedicated document/magnifier vector rather than text search's
  generic magnifier. The shared image frame clips at an eight-DIP corner radius.
- A trial that replaced grid columns during `SizeChanged` failed 25 relevant
  tests with an Avalonia `Grid.MeasureCellsGroup` index error. The final layout
  instead keeps four columns and groups the wide-only actions in the last column.
  It neither mutates grid definitions during arrangement nor leaves an empty
  column's spacing when compact. This trial is not part of the final implementation.

### Tests and native evidence

- Final focused preview, workspace-pane and related component filter: 155 passed.
  Twelve new compact-toolbar cases cover 300/399/400/640-DIP transitions, light
  and dark themes, readable filename allocation, no trailing empty-column gap,
  presenter shadows, actual commands, read-only denial and menu invalidation.
  Existing image fit/zoom/resize cases also verify the frame's geometry and clipping.
- Final solution and external native-host builds: zero warnings/errors. Scoped
  whitespace, Roslyn single-type/file/method limits and AXAML/resource XML checks
  are performed on the affected files. No AOT or publication setting is changed.
- Final full solution run executes 3222 tests: 3220 pass and two Windows MCP
  cases fail (`BlockingUiCaller_DoesNotPreventMcpInitialization` with manager enabled,
  and `ConfigurationChange_ReclaimsPreviousServerAndDescendants` with disable).
  App's final 2448 tests all pass, as do Agent 63, Browser 76, Infrastructure 124,
  Providers 283 and Tools 174. A subsequent isolated full Windows suite passes
  all 54 cases. The latest per-suite evidence is green, but the single combined
  run is not; unrelated MCP implementations/tests are unchanged. Reports are in
  `full-final`, with the Windows rerun in `isolated/windows-isolated.trx`.
- Evidence and all Python/QA support files remain outside the repository under
  `%TEMP%/MossAgent-preview-layout-20261009`. The host uses isolated SQLite data,
  ordinary permissions and render scale 1; the loopback test provider is not called.
- A fresh passive Python capture confirms the current 1936x1048 Codex reference
  still previews the same synthetic image. The final Light 1920x1032 capture is
  in `light-verified/moss-final.png`. `preview-header-comparison.png` contains two
  754x238 original-pixel crops, aligned to each inspector origin without scaling.
  Differences in tab counts and available actions remain; this is not a global
  pixel-difference score or proof of exact Codex responsive behavior.
- Native Light interaction narrows the inspector with the real splitter, opens
  the compact menu and refreshes the file. The menu closes and the task's snapshot
  count increases from two to three. Both before and after the fixed-column change,
  the menu's reveal action stays disabled under the seeded read-only policy.
- Native image zoom reaches 125%, creates a scrollable image and returns to fit.
  Dark 980x640 interaction opens/closes the compact menu, expands the inspector
  to the wide toolbar and restores the narrow pane/current tab. These interactions
  precede the fixed-column regrouping; the final rebuilt dark capture independently
  verifies the resulting narrow layout in `dark-verified/moss-final.png`.
- Pixel samples confirm actual image clipping, not just an assigned radius: the
  synthetic source's square corner is RGB 244/244/244, whereas the rendered corner
  retains the light/dark pane background and transitions to that gray within eight
  pixels. Fit, zoom and scrolling still operate on the same decoded snapshot.
- No Codex UI input, real provider call, message send, commit, push or release is
  performed. This follow-up does not close the outstanding whole-product fidelity
  or automation same-state acceptance work.
- All four native hosts close normally with exit 0 and empty stderr. No QA host
  or test process remains running. The checkout retains its prior unrelated work,
  with 939 dirty entries and zero staged paths at handoff.

## File-tab close chrome (2026-10-09)

The fresh passive Python Codex capture still displays the same synthetic image.
Its selected-tab close mark is substantially smaller than MossAgent's default.
Only the shared file-preview variant changes: the glyph is now six DIP with a
one-DIP stroke and theme-aware muted foreground; the action remains 24x24 DIP.
Inactive visibility, focus reveal, commands, tab selection and reserved width
are unchanged. Views do not add dimensions or bypass the shared component.

Design self-check: App/shared styles own this presentation-only change, using
the existing close command and authorized in-memory preview. There are no new
ports, file reads, tool calls, approval changes or migrations. The style and
test remain below file/method limits. Four theme/width cases assert actual icon,
foreground and hit-target geometry alongside existing empty/focus/menu behavior.
Those assertions fail against the old ten-DIP icon before the style change.
The final preview/isolation filter passes 33 tests.

Native ordinary/JIT hosts use separate synthetic SQLite data, render scale 1,
Light 1920x1032 and Dark 980x640. Actual center clicks close the selected image
tab in both windows, reducing the tab count from two to one and revealing the
remaining code preview. No file or conversation is deleted. All six hosts close
normally with exit 0 and empty stderr; no user provider is called.

Evidence stays outside the repository in
`%TEMP%/MossAgent-mcp-concurrency-20261009`. The 740x186 original-pixel comparison
is `preview-tab-chrome-comparison.png`, using the fresh Codex frame, the original
MossAgent frame and `native-light-normal-final/moss.png`. The final default-state
capture moves the QA pointer away from the tab before launching the light host,
so it is not compared against a hovered Codex tab. Dark default pixels are in
`native-dark-final/moss.png`; both actual click results are separately retained.
The passive screenshot and comparison scripts are not source or release assets.

At luminance below 180, the measured close-mark ink bounding box changes from
10x10 to 6x6 pixels, matching this Codex reference's 6x6 mark. At the lighter
220 threshold MossAgent is 8x8 and Codex 7x7, so the anti-aliasing is not identical.
The different tab count/names and remaining header/action geometry are not a
whole-pane pixel-perfect acceptance result. This slice does not complete the
automation same-state comparison, all DPI/themes/states or real-provider flows.

Final solution/native builds have zero warnings and errors. The scoped AST,
whitespace and diff checks pass. Two earlier broad runs pass all 3,229 tests;
the last run on the final glyph executes 3,229 with one unrelated provider/proxy
fixture cancellation during SQLite initialization, before its routing assertions.
All 12 provider/proxy cases pass in an isolated rerun. The raw last full result
remains 3,228 passed/one failed, not a clean whole-product acceptance. No proxy
implementation or fixture is changed to hide that result. No commit/push/publish
occurs; the checkout remains at 948 dirty entries and zero staged paths.
