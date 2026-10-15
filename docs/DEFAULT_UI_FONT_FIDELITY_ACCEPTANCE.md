# Default UI font fidelity

## Reference and design self-check

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

## Baselines and deterministic verification

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

## Native screenshots and persistence

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

## Remaining limits

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
