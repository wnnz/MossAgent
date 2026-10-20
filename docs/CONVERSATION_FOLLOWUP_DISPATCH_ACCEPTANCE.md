# Queue configuration checks at atomic dispatch

Status: seventeen explicit paths have been merged with zero baseline conflicts;
original-checkout integration verification passes. Complete Debug regression
55845 is terminal: 8,121 pass and one queued-write audit assertion failure,
8,122 total, no skips or aborts. Its earlier complete
regression (session 97039) is terminal and
its freeze has ended: 8,101 passed, one allocation assertion failed, no skips,
8,102 total; 2,404 source/configuration and 2,035 runtime identities unchanged.
The provider completion slice is already integrated and recorded separately.

## Confirmed race

Two deterministic production-App-DI/local-HTTP cases change project authorization
or disable the selected model while Queue attachment hydration is held. Both old
paths still issue a second request. The direct-baseline assertions fail with two
requests rather than one; no outside file, paid provider or existing window is
used. Earlier timeout-only tests are preserved but are weaker evidence.

## Candidate behavior and ownership

The existing Application persistence port now accepts PreparedConversationFollowup.
Infrastructure checks its current project primary/roots, provider runtime settings,
model identity/availability/media requirements and agent availability inside the
same short SQLite transaction that consumes the entry and commits the user
message/Running status. A mismatch fails before changing the queue, draft or task.
The check contains no HTTP, filesystem, worktree process or other long-running I/O.
Attachment/worktree preparation remains outside the transaction under existing
cancellation and deadlines. App delegates through the port with no new SQL or
infrastructure dependency. There is no migration or reinterpretation of old data.

Saved queued model parameters, agent instructions and approval policy stay fixed;
display/default parameter edits do not replace them. Provider credential/header
comparisons use bound SQL parameters and are not logged or placed in Queue data.
This grants no new root, approval, browser, process or network permission.

The required design self-check and fifteen candidate paths are under
artifacts/queue-runtime-workspace-20261019/artifacts/followup-dispatch-races.
Its initial Debug build and 49 App/47 storage selection passed. The final cases
also change configuration immediately before claim, then restore it and manually
continue once without duplicating the message or clearing the next draft. Final
Debug/Release builds passed with zero warnings/errors; both configurations pass
51 App and all 723 Infrastructure checks. Fifteen integration source paths and
1,270 Debug/Release runtime paths/hashes match before/after. Structural checks cover
15 single-type C# files and 62 methods/accessors, maximum 55 lines, no violations.

A later test-only addition uses production Queue/HTTP/tool approval and real
Windows PowerShell parent/child processes in generated authorized directories.
Its initial case passes: cancelling one task stops its own tree and pauses its
remaining entry, while a second task's tree stays alive, completes after its own
release and retains its draft. AskEveryTime remains active and both tool requests
receive explicit test decisions. The final 17-file candidate bounds all fixture
processes and checks approval readiness. Its Debug/Release solution builds pass
with zero warnings/errors and both configurations pass 52 App and 723 Infrastructure
checks. All 17 source paths and 1,270 runtime paths/hashes were unchanged during
validation, recorded under artifacts/followup-dispatch-final. No existing window or
user process is targeted; this does not prove filesystem confinement.

The guarded integration manifest, original-file backups, builds and checks are
under artifacts/followup-dispatch-integration-20261019. Original-checkout Debug/
Release builds pass with zero warnings/errors; both configurations pass App 52
and all 723 Infrastructure checks. The rebuilt original-assembly CLI harness also
passes all four termination/recovery cases. All eight validation stages are terminal.
The full
workspace structural check covers 571 changed/untracked C# files, 29 AXAML files
and 3,119 methods/accessors, maximum 78 lines, with no violations; whitespace passes.
The isolated seventeen-path check covers 71 methods, maximum 59 lines.

All eight session 55845 reports and 2,411 source/configuration plus 2,035 Debug
runtime paths/hashes were checked unchanged; its freeze ended before later
guarded integration. The failed assertion and exact audit-identity correction are
recorded in QUEUE_TOOL_AUDIT_ASSERTION_ACCEPTANCE.md. No passing full run is claimed.

## Separate allocation-test change

The original full run reports 3,480 bytes in the raw-counter task-lookup test.
Its specific cause is not established. The candidate uses the repository's existing
nonparallel AllocationMeasurement helper, with the same warmup/measured iterations,
identity assertions and exact zero-byte requirement. Real-allocation and invalid-
measurement controls remain required. No production lookup or GC setting changes.
See ALLOCATION_MEASUREMENT_ACCEPTANCE.md for the earlier independent counter probe;
that probe does not prove this particular failure has the same cause.

## Remaining limits

This is not a complete App regression, native GUI or real-provider acceptance.
The original run's failure remains preserved. Source-review grant/worktree changes
at claim and changes after a committed run starts remain separate boundaries;
source-review execution still uses its existing runtime revalidation. No broader
atomicity or continuous revocation guarantee is claimed. Command filesystem
isolation, Queue browser/process ownership, Steer and other matrix gaps remain
open; this candidate cannot complete the overall product goal.
