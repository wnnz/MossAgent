# Shared metadata and approval controls

## Scope and ownership

This pass improves the existing Codex-oriented desktop layout rather than
introducing a different visual system. It uses the UI/UX skill's accessibility
checklist and its Avalonia keyboard-navigation/shared-style guidance. The compact
consistency search did not return a verified platform-specific UX match; the
measured contrast and spacing requirements below are explicit review criteria,
not an unverified generated design system.

All production changes belong to App. Shared AXAML wrappers and theme tokens own
appearance; `ApprovalTaskControlViewModel` adapts existing workspace commands.
There is no new provider, persistence, filesystem, process, or tool-execution
implementation in UI code. Existing approval policies, task cancellation tokens,
authorized roots, output bounds, audit persistence, JSON and JIT/single-file
publication remain unchanged.

## Visible changes

- Secondary metadata uses the same semantic foreground across existing surfaces.
  Light subtle text changes from `#8A8A8A` to `#6B6B6B`; sidebar section text uses
  `#6B7075`. Dark variants use `#A0A0A0`. Main surfaces, primary text, titlebar
  colors, corner radii and shadows are retained.
- The former light subtle foreground had 3.45:1 contrast on white and 2.99:1 on
  the selection surface. The new subtle foreground has at least 4.61:1 across
  the six checked light surfaces; sidebar headings exceed 4.5:1 on their two
  checked surfaces. Both dark roles also exceed 4.5:1 on the checked surfaces.
  This is not an assertion that every text/background/opacity pair is compliant.
- Tool activity headers have matching 6-DIP horizontal insets for static and
  expandable rows. Status text uses the shared muted foreground at 11 DIP and
  a 120-DIP maximum width, with ellipsis and the complete status in a tooltip.
  The existing button help text retains the complete status for accessibility.
  Rows remain 32 DIP high and activity glyphs remain 12 DIP.
- At 125% scaling an inset can round by half a physical pixel. Rendering tests
  allow exactly that rounding bound instead of requiring an impossible exact
  logical-pixel result or disabling layout rounding.

## Approval stop recovery

Production Headless interaction exposed a real compact-layout gap: the expanded
approval card covered the composer's stop button at 526x601. It reproduced in
both themes. The first complete external driver run passed 22/24 scenarios and
retains those two failures; it was not reported as all green.

The approval footer now offers a localized stop-task action when the displayed
request belongs to the currently selected, actively running task. It reuses the
existing workspace stop command, never resolves approval as granted and never
changes an approval policy. It is hidden for missing context, another task,
completed runs and disposal. Request identity is checked against the live
approval queue, and `AppApprovalButton` retains gesture identity. Stale or null
request IDs cannot stop a different task or turn an old stop into a new send.

Focused negative tests cover another task at the head of the approval queue,
queue advancement, stale/null IDs and disposal. Stopping a pending edit leaves
the file unchanged and persists `tool_cancelled`, an unapproved audit and a
cancelled task through the existing pipeline.

## Verification and limits

- The pre-change eight-assembly checkpoint passed 6,690/6,690: App 4,815,
  Visual 282, Agent 123, Browser 88, Infrastructure 204, Platform.Windows 142,
  Providers 542 and Tools 494. All eight original test DLL hashes remained
  unchanged throughout that checkpoint. This is a baseline, not a full run of
  the later UI candidate.
- The current candidate's complete Visual assembly passes 300/300, with no
  failures, skips or aborted cases. The initial run retained four overly exact
  125%-inset assertions and six old sidebar-color expectations; those explicit
  expectations were updated to the intended design and all tests were rerun.
- The final candidate passes 275/275 focused App cases, with no failures, skips
  or aborts: approval identity/cancellation/disposal, file editing/undo, MainWindow
  lifetime, tool geometry/overflow, shared appearance boundaries and MCP recovery.
  The new nested footer required updating its old two-sibling structural
  expectation; the replacement checks all three commands and their distinct
  columns/request parameters. All 275 cases were rerun, not only that assertion.
- The external production MainWindow driver passes all 24 combinations of
  light/dark, 1280x820/526x601 and success/deny/conflict/cancel/read-only/path
  escape. It types and sends, operates approval/content controls, reviews stored
  changes, cancels then confirms undo, and interacts with restored history.
  Real SQLite, the task runner, loopback model HTTP and the shared executor are
  used. Files, outside sentinels, model request counts, tool results and audits
  are checked; reopening causes no model or tool replay.
- Successful review/undo has five distinct successful audits: read, edit,
  change statistics, snapshot review and restore. The first external helper
  incorrectly expected three and was corrected after inspecting all identities;
  no production audit was suppressed or removed.
- Full solution builds use a separate ignored artifacts directory, with zero
  warnings/errors, and do not overwrite the baseline run's outputs. Eleven
  changed C# files pass Roslyn one-type/file and size checks: at most 473 file
  lines and 72 method lines. Changed AXAML remains below 500 lines.

Reports and before/after frames are under
`%TEMP%/MossAgent-ui-polish-20261016`. The final 24-case interaction report is
`%TEMP%/MossAgent-file-ui-20261016/run-7246991f36904c7b9acc0cbb93c23b1f/result.json`.
That final run also compares the complete audit-call identities before and after
restoration, in addition to unchanged model request counts. The App DLLs in the
build, both test outputs and the external driver have one identical SHA-256.
The source driver and its captures remain outside the repository.

These are independent Skia/Headless frames and real production-composition
interactions, not native Windows input or fresh captures of Codex. No existing
Codex/MossAgent desktop window was operated, restarted or captured. Seeded
project/provider configuration is not acceptance of those editors. Full current
application regression beyond the focused cases, all native DPI/state combinations and same-state Codex
pixel equivalence remain separate gates. No commit, push or release is made.
