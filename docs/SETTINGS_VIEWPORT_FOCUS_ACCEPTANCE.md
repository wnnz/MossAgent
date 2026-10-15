# Settings viewport focus acceptance

## Ownership and safety

- App owns page/drawer keyboard focus through AppPageSwitcher and
  AppNavigationPanes. No application port or dependency direction changes.
- Automatic focus excludes controls outside, or partially clipped by, any
  ancestor ScrollContentPresenter. The existing lazy visual-order traversal,
  visible-field preference and eligible remembered target are retained.
- Drawer dismissal falls back to a visible primary control if its remembered
  field is now offscreen. No control/editor instance is replaced and no scroll
  offset is forcibly reset. Explicit user focus and scrolling remain supported.
- No tool, approval, timeout, output, provider, SQL, JSON/AOT or release policy
  changes. No global service or native resize workaround is introduced.
- The changed controls and new tests each contain one type and remain within
  the file/method limits. Negative tests cover clipped and nested viewports;
  positive tests cover retained visible editor focus, scroll and draft.

## Reproduction

Evidence is outside the repository:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-settings-focus-20261014`.

The initial ordinary-permission Debug/JIT App was verified against SHA-256
`3F90E07CCADEC5D85893D5AEFB22CB5BFB07EB75860724C4BFCF211CE68C1773`.
Opening Appearance at 1280x820 focused the first palette combo. After shrinking
to 526x601, opening the settings drawer and selecting Appearance restored that
offscreen field. The native heading disappeared; a wheel scroll back to the top
restored it without resizing the window. Python passive captures preserve both
states under `native/before-drawer-selection.png` and
`native/before-scroll-restored.png`.

Eight new production-MainWindow regression cases fail before the change with
an actual vertical scroll offset of 96 rather than zero: initially compact
entry plus selection, outside click and Escape dismissal, in both themes.
This establishes a focus/BringIntoView problem for this reproduction, not the
older managed-client/native-client size mismatch.

## Automated validation

- All sixteen new cases pass in `tests/focus-green.trx`: ten real MainWindow
  cases and six viewport-selection cases, including partial horizontal/vertical
  clipping, nested clipping and explicit user editing.
- The initial broader candidate records 140 passes and one new numeric-test
  failure. That test initially tried to focus the NumericUpDown wrapper instead
  of its real TextBox, then expected the editor as the first automatic target
  although the wrapper retains field precedence. The fixture now uses the actual
  editor for explicit focus and preserves the existing wrapper-precedence check.
  The failing logs/TRX remain; they are not reported as green runs.
- Complete visual suite: 72 passes. Architecture constraints: seven passes.
- Scoped formatting and the complete solution build pass with zero warnings
  and errors. Complete App regression: 4,335 passes with zero failures, skips or
  aborts. The external verifier checks driver termination, individual TRX
  outcomes/counters, assembly hashes and native fixture identity independently.
  The sixteen new cases are already included in the App total, not added again.
  Six non-UI full suites were not rerun for this App-only slice.

## Native validation

The fresh independent `native-after` fixture uses App SHA-256
`5D7A541B1F7B35B53E13F32B595BF15DF317BA6476218D55CE3DE5059E762BEB`.
Python frames verify compact Appearance retains its heading after selecting
the drawer destination. Dark-theme Escape dismissal likewise retains the top
of the page. Explicit wheel scrolling and focusing the background-color field
remain possible; closing the drawer restores that same field and its scroll
position without changing its value.

Accepted passive frames include 96-DPI 526x602 and 120-DPI 658x753 windows.
They are not interchangeable pixel references. `focus-before-after.png` places
the 96-DPI 526x601 before frame and 526x602 after frame side by side without
resizing, disclosing the one-pixel height and scroll/hover-state differences.
This is bug-reproduction evidence, not Codex pixel equality or a measured
Codex compact breakpoint.

A maximize input produced a wide Sky frame, but the subsequent Python capture
reported a normal, non-maximized 658x753 window. Its evidence is named
`light-after-maximize-request`; this attempt is not credited as native maximize
acceptance. It does not invalidate the independently captured compact title and
editor-restoration checks.

Before and after preview Apps/observers exit zero; port 51792 is released.
All nineteen table fingerprints, three original artifacts, four-request
history and restored-Light UI bytes remain unchanged after process exit.
No provider/model request, automation execution or approval-setting change
was made. The fixture directories are closed and must not be reused.

## Remaining scope

The last passive Codex reference is maximized at 1920x1020 and 120 DPI, but still
a conversation, not same-state Appearance. Earlier 600x750 conversation frames
are retained rather than relabeled as Appearance.
Full settings/Codex fidelity remains unaccepted. The older native managed/native
geometry mismatch, WM_DPICHANGED event-chain diagnosis and complete product
goal remain open. Healthy frames or a restart are not evidence of their repair.
