# Sidebar refresh optimization

## Design self-check

- App owns sidebar filtering, ordering and observable row reconciliation. No
  dependency direction changes; the existing workspace/configuration ports still
  own persistence and I/O.
- Project, pinned and recent lists share the existing navigation collection helper
  instead of allocating separate filter pipelines and snapshots on every refresh.
  Keep culture-aware title matching, project-name matches, pin exclusion, row
  identity, duplicate occurrence order, selection and observable deltas.
- Recent tasks are sorted when loaded or inserted, not on each search keystroke.
  Initial duplicate selection still takes the newest record and the first record
  when timestamps tie. Updated tasks still follow other equal-timestamp tasks.
- There are no new file, command, HTTP, approval, timeout or cancellation operations.
  No tool execution, authorization, task lifetime, secrets, JSON or publication
  policy changes. The optimization introduces no global or cross-task cache.
- Use lazy reconciliation state only after an actual mismatch. Equal prefixes and
  tail removals do not need row maps; changed suffixes still reuse equal rows and
  retain independent row instances for duplicate task records.
- Each handwritten C# file retains one type. Changed production methods remain
  below 40 lines and files below 250 lines.
- Focused tests cover 14,641 short duplicate-sequence transitions, equal records,
  prefix/tail notifications, filters with/without matches, selected rows, newest
  duplicate pin state, stable timestamp ties and updated ordering. Existing
  sidebar/search regressions and a solution build are also required.

## Baseline and allocation measurements

The pre-change focused run passes all 13 behavioral cases and fails the seven new
allocation guards. These failures establish the redundant work before changing
production code. All 324 focused sidebar/search/collection regressions then pass,
including the 14,641 duplicate-sequence transitions inside one test case.

| Warm scenario | Iterations | Bytes before | Bytes after |
| --- | ---: | ---: | ---: |
| Pinned, matching filter | 10,000 | 6,080,000 | 0 |
| Recent, matching filter | 10,000 | 17,840,000 | 0 |
| Project, matching filter | 10,000 | 7,760,000 | 0 |
| Pinned, no matches | 10,000 | 2,160,000 | 0 |
| Recent, no matches | 10,000 | 3,040,000 | 0 |
| Project, no matches | 10,000 | 3,840,000 | 0 |
| Append/remove one row after 32 retained rows | 1,000 cycles | 18,024,000 | 512,000 |

Measurements use Windows x64, .NET 10 Debug/JIT and thread allocation counters
after 100 warm-up iterations. The filters use normalized queries and existing
in-memory task snapshots. Tail measurements include new row creation and required
observable notifications. The append/remove reduction is 97.16%; these are
bounded microbenchmarks, not an overall UI speed or memory-use claim.

## Verification

- Focused sidebar, recent-task, search and collection regressions: 324/324 passed.
- Complete App regressions: 3,638/3,638 passed. The seven allocation measurements
  match the focused run. This is a new successful run, not a reinterpretation of
  the previous collection-optimization run's temporary-directory cleanup failure.
- Architecture constraints: 7/7 passed. The other complete test assemblies were
  not rerun in this slice.
- Post-build replay of the new allocation/collection and recent-task cases:
  20/20 passed.
- Solution build after test-host exit: zero warnings and errors. Two earlier
  concurrent build attempts failed copying the App DLL locked by the running
  test host; their logs are retained. No process was killed or source changed to
  bypass the lock, and the final serial build succeeds.
- Scoped `dotnet format whitespace --verify-no-changes` and `git diff --check`
  pass. Roslyn auditing confirms one type per changed C# file, a maximum file
  size of 153 lines and a maximum method/accessor/local-function size of 32 lines.
- No appearance markup, shared-control geometry or native component boundary was
  changed. This slice does not include a fresh native-window screenshot, paid/live
  provider requests or completion of the separate subagent native lifecycle check.
- Windows x64, JIT debugging and single-file publishing remain unchanged. No AOT,
  JSON source generation, commit, push or release is introduced; unrelated
  working-tree changes are retained.

Evidence is outside the repository at
`%TEMP%\MossAgent-sidebar-refresh-optimization-20261011`: `baseline.trx`,
`focused.trx`, `app-full.trx`, `architecture.trx`, `final-replay.trx`, `solution-serial-build.log`,
whitespace/source-audit logs and the pre-change file snapshots. Earlier failed
build logs remain `solution-build.log` and `solution-final-build.log`.

## Project-list subscriptions: 2026-10-12

### Design self-check

- App owns this synchronous in-memory projection. Existing Application ports,
  persistence and dependency direction remain unchanged; no new service or cache.
- The project list no longer builds an `Except` membership set and scans every
  project subscription after each source change. Add/remove/replace process only
  affected rows. Constructor/reset rebuild subscriptions, and source moves leave
  subscriptions untouched while still refreshing sorted rows.
- Last-occurrence checks preserve duplicate references; stable sorting still
  respects source order when every sort key, including project identity, ties.
  Existing manual/activity order, row identity, selection and expansion remain.
- Captured collection/property notifications can still run after another listener
  disposes the list. Guards reject these callbacks without republishing rows or
  reattaching subscriptions. Independent list owners retain their own handlers.
- No file, command, network, provider or tool operations are introduced. Existing
  authorization, approval, timeout, cancellation, output and audit limits are not
  changed. No migration, secret handling, appearance or publication change.
- The three handwritten C# files each contain one type: 97/144/50 physical lines,
  with largest methods of 24/23/30 lines respectively.
- Focused tests cover replacement with existing references, duplicate lifetimes,
  equal-key source moves, reset/reinsertion, owner isolation and callbacks captured
  before disposal. Allocation guards compare source moves to equivalent sorting
  refreshes, bounding extra event work independently of sorting allocations.

### Allocation evidence

Windows x64, .NET 10 Debug/JIT, current-thread allocation counters. Each workload
uses synthetic projects with unique creation times, warms up 100 two-operation
cycles, then measures 1,000 cycles (2,000 source moves). Sorting is still performed;
there are no visible row changes or collection notifications in this workload.

| Projects | Move bytes before | Move bytes after | Reduction |
| --- | ---: | ---: | ---: |
| 32 | 8,880,000 | 6,736,000 | 24.14% |
| 256 | 53,168,000 | 40,784,000 | 23.29% |

These are bounded allocation measurements for source moves, not overall UI speed
or application memory-use claims. Add/remove/replace have deterministic behavior
coverage but are not separately benchmarked here.

### Verification

- Original final baseline: 5/9 pass; both allocation guards and both captured-
  notification disposal regressions fail. The earlier eight-case baseline is
  also retained rather than reinterpreted as successful acceptance.
- Post-change focused sidebar/recent/search/collection tests: 382/382 pass.
- Architecture constraints: 7/7 pass. Solution build: zero warnings and errors.
- Scoped whitespace and diff checks pass. Roslyn source auditing confirms the
  file/type/method limits above using PowerShell's loaded parser.
- First complete App run: 3,765/3,766 pass. The existing provider-catalog
  `WarmModelCommandAndAvailabilityChecks_DoNotAllocate(missing: false)` assertion
  expected zero bytes but measured 1,616. No provider implementation or test is
  changed. That suite and all nine new cases then pass together, 16/16, in a
  separate replay; the failed complete run remains preserved.
- Second complete App run without code changes: 3,765/3,766 pass. The provider
  guard now passes; the existing project/no-match `WarmUnchangedFilters` guard
  instead expects zero bytes but measures 1,304. Both affected allocation suites
  and all nine new cases pass together in the final replay, 22/22. Neither failed
  full run is classified as passing. The intermittent allocations' cause is not
  diagnosed in this slice, and no zero-allocation assertion is relaxed.
- Other full test assemblies are not rerun in this slice, apart from the seven
  architecture constraints above.
- No native window/screenshot comparison or paid/live provider request is run
  for this optimization-only slice. No styles, control geometry or markup change;
  headless behavior coverage does not complete native visual acceptance.
- Debug remains JIT and publishing remains Windows x64 single-file, without AOT
  or JSON source generation. Existing dirty work is preserved; no staging,
  commit, push or publish.

Evidence is outside the repository at
`%TEMP%\MossAgent-project-subscriptions-20261012-112239`: `baseline.trx`,
`baseline-final.trx`, `focused.trx`, `architecture.trx`, `solution-build.log`,
`whitespace.log`, `diff-check.log`, `source-audit.log`, `app-full.trx`,
`app-full-repeat.trx`, `final-replay.trx`, `allocation-replay.trx`,
`solution-final-build.log` and the pre-change source/document snapshots.
