# Repository status read ownership

## Scope and verified defect

The original `WorkspaceRepositoryStatusViewModel` starts metadata reads with
`CancellationToken.None` and has no disposal contract. Workspace selection can
launch an unawaited refresh, and neither workspace shutdown nor service-provider
disposal joins that operation. A controlled noncooperative Git port proves that
disposal can finish while its reads remain active; late reads can still publish.

The fix remains in App and reuses `IGitRepositoryInfoService`, the Infrastructure
Git runner and `SettingsOperationLifetime`. It does not run processes or access
files in UI, add a service locator/cache, or change tool authorization/approval.
Each read has the existing 30-second operation deadline. Cancellation does not
abandon ownership: closing waits for the port to return, including cleanup of
native children already owned by Infrastructure.

## Behavior

- Every read is reserved before invoking the port and released only after its
  cancellation source and completion are settled. Shutdown/disposal rejects new
  work, cancels all pending reads and shares one stable completion across retries.
- Generation and shutdown checks reject stale results and a reentrant selection
  or close before dispatch. New selections show their own directory and unknown
  Git status while pending, instead of retaining another project's branch.
- Service failures remain unknown and can recover on a later refresh; raw
  exception text is never displayed. Successful null branches still mean non-Git,
  and a selected task's worktree still takes precedence over the project root.
- Workspace synchronous disposal, asynchronous disposal and normal shutdown all
  connect to the owner. Shutdown starts metadata cancellation alongside existing
  cleanup and joins it even when another cleanup fails. The four existing labels
  move to localized workspace resources without changing their Chinese values.
- Git runner, branch lookup and owned-read waits do not capture the caller's UI
  synchronization context. Final status publication explicitly dispatches to the
  UI with the operation token; close can cancel a queued publication without
  needing a stopped message pump to resume a library continuation.

## Deterministic verification

- The first ten regression cases on the original implementation pass 2/10 and
  fail 8/10: uncancelled tokens, premature disposal, missing ownership and unsafe
  error propagation are retained in `lifetime-red.trx`.
- Final coverage adds callback-failure ownership, three publication/reentrancy
  cases and two real Workspace composition cases: 16 new regressions. Together
  with operation-lifetime, conversation, shutdown and preference-lifetime tests,
  the ownership-focused group passes 36/36. No assertion, cleanup delay or allocation
  threshold is weakened. An intermediate test build catches a missing xUnit test
  cancellation token; the final project-creation call passes the active test token.
- Three additional Infrastructure regressions prove symbolic lookup, detached
  HEAD fallback and cancellation complete under a caller context that does not
  pump queued callbacks. The Git-focused group passes 24/24. After the context
  repair, the App-focused group passes 36/36, including all 16 ownership cases,
  the eight layout cases involved in the stalled run and adjacent lifetime tests.
- Owned noncooperative reads deliberately ignore cancellation until the fixture
  releases them. Close stays pending, all their tokens are cancelled and neither
  old labels nor new reads can be published. Another owner's token stays usable.
- All eleven changed/new C# files have exactly one type, no syntax errors and stay
  below hard limits. The status owner is 146 lines/27-line longest body; the
  existing Workspace remains 489 lines/57-line longest body. Resource keys have
  no duplicates. Scoped whitespace verification passes; solution build has zero
  warnings/errors.

## Full-run diagnosis and final verification

The first App full run stalls during teardown of
`FileChangeSummaryLayoutTests.Summary_UsesBoundedRowsSharedGeometryAndExplicitDisabledUndo`.
The process remains alive. Thread stacks and read-only ClrMD task-chain evidence
show the headless consumer waiting on an empty queue while the Git runner has
finished and its callers await the stopped UI synchronization context. This is
an independently verified wait-chain defect, not a timeout inferred from silence.
Only after recording the evidence and checking the exact test process identity
is that run terminated. Its `full.trx` contains just 2,478 results, with 2,477
passed and one failed; it is incomplete and is not counted as a full pass.

That recorded failure is the unchanged
`ProviderCatalogLookupTests.WarmProviderCommandAndAvailabilityChecks_DoNotAllocate(missing: false)`:
5,856 allocated bytes against the existing zero-byte assertion. The same case
passes in the later focused and final runs. Neither production lookup code nor
the allocation assertion is changed, and the intermittent allocation cause is
not claimed to be repaired.

After the wait-chain repair and final build, one complete run of all eight test
assemblies produces the following parsed `final.trx` results:

| Assembly | Passed / total |
| --- | --- |
| Agent | 112 / 112 |
| App | 3,894 / 3,894 |
| App.Visual | 8 / 8 |
| Tools | 483 / 483 |
| Platform.Windows | 137 / 137 |
| Browser | 76 / 76 |
| Infrastructure | 199 / 199 |
| Providers | 516 / 516 |
| **Total** | **5,425 / 5,425** |

All eight summaries are completed, their result counts match, and there are zero
failed or not-executed cases. This final success does not erase the historical
incomplete run or establish a fix for earlier intermittent allocation/deletion
failures. No test-run restart is used as a substitute for the wait-chain repair.

## Prior directory-delete failures and limits

The earlier complete App runs failed at temporary-root deletion in
`WorkspaceConversationFlowTests`, not at a model/result assertion. This turn's
unmodified native-flow baseline is replayed twenty times: all 40 cases pass.
A read-only 90-second observer sees 194 Git process IDs under 38 synthetic test
directories; it records only PID, authorized temporary cwd and relative timing,
not command lines, environment, credentials or user-project data.

These observations establish that real Git metadata processes participate, but
do not identify the process holding a directory at an earlier failed deletion.
The ownership defect is independently proved and fixed; it is not presented as
conclusive attribution or universal repair of that intermittent OS-level failure.
Historical failed runs remain in `UI_HOTPATH_OPTIMIZATION_ACCEPTANCE.md`.

No fresh native window, DPI or screenshot parity check is performed for this
lifecycle slice. Tests use synthetic/local services and no paid provider or user
database. Debug remains JIT; release remains Windows x64 single-file without AOT
or JSON source generation. No staging, commit, push or publish is performed; the
full product goal remains active.

Evidence is outside Git at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-repository-status-lifetime-20261012-133140`:
source/resource/architecture snapshots, `flow-baseline-*.trx`,
`git-baseline-observations.json`, `lifetime-red.trx`, `lifetime-focused.trx`,
`lifetime-green.trx`, `context-green.trx`, `git-context.trx`,
`app-hang-stacks.log`, `app-hang-async-verified.log`, `full/*/full.trx`,
`final/*/final.trx`, `solution-final-build.log`, `whitespace-final.log`,
`source-final-audit.log` and `diff-check-final.log`.
