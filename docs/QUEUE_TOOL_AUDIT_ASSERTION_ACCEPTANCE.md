# Queued write audit identity assertion

Status: integrated into the original checkout after complete regression 55845
terminated and its source/runtime identities were checked. That failed run reports
a failure in
QueuedWritesUseTheirSavedPolicyAndPublishRealResultsAndAudits(AskEveryTime, true):
its unfiltered Assert.Single sees the queued write and an in-flight background
filesystem.change_statistics audit. This does not establish a duplicate write.

## Deterministic evidence and change

The independent copy under artifacts/queue-runtime-workspace-20261019 adds an
explicit wait until the real production background statistics audit appears,
then preserves the old Assert.Single. This deterministically fails the allowed
write case with exactly those two audits; both refused-write cases pass.
The unchanged original and instrumented baseline source snapshots, TRX and log
are retained under artifacts/queue-audit-statistics-20261019/direct-baseline.

The candidate keeps the wait but now awaits a completed statistics audit. It
requires exactly two audits for a successful write, exactly one for either
refusal, exactly one filesystem.write_file, exact task identity for every audit,
and only a successful completed read-only statistics call with the expected call
prefix/policy/approval result as the other audit. Existing queued-write CallId,
saved policy, approval allow/refuse, error, actual file bytes, visible tool result,
persisted history, draft and Queue assertions remain unchanged. No filtering
hides an unknown extra tool, duplicate write, unfinished statistics or task leak.

The first candidate's five boundary cases pass. The tightened final candidate's
35 Queue cases pass in both Debug and Release with no failures/skips. These are
two configuration runs of overlapping cases, not seventy distinct scenarios.
No production tool, approval policy, timeout, output, persistence or audit code
changes. The seven-file guarded integration has zero baseline conflicts. Original
Debug/Release full builds and Browser suites pass; the related App selection's
Release 212 checks pass. Its first Debug run has 211 passes and one separate Queue
snapshot completion failure; the tightened audit cases pass in both configurations.
The original full run remains 8,121 pass/one failure, 8,122 total, no skips or
aborts, with all 2,411 source/configuration and 2,035 runtime paths/hashes unchanged.
See BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md for integration/runtime evidence
and QUEUE_SNAPSHOT_COMPLETION_ACCEPTANCE.md for the separate assertion boundary.

## Required self-check

App.Tests owns these integration assertions; module dependencies and production
ports are unchanged. Production DI, ToolExecutor, approvals and SQLite remain in
use, with the fixture's existing deadline and cancellation. No native shell or
browser is involved. The single modified C# type remains in one file below 500
lines; all methods must remain below 80 lines. Success plus ReadOnly/approval
denial, queued approval cancellation and incomplete-call refusal remain covered.

This identifies the direct assertion error, not every historical failure or the
specific timing of the original interleaving. No passing full regression is
claimed; the original failed run and all earlier failed records remain intact.
