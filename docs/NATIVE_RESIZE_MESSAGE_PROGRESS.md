# Native resize message progress

## Scope and safety

This continues `NATIVE_RESIZE_DIAGNOSTIC_PROGRESS.md` and the failed-state
managed/native snapshot. The original clipping defect remains open. No product
window, layout, renderer, dependency, approval, JSON, AOT or packaging code changes.

Evidence is repository-external under
`%TEMP%\MossAgent-native-messages-20261013-235250`. An external startup hook uses
Avalonia's supported Win32Properties callback to observe only WM_SIZE,
WM_DPICHANGED, WM_DISPLAYCHANGE and enter/exit-size messages. It does not set
`handled`, override the native result, resize, invalidate layout or dispatch
additional UI work. Native calls only read client geometry and DPI. It subscribes
to resize/scaling/state events, retains at most 2,048 geometry-only records and
serializes them on normal close. No window text, model content, credentials,
memory dump or user configuration is inspected by this hook.

The observation starts at WindowOpened, not before native window creation.
Its subscriptions/read calls can affect timing; it is not an uninstrumented
failure-history replay. The production App binary is unchanged. Each external
handwritten C# file contains one type. Native input uses Sky; Python captures
passive foreground pixels and performs bounded read-only fixture audits.

## Versioned native replay

The ordinary-permission production Debug/JIT App launches with independent
synthetic SQLite backup data, not real user configuration. Its assembly SHA256 is
`04DE522716B323CCBCEB3BB8B7BA0907014EC53F1C5C2B89EA8736DE14CD141F`, matching the
provider/model session checkpoint and remaining unchanged after this run's
solution build. PID17772/HWND15862122 and observer PID12692 are verified.

Native input covers the light maximized workspace, restore to 1280x820, light
Appearance at that size, dark Appearance maximize/restore, a real southeast-grip
resize to 980x640, dark workspace return, light Appearance at 980x640, maximize
and restore again. Eight clean passive screen frames have valid owner, ordinary
privilege, 96-DPI metadata and no trailing black padding. Caption, settings
content and the minimum-size workspace composer visibly reflow. The immediate
workspace restore frame has a Windows snap overlay and is excluded from the clean
frame list; the following Appearance interaction dismisses it.

The actual caption close exits the App and observer with code zero. Immediate
window enumeration races shutdown; the waiting launch session, exit records and
fresh process/port checks establish cleanup, not that first enumeration.

## Message evidence and interpretation

`native-messages.jsonl` contains 29 records without reaching the output bound.
`message-analysis.json` pairs all nine native WM_SIZE messages with subsequent
managed resize notifications. Every reported managed ClientSize, after scaling,
matches both the raw message dimensions and actual native client dimensions.
The callbacks include Application, Layout, User and Unspecified reasons.

Eight paired callbacks still have previous Window.Bounds while ClientSize is
already current. This is a transient observation: later visible frames reflow,
and the final close snapshot converges to native/client/bounds 980x640. Do not
classify Bounds inequality inside a resize callback as a settled clipping failure
or add forced layout based on it. The earlier failed-state snapshot has valid,
persistently stale measure/arrange geometry and remains separate evidence.

The current machine exposes one 1920x1080 monitor with a 1920x1032 work area.
This replay stays at scale 1 and 96 DPI: zero WM_DPICHANGED and zero scaling
events are recorded. It cannot prove the earlier 1.25-to-1 notification history,
native high-DPI behavior or a missing-resize cause. The next resize-specific
evidence must include an actual scale transition and failed-state message history,
not more healthy 96-DPI restores or a Headless-only notification simulation.

Public Avalonia issue [19431](https://github.com/AvaloniaUI/Avalonia/issues/19431)
describes a 175%-scaling restore rounding defect. That report is not evidence
that it caused this separate 96-DPI managed/native mismatch. The inspected
12.1.3/12.1.4 message-handler comparison does not establish a repair, and no
package upgrade or copied framework patch is applied.

## Validation and remaining gates

- `tests/native-message-related.trx` passes 43 window caption/resize/frame/main
  layout cases without skips. These tests are deterministic, not native DPI proof.
- `solution-build.log` records a zero-warning/error solution build. No fresh
  complete App/eight-assembly suite or full pixel suite is run in this diagnostic
  slice; the preceding checkpoint remains separately versioned.
- `final-closed.audit.json` passes exact comparison of all nineteen table
  schemas/rows, three original artifact hashes, request history and restored light
  UI settings. No model/discovery request, deletion or real-user data write occurs.
- `cleanup.json` records both zero exits; App/observer processes disappear and
  port51792 is released. The production assembly hash stays unchanged.
- The fresh passive `codex-passive-current.png` still shows the conversation,
  not Appearance. No Codex UI input occurs and no equivalent settings comparison
  is claimed.

Native clipping root cause, real DPI transitions, equivalent Appearance/PI-Desktop
references and the whole-product interaction/theme/size matrix remain open.
No script or screenshot is added to the repository; no staging, commit, push,
default output replacement or release occurs. The complete goal stays active.

`NATIVE_DPI_TRANSITION_PREPARATION.md` records a subsequent production-App baseline,
an enhanced read-only message observer with suggested/native rectangles, and a
bounded owned-window geometry snapshot. Current package/source identity is verified;
actual scale switching is requested from the user, not automated. This preparation
does not promote another healthy 96-DPI baseline to failure-history or repair proof.
