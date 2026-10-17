# MCP settings accessibility and disconnected recovery

## Scope and design review

- App owns field labels and status severity. Nine editor fields now share their
  visible localized labels with persistent automation names. Names do not depend
  on values, placeholders or the selected transport. Existing shared wrappers,
  dialog layout, semantic error color and header reflow remain the visual system.
- `McpSettingsViewModel.HasStatusError` explicitly distinguishes failed reads,
  validation, mutations and connections from loading, cancellation and success.
  Both the page header and dialog footer use the same shared error variant.
  Opening a new editor or recovering a connection clears the old error severity.
  No behavior is inferred from translated status text.
- Existing repository, manager, provider, approval, audit and tool contracts are
  unchanged. UI code gains no file, HTTP, process or SQL access. No migration,
  packaging, AOT or JSON-contract changes are made.
- Reconnect retains the existing cancellation/lifetime and connection deadlines.
  It refreshes registration; it never retries a failed tool side effect. Unknown
  MCP tools still require the standard side-effect policy and approval pipeline.
  Cancellation may remove old registrations; retaining the configuration list
  does not imply those connections remain usable.
- Each changed handwritten C# file contains one type and remains below 500 lines;
  changed methods remain below 80 lines. Helpers are separately named files.

## Reproduced failures

- Before adding the automation names, all four light/dark × stdio/HTTP form
  checks fail to find named editor fields. All four pass after the label change.
- Before binding explicit severity, the four real HTTP failure/recovery UI cases
  reach the correct safe connection failure, but the page header still presents
  it as an ordinary status. The other eight workflow cases pass. The correction
  uses the existing error style, not local colors, dimensions or shadows.

## Production-composition workflow

`McpUiRecoveryTests` runs the production `MainWindow`, settings, workspace, Agent,
HTTP provider, MCP client, tool executor and SQLite repositories in Avalonia
Headless. The loopback model and MCP server are deterministic test-owned services;
no paid model, real credentials or user's settings are accessed.

Light and dark each cover both 1280×820 and 526×601:

1. Activate Add, type the server name, choose HTTP with the keyboard, enter the
   endpoint and headers, and activate Save and connect. The actual client
   initializes, discovers and registers the tool with its persisted identity.
2. Make the endpoint unavailable, type in the composer and activate Send. The
   tool request waits for the real approval UI; no tool HTTP call occurs before
   approval. Approving produces one failed HTTP call and a safe persisted result.
3. Activate Reconnect while offline. The old tool disappears from registration,
   safe error feedback is visible, and neither tool nor model is retried.
4. Restore the endpoint and activate Reconnect. The same tool identity returns,
   the error presentation clears, and there is still no automatic tool/model call.
5. Explicitly create and send a new task, approve it, and receive the real MCP
   echo. Each full recovery scenario has four model requests and two tool HTTP
   attempts: one expected failure and one success. Header/session configuration,
   both completed audits and persisted result/error state are checked.
6. Expand the completed turn and tool row using keyboard actions. Verify the
   actual selectable output and Markdown reply are on screen and hit-testable.
   Recreate the service composition and window, restore the selected task, repeat
   these interactions and assert no further tool or model request.

Four additional rejection cases cover actual Deny and ReadOnly policy in both
themes, split across the two sizes: no tool HTTP call is made. The persisted audit
and conversation retain `approval_denied` or `capability_denied` as appropriate.
Four cancellation cases hold the real HTTP initialization, activate Cancel,
release the late response, verify it cannot register a tool, then reconnect
successfully. Five focused status tests cover initial read recovery, invalid
input, saved-but-disconnected retry, disabling an unreachable server and neutral
cancellation. Four form tests cover all nine accessible names.

All failures are checked for absence of a synthetic secret marker in UI status,
persisted tool/audit records and subsequent model input. HTTP failure bodies are
not used as user-facing error details.

## Rendering and test-harness limits

Optional evidence is written outside the repository using
`MOSSAGENT_MCP_RECOVERY_QA_ROOT`. Editor, offline, reconnected, expanded output,
completed reply and restored frames cover both themes and sizes. These are
Headless/Skia renders at the default scale, not desktop captures or Codex comparisons.

The initial output check omitted a render pulse and hit an old visual. A later
restored compact-window check still hit the old scroll presenter even though a
captured frame already showed the correct control. Diagnostic output confirms
the same coordinate hits the control after capture. The test now synchronizes
against a captured current render before hit testing; it retains the original
on-screen and actual-hit assertions. No production scrolling change is inferred
from these harness failures, and failed reports are retained.

## Final verification: 2026-10-16

- The final related App run passes 266 cases, including all 21 new tests and
  existing MCP workflow, catalog, header and shared-component guards. The
  complete pixel suite passes 282, MCP protocol/tool tests 56 and architecture
  tests 7: 611 distinct cases, no failures, skips or aborted tests. Earlier
  focused runs and red baselines are not added again to that count.
- The solution build completes with zero warnings and zero errors. Scoped
  whitespace formatting, `git diff --check` and nine-file AST size/type/method
  audits pass. Production App and both test output copies have the same App DLL
  hash; the recorded App and App-test binaries remain unchanged during the final
  no-build test runs.
- Twenty-four final Headless frames cover six states across both themes and
  sizes. Compact error/recovery, editor and restored-output frames are visually
  reviewed. Final reports are `app-final.trx`, `pixels-final.trx`, `mcp-final.trx`
  and `architecture-final.trx`; frames are in `frames-final`. Original red
  reports and harness diagnostics remain beside them.
- This is a focused regression/build checkpoint, not a new complete solution
  test run or whole-product sign-off. Unrelated existing work is preserved; no
  staging, commit, push, publish or existing desktop-window action is performed.

## Remaining acceptance

- This is explicit HTTP reconnect, not automatic transport resumption, independent
  GET SSE support, arbitrary server requests or automatic side-effect replay.
- Provider/model configuration is seeded through repositories here; its editor
  workflow is covered separately. A scripted local model does not establish live
  model reasoning quality or arbitrary third-party MCP behavior.
- Fresh-composition history restoration is tested, not native desktop startup,
  every startup handshake race, process crash recovery or every stdio scenario.
- Native window interactions, assistive-technology announcements, fractional DPI,
  same-state Codex comparison and whole-product visual acceptance remain open.
  Existing user desktop windows are neither operated nor captured by this slice.

Evidence root: `%TEMP%/MossAgent-mcp-recovery-20261016`.
