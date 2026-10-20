# Allocation measurement isolation

## Scope and evidence

The ordinary frozen `workspace-regression-20261018` full run reported a 736-byte
delta in `WorkspaceCatalogLookupTests` despite its unchanged zero-byte assertion.
An earlier full run reported 3,552 bytes in `ProviderCatalogLookupTests`. Neither
report is erased by focused passes, and their individual causes are not proven.

A package-free local probe under ignored
`artifacts/gc-allocation-counter-20261018` independently reproduces nonzero
`GC.GetAllocatedBytesForCurrentThread` deltas around a no-inline method containing
only two counter reads and `Thread.SpinWait`. Its `default.json` records:

| Control on .NET 10.0.12 / Windows X64 workstation GC | Result |
| --- | --- |
| Ordinary empty intervals | 32/32 zero |
| Explicit retained `new byte[37]` | 32/32 exactly 64 bytes |
| Empty intervals while a bounded worker requests background GC | 11/192 nonzero |
| Empty intervals in a short no-GC region after the worker stops | 32/32 zero |
| Explicit allocations in the same protected control | 32/32 exactly 64 bytes |

The probe reports `InvalidControls=false`. Exit 1 denotes reproduction of the
unexpected empty-interval delta, not a successful ordinary test run. It does not
identify the cause of every historical test failure.

Public context checked on 2026-10-18:
[dotnet/runtime#134724](https://github.com/dotnet/runtime/issues/134724) and
[the proposed accounting fix](https://github.com/dotnet/runtime/pull/134855).
The PR remains open and unmerged; its description is not proof that the local
runtime is patched or that every application-test delta has this cause.

## Test-only implementation

App.Tests owns `AllocationMeasurement` and its nonparallel xUnit collection.
This changes no production runtime configuration, GC mode, tool policy, model
selection, persistence or dependency direction, and needs no new application port.

- Callers perform setup and warmup before passing a bounded synchronous action.
- `Measure` reserves a 16 MiB no-GC region, reads the current-thread counter,
  executes the action once and returns its actual delta.
- `finally` ends the region. Failure to start or end it fails the measurement.
- There is no retry, minimum selection, byte subtraction, allowance or skip.
- A dedicated collection disables concurrent test collections in this process;
  it does not change product behavior or other test executables.
- The two catalog test families retain all functional checks, 1,000 warmup
  iterations, 10,000 measured iterations and exact zero-byte assertions.
- Other allocation tests are not silently rewritten or declared fixed.

Six focused controls cover an empty action, real retained allocation, propagation
of an operation failure, rejection of nested measurement, null input and an
explicit collection invalidating the measurement. Release checks prove a later
measurement can start after the successful and rejected paths. A real allocation
must still produce a positive delta; invalid accounting cannot become a pass.

Each new file declares one type and stays below the repository size limits.
Actions are synchronous; this helper is not for async work, long-lived operations
or code intentionally forcing GC.

## Validation

- `allocation-isolation-20261018/results/focused`: **28/28 passed**, no skips.
- The same candidate's `results/expanded`: **113/113 passed**, no skips; includes
  model menus/reloads, workspace loading, draft/background persistence, settings
  navigation, real authorization tools and both catalog lookup families.
- That candidate's solution build completed with **0 warnings and 0 errors**.
- The subsequent production snapshot-sharing change is a different candidate;
  its own **113/113** expanded App cases pass with no skips. Its validation is
  recorded in `FILE_SNAPSHOT_SHARING_ACCEPTANCE.md`.

Results are under ignored `artifacts/`. Focused checks are not a green complete
App regression. The old frozen full run remains separate and must be inspected
through its own terminal results. No native UI, real provider, release, commit,
push, AOT, trimming or memory update is claimed.

The frozen ordinary run has since ended with App **5,021 passed / 2 failed /
5,023 executed**, no skips or abort. Its 736-byte allocation assertion remains
one of the two failures. The newer no-GC helper is not present in those frozen
binaries. Session 95889 tests the later thumbnail candidate that includes this
helper; its focused passes are still not a complete-run result.

That newer full run now reports **1,176 bytes** in
`SidebarFilterAllocationTests.WarmUnchangedFilters_DoNotAllocateOrReplaceRows`
for recent/no-matches. This is a different family still measuring with raw
counter reads; no production or test change has been made to its frozen binary.
The newer `history-thumbnails-20261018` source also routes the synchronous
sidebar-filter family through the same helper and nonparallel collection.
All six pinned/recent/project and match/no-match cases retain 100 warmups,
10,000 measured filters, row identity/selection/notification assertions and
the exact zero-byte expectation. No production filter code changed.
The six sidebar cases and six helper controls pass in its 31-case focused run
and 409-case expanded App run;
that does not establish the exact cause of the frozen full-run failure.
The shared helper now covers three families. The other raw-counter tests still
need an explicit boundary/collection audit, not blanket attribution to the
runtime issue or relaxation of zero-byte expectations. In particular, do not
convert async work into an async-void Action or subtract thread-local counters
across an await.

The later provider-stream-boundaries-20261019 complete run (session 97039) reports
3,480 bytes in WorkspaceTaskLookupOptimizationTests.WarmTaskLookup_DoesNotAllocate
for missing=false. That separate raw-counter family was not changed in the frozen
run. Its exact cause remains unconfirmed. An independent dispatch-race candidate
routes it through the existing helper/nonparallel collection while retaining
1,000 warmups, 10,000 measured lookups, reference checks and the exact zero-byte
assertion. Candidate controls and Debug/Release validation are recorded in
CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md; they do not erase the original failure
or establish a production lookup defect/fix. This test-only change is now merged:
both original-checkout configurations pass the 52-case App selection, including
the task-lookup family and helper controls. Complete session 55845 is terminal:
8,121 pass and one separate queued-write audit assertion failure, 8,122 total,
no skips or aborts, unchanged source/runtime identities. The allocation checks
pass in that run; the exact zero-byte assertion was not relaxed. This does not
identify the historical raw-counter failure's cause or establish a product fix.
