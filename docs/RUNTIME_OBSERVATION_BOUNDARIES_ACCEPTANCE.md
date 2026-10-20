# Terminal payload and subagent UI observation boundaries

Status: five explicit test files integrated after complete original regression
93473 ended: 8,136 passed / 2 failed, 8,138 total, no skipped/aborted tests. Eight
reports, 2,414 source/configuration files and 2,035 runtime files have matching
before/after paths and hashes. Its terminal payload sharing violation and subagent
collection-enumeration failure remain preserved. The original freeze has ended.

## Terminal payload completion

PtyNativeLifecycleTests waits for executed.txt to exist, then immediately reads
it. Creation precedes the shell's WriteAllText close. A controlled exclusive
writer reconstructs those same two reader statements and fails with the same
IOException while the writer owns the file. The original full-run report does
not identify the exact locking process, so this is boundary evidence rather than
a claim about every historical sharing violation.

The test-only candidate adds a completion marker after the real shell's payload
write returns. Its reader awaits that marker, then checks the unchanged exact
Unicode/retained bytes. A new locked-writer negative case requires the read to
remain pending, then verifies exact content after close/publication. It adds no
IOException retries or extra arbitrary sleep. Real process restart, session
identity, child reaping and independent-task assertions remain intact.

file-baseline.log/TRX under artifacts/runtime-observation-boundaries-20261019
retain the intentional controlled failure. The first candidate Windows suite
passes 147 checks but lacked the already-integrated PID-publication test/readiness
lines in its older independent copy. Those were copied unchanged from the frozen
original; the synchronized candidate passes 148 checks including both publication
boundaries. That earlier 147 result is not described as the current full suite.

## Subagent observation on the UI thread

The original failure is Collection was modified in the fixture's Messages.Any
predicate while real streamed progress changes the observable UI collection.
Without an Avalonia application, presentation uses its existing direct path;
the plain xUnit polling test can enumerate on a different worker thread.

The candidate moves all five lifetime cases into the existing awaited Headless
session and nonparallel visual collection. Reads and presentation execute on the
UI dispatcher; explicit access assertions guard the partial-dialogue predicate
and the presentation-failure callback. No production locks, exception-swallowing
poll retries or provider behavior changes are introduced. Existing real local
HTTP/child disconnection, selected parent/child models, reasoning, cancellation,
partial-dialogue recovery, safe errors and completed audits remain asserted.

Initial Debug/Release relevant App selections each pass 18 checks; the expanded
Subagent/Headless selections each pass 164, with complete TRX reports. These are
Headless dispatcher plus simulated-provider checks, separate from native UI or
actual provider acceptance. The changed scheduling addresses the identified
unsafe observation; it does not prove that all historical failures share a cause.

## Ownership and further verification

Only five App.Tests/Platform.Windows.Tests files are candidates; production module
dependencies, ports, authorization, approval, process behavior and SQLite data are
unchanged. Reuse existing fixture deadlines and cancellation. Each C# file has one
type and stays under file/method limits. DESIGN.md and baseline-manifest.json
record required self-checks and original identities. Candidate Debug/Release
solution builds have zero warnings/errors. A Roslyn audit covers the five
candidate files and six newly authored browser probe files: eleven single-type
files, no file/method violations, maximum method length 51 lines. The prepared
five-file integration script defaults to an identity-only plan and refuses
execution without the original regression's reviewed terminal reports and
unchanged source/runtime identities. That gate has passed; all five explicit
files were copied with zero baseline conflicts and matching candidate hashes.
Original combined verification is tracked with the later production audit-recovery
slice in `AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md`.
Command filesystem
isolation remains an open safety blocker; this is not product completion.
