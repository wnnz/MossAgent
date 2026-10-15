# Workspace lookup optimization

## Scope: 2026-10-12

This slice removes redundant work in existing workspace presentation. It does
not introduce another sidebar, task catalog, persistence port or navigation
workflow. Ordinary JIT debugging and Windows x64 single-file publishing remain
unchanged; there is no AOT or JSON source-generation change.

## Design self-check

- App owns the synchronous in-memory lookup and tab projection. Dependency
  direction remains unchanged; Application repositories still own all I/O.
- The catalog, project navigation and tabs reuse one typed lookup helper for
  current project indices, projects, groups and tasks. Project editing no longer
  copies the entire project collection into a temporary list. Selected-tab
  lookup is folded into existing reconciliation; there is no cross-task cache.
- No new file, command, provider, browser or tool operation is introduced.
  Authorization, approval, risk, cancellation, deadlines, audit and output
  limits stay in existing execution paths. Closing a tab still closes only its
  presentation, not the stored task. No secrets or user data are read by tests.
- Every changed C# file retains one type and stays below 500 lines. The largest
  production file is the existing catalog at 464 lines; its longest method is
  43 lines. The new helper is 43 lines and has no method longer than ten lines.
  Tab synchronization remains 30 lines, below the 40-line preferred limit.
- Focused tests preserve first matching project/group/task, duplicate order,
  owning-project scope, full snapshots despite pin/search filtering, retained
  tab instances, current titles, draft selection, removed command parameters and
  close-without-archive behavior. Missing identities, removed rows and duplicate
  groups do not trigger fallback to a different owner or stale cached snapshot.
  Existing history, archive, lock/disposal and rendered-header tests are reused.

## Baseline and allocation evidence

The initial baseline included an incorrect expectation that closing a manually
injected duplicate tab would immediately select its surviving duplicate. Existing
catalog selection suppresses an equal-record notification, so that expectation
was removed without changing production behavior. The corrected pre-change run
passes all four behavioral cases and fails only the four new allocation guards.

The subsequent 13-case focused run passes every case. Measurements use Windows
x64 .NET 10 Debug/JIT, current-thread allocation counters and 1,000 warm-up
iterations. Tab measurements invoke the existing synchronous projection via a
delegate bound once outside the measured region; no navigation, storage reads or
provider requests are included. Collections have 32 task records unless noted.

| Warm scenario | Iterations | Bytes before | Bytes after |
| --- | ---: | ---: | ---: |
| Task identity lookup, found | 10,000 | 960,000 | 0 |
| Task identity lookup, missing | 10,000 | 960,000 | 0 |
| Unchanged single-tab synchronization | 1,000 | 480,000 | 48,000 |
| Unchanged 32-tab synchronization | 1,000 | 7,424,000 | 48,000 |
| Combined project/index/group lookup, found or missing | 10,000 | Not measured | 0 |

The 32-tab reduction is 99.35%; retained allocations are existing property
notification overhead. This measures temporary allocations in these operations,
not whole-application memory consumption or a claimed UI frame-rate improvement.

## Validation scope

- The new focused tests pass 13/13 with no skipped cases.
- The wider workspace/sidebar/project/history/header regression group passes
  491/491; architecture constraints pass 7/7, with no skipped cases.
- The complete serial App suite passes 4,032/4,032 with no failed or skipped
  cases. Solution build succeeds after the test host exits, with zero warnings
  and errors. Other test assemblies are built but are not rerun in this slice
  beyond the seven architecture cases.
- A final post-build replay of all 13 new cases passes; its task/catalog lookups
  still allocate zero bytes and both tab-count cases retain 48 bytes per refresh.
- A Roslyn source audit confirms one type per file, valid syntax and the file and
  method limits for all seven changed/new C# files.
- Scoped whitespace verification and repository `git diff --check` pass.
- No visual markup, shared appearance, component geometry, persisted JSON shape,
  database schema or execution pipeline changes. This code-only slice does not
  claim a new native screenshot comparison, Codex pixel equivalence, paid-provider
  acceptance or completion of the full product goal.
- Existing unrelated work remains intact. Nothing is staged, committed, pushed
  or published.

Evidence remains outside Git at
`%TEMP%\MossAgent-workspace-lookup-optimization-20261012-232727`: pre-change source
snapshots, `baseline.trx`, `baseline-corrected.trx`, `lookup-focused.trx`, run logs
and `source-audit.json`. Wider regressions and architecture results remain in
`workspace-regressions.trx` and `architecture.trx`; formatting and diff diagnostics
remain in `whitespace.log` and `diff-check.log`. The complete serial App run is
`app-serial.trx`, using the retained `serial.runsettings`; the solution build is
`solution-build.log` and the post-build replay is `final-replay.trx`.
