# Subagent manual approval and tool-output keyboard acceptance

## Scope and design self-check

1. App owns the shared tool-output focus appearance. Agent/provider/tool execution
   and module dependencies remain unchanged; the remaining changes are tests.
2. Existing wrappers, native scroll navigation, approval commands, production
   composition, local provider fixtures and SQLite repositories are reused.
3. Only isolated synthetic tasks use AskEveryTime. No user policy, credentials,
   application window, desktop capture, authorized root or browser is modified.
   Existing tool cancellation, timeout, output limits and audit boundaries apply.
4. New handwritten C# files each declare one type and stay below the size limits.
   Appearance belongs to shared styles, not individual business views.
5. Tests cover successful approvals, denied/canceled requests, stale request IDs,
   retained drafts, restart without replay, keyboard access and unrelated controls.

## Deterministic approval coverage

- Eight success cases use the production offscreen MainWindow, real execution
  pipeline, synthetic SQLite state and local parent/child model servers. Child
  protocols are Responses and Gemini; both context-sharing choices and themes
  are covered. Light is 1280x820; compact Dark is 526x601.
- Before approval, there is one parent request and no child request. Light uses
  mouse hit-testing and Dark uses Space while settings are open. The actual saved
  model, reasoning effort and context-sharing setting reach the child request.
- Approval permits the outer subagent.run. The existing read-only policy permits
  its internal filesystem.read_file automatically; this is not a claim that every
  child read receives a second manual approval.
- Parent/child audits preserve task identity and namespaced child call identity.
  The authorized synthetic file is read; outside content is excluded from the
  child request and the outside file remains unchanged.
- Conversation and tool disclosures open/close using actual buttons. The result
  heading is rendered as Markdown, scrolled through both nested viewports and
  hit-tested on its text, not merely asserted to exist in a hidden visual tree.
- Reopening composition and rebinding the same window restores the result
  without replaying model requests or recreating pending approvals. Existing
  drafts and settings-page state survive approval.
- Eight negative cases cover denial/cancellation across both protocols/themes.
  Unrelated or stale request IDs cannot approve the pending/completed request.
  No child request starts; audits record approval_denied or tool_cancelled.

## Keyboard usability correction

- Four real-view cases reproduce a separate UI problem: expanded output has
  vertical overflow but the shared ScrollViewer cannot receive keyboard focus.
- The tool-details-scroll shared variant now accepts focus. Tab reaches it from
  the disclosure button; PageDown/PageUp can traverse the entire output. Focus
  outside the card does not continue scrolling its content.
- Focus uses the existing rounded card's border and shared BorderFocusBrush,
  without an extra square adorner or changes to padding, border width or layout.
  Its accessible name follows the existing tool summary. Ordinary scroll viewers
  keep their previous non-focusable behavior.
- Eight pixel cases cover both themes, compact/wide layouts and 1x/1.25x/2x
  rendering. They compare the focused edge, unchanged rounded corner, stable
  geometry and complete restoration after blur.

## Evidence interpretation

- Evidence remains outside the repository at
  `%TEMP%/MossAgent-child-manual-approval-20261015`.
- `initial.trx` and `namespaced-audit.trx` retain incorrect test assumptions about
  child call IDs, Markdown renderer types and queued settings-close layout. These
  are not production regressions or a product red baseline.
- `visible-result.trx` and `nested-scroll.trx` retain intermediate visibility
  checks: nested scroll offsets must be adjusted independently, and a selectable
  text hit-test must target the text rather than full-row whitespace.
- `keyboard-baseline.trx` reproduces the actual non-focusable output defect.
  Intermediate keyboard runs also assumed unsupported native Home/End behavior;
  final coverage uses the native PageUp/PageDown interaction instead of adding
  application-specific key handling.
- These are deterministic offscreen renders, not native captures or same-state
  Codex pixel equivalence. Native manual-approval success, DPI transitions and
  full-product visual acceptance remain open. The previous 6,467-case complete
  checkpoint predates this slice and must not be increased by adding subset runs.

## Validation checkpoint (2026-10-15)

- All 317 related App cases and all 188 pixel cases pass with no skips/aborts:
  505 distinct cases. These include sixteen new approval flow/boundary cases,
  four keyboard cases and eight focus-rendering cases.
- The solution build has zero warnings/errors. Production and both tested App
  DLL copies match; the two tested assembly hashes remain unchanged throughout
  the runs. Scoped whitespace formatting and diff checks pass. Full App and the
  other six complete suites were not rerun for this
  slice; the earlier eight-suite checkpoint remains separately identified.
