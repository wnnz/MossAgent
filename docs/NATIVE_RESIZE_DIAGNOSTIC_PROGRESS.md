# Native resize diagnostic progress

## Scope and status

This investigation follows `WINDOW_FRAME_SHADOW_ACCEPTANCE.md`. No product UI,
window-state, sizing, rendering, authorization or persistence code is changed.
The native restore defect remains open. Successful replays, a new output path,
diagnostic subscriptions and restarts are not a repair.

`NATIVE_RESIZE_MESSAGE_PROGRESS.md` adds a later, separately versioned native
message replay: nine size notifications match actual client geometry, while
callback-time Bounds can temporarily lag. No actual DPI transition or failed
message history is captured; this does not repair the earlier defect.

Evidence stays outside the repository in
`%TEMP%\MossAgent-native-resize-diagnostics-20261013-130044`. Python performs
passive capture and fixture audits only; native inputs target MossAgent through
Computer Use. Codex is captured passively, never clicked or reconfigured.

## Reproduction and controls

- The original production assembly, SHA256
  `2265AB5B949EE62B775FCE7D4E03D0809ABE9076E97F6A539E5C755B0748BCD1`,
  reproduces the clipped maximized arrangement after a native 1280x820 restore
  in the independent `plain` fixture. This is a new failure, not just the
  earlier retained image. Capture metadata reports non-black content, but the
  caption and composer are missing from their expected locations.
- The real production App composition and MainWindow, launched by an external
  diagnostic entrypoint, reflow correctly in two `current` restores. A matching
  WinExe/manifest replay in `native` also reflows correctly. The reduced
  `minimal-2` probe removes LayoutUpdated subscriptions, background layout
  observations and live JSON output; its restore still reflows correctly.
- A standalone production entrypoint from the diagnostic build also reflows
  correctly, without telemetry (`plain-new`). Its assembly SHA256 is
  `6E10E54B78BE6C11D036ED5D5B8B39EE6FF920AD15FA6A062B05141F2D8C2CD3`.
  Normalized method-instruction comparison reports no differences in eleven
  application assemblies, after excluding generated regex path-dependent
  names. App embedded-resource hashes and native Apphost hashes match. This
  narrows the investigation, but is not complete binary or timing equivalence
  and does not establish a stale-build cause.
- A delayed read-only hook on the original production binary (`hooked`)
  observes a successful restore. A separate original-binary hook that adds
  only a geometry-filtered TraceListener, with no UI dispatcher work or
  geometry subscriptions (`log-only`), also restores successfully. It reports
  no geometry warning during that successful replay; failed-layout exception
  logging has not been obtained.

Successful telemetry shows native ClientSize updating before Window/Root Bounds,
then the bounds settling at 1280x820 and the caption moving inside the client.
WindowState notifications can arrive before or after this sequence. These are
observations of healthy transitions, not the missing sequence from a failed one.

The first reduced `minimal` attempt is excluded: startup telemetry reports
RenderScaling 1.25 and later 1.0; PrintWindow has trailing black padding and
fails the capture guard. There is no recorded transition to Normal from that
attempted input. It is not native restore or higher-DPI acceptance.

## Screenshot layout guard

### Later failed-state geometry snapshot

External evidence in `%TEMP%\MossAgent-live-geometry-20261013` captures the actual
PID14468/HWND18483404 failed state, rather than only a healthy telemetry replay.
The assembly is the standalone diagnostic build SHA256
`6E10E54B78BE6C11D036ED5D5B8B39EE6FF920AD15FA6A062B05141F2D8C2CD3`.
A bounded CLR snapshot reads geometry and callback metadata only: no string
contents, secrets or memory-dump file are saved. Its two-million-object bound
is reached, so observed instance counts are not whole-heap uniqueness claims.

| Observed surface | Size | Scaling |
| --- | --- | --- |
| MainWindow cached ClientSize, Bounds, DesiredSize and arrange bounds | 1536x816 | 1 |
| Native client/window bounds | 1920x1020 | 96 DPI, per-monitor-v2 |
| Client/server composition targets and server layer | 1920x1020 | 1 |

MainWindow measure/arrange are valid, and its PlatformImpl reference matches the
observed Win32 instance. Resized still targets Window.HandleResized;
ScalingChanged retains PresentationSource and TopLevel callbacks. LayoutManager's
owner is PresentationSource, not Window; a false owner-equals-Window check is not
an error. A passive post-snapshot frame still has trailing black padding.
This establishes a real managed/native geometry mismatch, not its event history
or a missing-callback root cause.

The official 12.1.3 WM_DPICHANGED path updates scale, calls ScalingChanged and
SetWindowPos, relying on WM_SIZE for Resized. TopLevel's scale handler does not
update cached ClientSize, while PresentationSource uses the platform client size.
An external Headless-only simulation delivers ScalingChanged, sets platform size
without Resized, then explicitly delivers Resized. All eight simulated 1/1.25
and 1/2 transitions in Normal/Maximized preserve the stale cache before the
notification and converge afterward. The first two attempts are excluded: their
incorrect ordering allows Headless to reset the platform size. Corrected output
is `dpi-event-repro-corrected.json`. This synthetic consistency demonstration
does not prove the native event was lost or repair it. No reflection/callback
invocation is added to product code, and the package is not upgraded.

After independently verifying the native owner, a single Computer Use activation
recovers foreground pixels and restores that old preview to a normal 1536x816
window. `focused-screen.png/json` is valid, but is not a maximized/DPI repair.
The old App then closes normally with exit zero. The later native settings replay
and title correction are recorded in `NATIVE_SETTINGS_INTERACTION_PROGRESS.md`;
their healthy resizes leave this original root-cause gate open.

The external `verify_layout_anchors.py` checks explicitly selected equal-state
light 1280x820 PrintWindow captures against visible close-caption and composer
send-button anchors. Non-black metadata alone is insufficient.

- Both retained clipped frames have `valid=true`, but fail both anchors:
  mean absolute RGB errors are 14.0961 and 24.9508.
- Six independent successful frames match both anchor regions exactly. The
  separately chosen `minimal-2` frame is the reference, not another test case.
- This gate checks two fixture-specific layout invariants only. It does not
  establish whole-window equivalence, other themes, minimum sizes or DPI states.

## Data and completion boundaries

All eight diagnostic fixtures retain the original nineteen SQLite schemas and
table contents, three original artifact hashes and four-request history. All
remain Light. Each App and local observer exits normally with code zero; the
original process IDs are gone and observer port 51792 is released. No model
request, browser/terminal session or approval-setting action is performed.

The passive Codex reference is currently the maximized light General settings
page. It is not an Appearance-page reference and does not close that comparison.
The diagnosis does not close native narrow/dark/high-DPI/full-state acceptance,
the resize root-cause gate or the complete product goal. No forced Width/Height,
InvalidateMeasure, UpdateLayout, native hook or diagnostic listener is added to
the product; debug output remains separate from the default executable.

The layout/resource-related App test group passes 19/19, and the complete pixel
assembly passes 38/38 without failures or skips. The solution build completes
with zero warnings/errors, and scoped documentation diff checks pass. All eight
test assemblies are not rerun in this investigation. These checks preserve
the existing deterministic contracts, not native resize or product completion.
