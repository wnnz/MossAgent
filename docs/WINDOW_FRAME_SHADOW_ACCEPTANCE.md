# Window frame shadow progress and native resize failure

## Root cause and shared repair

Equal-size light native screenshots show a spatial shadow gradient beside the
Codex workspace, not a different flat navigation-rail brush. MossAgent's frame
had ClipToBounds=True, clipping its own outer shadow. Its ancestor AppPageSwitcher
also inherits UserControl's clipping, removing the shadow above the workspace.

WindowVariants now separates the outer shadow frame from an inner rounded
content clip. Both retain the same 12-DIP radius; frame dimensions, margins,
sidebar width, commands and input regions are unchanged. Only the outer window
page switcher stops clipping. Nested page switchers retain their defaults.
The inner clip prevents oversized/transformed children escaping any corner.

SharedWindowFrameShadow owns the measured window-only variant: zero-offset
28-DIP blur at alpha 12, plus a zero-offset one-DIP edge at alpha 4. Existing
SharedSurfaceShadow and all card/dropdown shadows remain unchanged. The build
guard permits the new token only on the exact shared window-frame selector;
other selectors, arbitrary shadows and unknown tokens remain rejected.

## Design self-check

1. App's existing shared styles own this rendering change; module dependencies
   remain unchanged.
2. Existing AppBorder, AppPageSwitcher and shared resources suffice; no new
   service, global state, native hook or execution port is introduced.
3. No tool, path authorization, approval, timeout, cancellation or output policy
   changes. Native QA uses an independent database backup and sends no request.
4. Changed AXAML remains under its hard limits; both changed test files contain
   one type and stay below 250 lines. No lifecycle subscription is added.
5. Pixel tests cover light/dark, three window sizes and 1/1.25/2 render scaling,
   shadow reach, unchanged distant rail color, upper shadow and rounded overflow
   rejection. Resource/build-guard tests preserve card tokens and reject misuse.

## Verification and version ownership

Evidence root: `%TEMP%\MossAgent-frame-shadow-20261013-123343`.
Final production App SHA256:
`2265AB5B949EE62B775FCE7D4E03D0809ABE9076E97F6A539E5C755B0748BCD1`.
Output remains ignored Debug/JIT `.scratch/caption-focus-20261013`; the default
output is not substituted. No AOT, JSON, publishing, commit or push changes.

- The initial fourteen-case baseline fails ten shadow-rendering cases and four
  separation-contract cases. Intermediate clipping-only/calibration failures,
  diagnostic measurements and the failed locked-app build are retained, not
  reclassified as successful product verification.
- Final relevant App tests pass 120/120; the entire pixel assembly passes 38/38,
  including all fourteen new rendering cases. Six new resource/guard cases are
  included in the App group. Final solution build has zero warnings/errors;
  scoped whitespace formatting and markup/diff checks pass.
- Fresh passive Codex and final MossAgent light frames are 1920x1032, DPI 96,
  ordinary privileges. At 60 rail-shadow pixels across three rows, mean absolute
  RGB error drops from 2.2833 to 0.3333 and maximum error from 11 to 1. The earlier
  (40,400) difference is corrected without recoloring the distant rail. Upper
  titlebar-shadow samples still differ by up to two RGB units. Unscaled crops,
  coordinates and metrics are retained; whole-window equivalence is not claimed.
- All nineteen SQLite table schemas and rows remain identical to the original
  backup; three original artifact hashes and the four-request log are unchanged.
  No model request, browser/terminal process or approval-setting action occurs.
  The final fixture returns to Light and exits normally with Alt+F4, App/observer
  both exit zero, their original processes are absent and port 51792 is released.

## Native resize acceptance failed

On the final production binary, clicking restore changes the native window from
1920x1032 to 1280x820, but the content retains its maximized arrangement: caption
buttons, composer and right context card are outside the visible client. Both
passive PrintWindow and foreground screen captures independently confirm this.
The capture metadata's valid flag does not establish successful layout.

The visible View menu remains interactive and switches to Dark and back to Light;
the clipping persists, so this is not merely a frozen screenshot or an unresponsive
process. A post-restore Computer Use geometry error is not treated as evidence
that restore did not execute, and the caption is not blindly clicked again.

This reproduces the earlier open native resize defect but does not establish its
root cause or a repair. Passing Headless tests are not promoted to native resize
acceptance. Narrow/native dark-layout acceptance, complete settings/inspector
states, higher native DPI, equivalent PI-Desktop references and the complete
product objective remain open. The next investigation must distinguish native
client resize delivery, layout invalidation/constraints and rendered-root size;
no speculative forced-width or restart workaround is shipped.

The subsequent controlled replays and external screenshot-anchor guard are
recorded in `NATIVE_RESIZE_DIAGNOSTIC_PROGRESS.md`. The original production
binary reproduces the failure again, while other unmodified/instrumented
replays succeed. These observations narrow the investigation but neither
establish a root cause nor repair the native resize acceptance failure.
