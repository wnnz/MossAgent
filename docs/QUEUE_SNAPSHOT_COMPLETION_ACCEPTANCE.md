# Queue snapshot completion observation

The first original Debug browser/Queue selection after browser cancellation
integration passes 211 checks and fails one enabled-agent snapshot assertion:
expected two requests, observed one. The original Release selection passes 212.
Both reports remain under artifacts/browser-timeout-runtime-20261019; neither
result substitutes for the other or for a complete regression.

## Boundary and deterministic evidence

Workspace IsRunning includes active preparation. However, the predecessor is
removed from the tracker before its RunFinished subscribers hand off to Queue.
An observation between those actions can see IsRunning=false with one completed
request while the queued input is still saved. An existing public completion
event observer is now held before the Queue subscriber using a test-only fixture
configuration callback. With the old wait, both branches fail deterministically:
enabled expects two requests but sees one; disabled expects a refusal notice but
sees none. See observer-baseline.log/TRX under
artifacts/queue-snapshot-completion-20261019.

An earlier attachment-preparation gate was too late to reproduce this observation:
preparation already counts as IsRunning. Its timeout remains recorded separately,
not presented as the same failure. The first corrected candidate also asserted a
visible Queue row during the catalog's asynchronous refresh and saw an empty
projection. That assertion now inspects the exact persisted pending entry at the
held handoff; final outcomes still inspect the visible Queue.

## Change and required self-check

Only App.Tests changes. The fixture's optional callback uses the existing
WorkspaceRunController event and leaves production composition, dependencies and
behavior unchanged. The held boundary proves the predecessor is absent, exactly
one request exists and the exact queued entry remains persisted, then always
releases the observer. Completion waits require an idle run/Queue and a visible
failure or a subsequent request. The attachment tests use the same terminal
outcome rule. No delay increase, alternate model, approval relaxation or retry
masks a failure.

All chosen model/reasoning, immutable saved agent prompt, disabled-agent rejection,
attachment integrity, request counts and UI assertions remain. Enabled dispatch
also requires an empty visible Queue and exactly one committed user message;
disabled dispatch must retain the entry and persist no dispatched message.
Existing deadlines/cancellation, production SQLite and simulated loopback HTTP
remain in use. One type per file and file/method limits remain required. No tool,
provider, authorization, migration, network or native process behavior changes.

## Verification and limits

The independent four-case snapshot selection passes; Debug and Release expanded
browser/Queue selections each pass 212 checks. The two original files were merged
only after their hashes matched saved baselines, with candidate hashes checked
after copying. integration-manifest.json and merged-files.json retain identities.
Original-checkout Debug/Release complete builds pass with zero warnings/errors,
and both configurations' expanded selections pass 212 checks with no failures or
skips. Structure/whitespace checks cover 577 C# files, 29 AXAML files and 3,184
methods/accessors, maximum 78 lines, with no violations. The new complete Debug
regression session 93473 under artifacts/browser-cancellation-integrated-20261019
has ended: 8,136 passed / 2 failed, 8,138 total, no skipped/aborted tests. Eight
reports and 2,414 source/configuration plus 2,035 runtime identities match; its
freeze has ended. The two observation failures and their guarded test-only fix
are retained in RUNTIME_OBSERVATION_BOUNDARIES_ACCEPTANCE.md.

This identifies an insufficient completion observation, not every historical
test failure or a product dispatch fix. The held public event represents a
controlled handoff schedule; it does not prove the exact timing of the original
failure. Native GUI, real AI providers, a new complete regression and the command
filesystem isolation blocker remain separate acceptance work.
