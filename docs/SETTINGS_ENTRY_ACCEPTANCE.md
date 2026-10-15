# Settings entry and navigation-rail tooltip acceptance

## Scope and design review

App owns this presentation/input change. The shared navigation-rail appearance
places tooltips to the right with an eight-DIP gap and zero vertical offset;
individual views do not override tooltip geometry. MainWindow binds Ctrl+Comma
to the existing idempotent OpenSettingsCommand, not ToggleSettingsCommand. Help
reuses the localized navigation-rail Settings label. No domain, port, execution
pipeline, approval policy, storage, provider, packaging or JSON behavior changes.

Navigation does not authorize a tool or submit a draft. Existing modal-window
input routing remains authoritative. Deterministic tests retain pending approval
identity, its reachable decisions and the embedded-browser presentation block.
No new IO, timeout, cancellation or output mechanism is introduced. All changed
handwritten source files remain below the hard size limits and contain one type.

## Reproduction and correction

Evidence and Python scripts are outside the repository at
`%TEMP%/MossAgent-rail-navigation-20261015`. The baseline is the previously built
App hash `3AF85B79BD10B41F3BF13926A96AEA18D2DE6A381A15FEC486AFFD870225FD07`.
In its maximized 1920x1032 workspace, the settings button spans x=8..44 and
y=988..1024. Default pointer placement constrains its tooltip to
(25,997)..(67,1027), covering the center (26,1006). A center click does not open
settings; clicking the uncovered button edge at (15,1006) does. The command
binding itself is therefore reachable, rather than missing. The old App also
has no Ctrl+Comma binding.

Final App hash is
`19564864E0806BD8CD4B4EDFF64E34D493808BF6715276CA9EE4ED2006368458`.
Native captures put the tooltip at (52,991)..(94,1021), leaving eight pixels
between it and the button. Seven captured tooltip states prove the same clearance,
including 526x601 light/dark windows. Center clicks open and close settings;
the settings Home action returns to the conversation. Ctrl+Comma opens settings,
and a repeated chord leaves it open. The real help flyout displays Ctrl+, and
Escape dismisses it. Eight DIP is a non-overlap correction, not a measured claim
about current Codex tooltip placement.

## Native evidence and boundaries

The ordinary-permission final process produces fourteen passive Python captures,
using PID/executable/hash-checked PrintWindow frames plus only its owned popups.
They cover 1920x1032, 1280x820 and 526x601 at 96 DPI, both themes, pointer page
transitions, shortcut opening/repetition, help dismissal and restored maximization.
Both baseline and final processes exit normally with code zero and empty stderr.
Both audits retain nineteen identical table hashes, zero new artifacts and
byte-identical restored Light/default-font preferences. Only synthetic copied
data is used; no provider request is intentionally submitted or security setting
changed. Approval and modal boundaries are deterministic tests, not new native
approval decisions.

`rail-tooltip-comparison.png` contains unscaled before/after workspace crops.
Title, visual-style heading, palette labels and lower palette rows remain
pixel-identical to the baseline settings capture. The entire palette crop is
not identical: 460 changed pixels occupy the previous theme-dropdown focus
outline, at (1282,338)..(1458,366). That interaction-state difference is recorded,
not hidden by a whole-palette equality claim.

Fresh passive Codex remains version 26.1007.9446 in a 481x601 conversation window,
not an equivalent Appearance state. It is not substituted for the saved full-size
reference. Current-version same-state pixel fidelity, other menus and surfaces,
native DPI transitions and overall product acceptance remain open. The preceding
typography acceptance report remains historical evidence, not a claim that all
its residual differences are resolved here.

## Test evidence

New cases cover every visible rail tooltip and center-click navigation in both
themes and ordinary/compact layouts; actual Ctrl+Comma key events, unmodified
Comma, idempotence, retained selected page/search/draft, modal owner isolation,
and pending-approval identity. A first baseline run correctly fails eight rail
and four shortcut cases. Two additional failures were an invalid test assumption
that a modal owner changes its IsEnabled property; the test instead verifies the
actual owner and modal input route. A later focused run passes 78/80: two compact
shortcut tests incorrectly look for a search field in a closed drawer. Opening
that drawer before testing its focused input corrects the fixture, not production
behavior. All six shortcut cases then pass on the final test assembly.

The final complete solution rerun executes 6,115 cases: 6,111 pass and four fail,
with no skipped or aborted cases. All eighty cases in the related focused subset
pass within this complete run, including all sixteen new cases. App passes
4492/4493; visual 84, Agent 119, Browser 76, Infrastructure 199, Providers 516
and Tools 491 all pass. Windows passes 134/137. The four failures are:

- PtyTerminalResizeOwnershipTests: waiting for native resize times out.
- ShellProcessOwnershipTests: intermediate child cancellation binding raises
  TaskCanceledException.
- McpStdioManagerLifecycleTests: reload initially observes no expected tool.
- AuthenticatedProviderProxyRuntimeTests: Gemini/SOCKS5 rejected-credential case
  observes fewer than two proxy rejection-counter increments at its final
  assertion. This is not an observed successful authentication or direct fallback.

The three Windows failure paths pass a separate five-case isolated rerun; all
twelve rejected-credential protocol/proxy cases also pass an isolated rerun. No
implementation, timeout, security assertion, exclusion or allocation threshold
is changed for these unrelated failures. Isolated results are not added to the
6,115 complete-run total or used to reclassify it as all-green. The preceding
typography run remains historical at 6096/6099; its allocation case passes in
this new full run, but is not claimed fixed by the settings-entry change.

Final solution build has zero warnings/errors. Scoped whitespace formatting,
source size/type checks, source/assembly/native-hash verification and diff checks
pass. Screenshots/scripts are not tracked. Nothing is staged, committed, pushed,
released or installed; the full product goal remains active.
