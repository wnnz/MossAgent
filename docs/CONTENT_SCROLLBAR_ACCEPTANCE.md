# Shared content scrollbar acceptance

## Scope and design, 2026-10-12

- App owns this presentation-only refactor. No new port, service or native
  wrapper is needed: existing AppScrollViewer/AppListBox templates retain their
  framework ScrollBar, Track, Thumb and RepeatButton parts. Thumb visuals use
  AppBorder, respecting the shared visual-component boundary.
- ScrollBarVariants centralizes transparent tracks, hidden line buttons,
  identity transforms and six-DIP rounded visuals in twelve-DIP hit areas.
  Navigation and terminal styles retain their own palette, not duplicate chrome.
  Diff previews retain sixteen-DIP horizontal bars and visible arrow buttons.
- Native auto-hide/overlay behavior is preserved. Terminal and diff preview
  variants still disable auto-hide explicitly; settings-page bars remain hidden
  without disabling wheel input. No authorization, approval, execution, timeout,
  cancellation, persistence, concurrency or JIT/publish-policy change is made.
- All affected C#/AXAML files remain under 500 lines; the added test file contains
  one type. No application code reads user files or starts processes for this UI
  change. Native validation uses the production App with its existing
  MOSSAGENT_DATA_DIR override and a copied synthetic database, never user data.

## Baseline evidence

- Actual production Debug App and Codex windows are captured passively with the
  existing external Python PrintWindow script at 1920 x 1032, 96 DPI. The original
  App renders two-pixel Fluent grey thumbs rather than the reference rounded
  six-pixel thumb. Codex's measured resting light colour is RGB (228,228,229);
  the original App's resting colour is RGB (122,122,122).
- Initial component tests run against unchanged production styles: eight of
  sixteen cases fail because the expected shared thumb visual is absent; disabled
  and fitting-content cases already pass. The earlier build-lock and missing-test-
  import failures are not counted as behavioural baselines.
- A first shared-style replay exposes six sidebar width regressions caused by
  changing the default auto-hide mode. Removing that default-mode change fixes
  the implementation rather than loosening the existing geometry assertions.
  The corrected focused slice passes 93/93 before adding theme-switch/list tests.
- Full-window sidebar integration identifies the shared vertical thumb class
  instead of the retired sidebar-only template class. All existing width, corner,
  edge alignment, overflow and scroll-position assertions remain unchanged.

## Evidence location

Logs, TRX files, metadata hashes and passive captures are outside the repository:
`%TEMP%\MossAgent-production-ui-20261012-164046`.
The Python capture and metadata helpers are not repository changes.

## Final validation

- Twenty new cases cover both axes/themes, resting/hover/pressed geometry, drag,
  page and wheel input, disabled input, fitting content, in-place theme changes
  and thin-list navigation colours. The expanded final component/window/catalog
  slice passes 129/129; this includes the unchanged allocation test class replay.
- The intermediate sequential solution run executes 5,466 cases, with 5,461
  passes and five failures. Four are the subsequently corrected template-class
  integration selector; one is the unchanged provider-catalog allocation assertion
  (6,632 bytes instead of zero). All five affected cases pass in the final focused
  replay. The full-run log retains every assembly's counters; its explicitly named
  TRX file is overwritten between assemblies and only represents the final assembly.
  No second clean full-solution run or allocation-failure cause is claimed.
- The solution build passes with zero warnings/errors. Scoped dotnet whitespace
  verification, XML/whitespace checks and Roslyn source auditing pass. App.axaml
  is 494 lines; the new shared style is 63. The two changed C# test files contain
  one type each and have 199/150 lines, with maximum method sizes of 38/34 lines.
- The rebuilt production App starts normally without elevation, with its real
  composition root and no substituted runtime service. Binary hashes identify
  this build separately from the baseline. The 1920 x 1032 light capture measures
  a six-pixel thumb with RGB (228,228,229), matching the measured Codex width/colour.
  Thumb length differs with content and the App's right-edge position differs by
  one pixel; this is not a claim of whole-window pixel parity.
- Activation restores the App to 1280 x 820. Real main scrolling changes the
  exposed offset from 1267 to 1079.5. Child-detail content and its thumb then move
  independently while the main offset stays unchanged, verified by captures;
  its UIA value remains zero, so no native numerical child-offset claim is made.
- Actual appearance settings switch dark then light without replacing the window.
  Dark content thumbs measure six pixels with RGB (64,71,77); no current Codex dark
  reference or high-DPI comparison is available. Settings still hide their bar
  while wheel input moves the page; diff preview arrow buttons remain visible.
- The observed window closes normally and its process disappears; the external
  observer does not obtain an exit code. After closure, hashes and counts of the
  synthetic task, eleven messages, six tool executions and artifact records are
  unchanged. No message is sent or tool operation replayed during this UI check.
  User data, JIT debugging, Windows x64 single-file publishing and unrelated
  working-tree changes remain untouched. No commit, push or release is performed.
