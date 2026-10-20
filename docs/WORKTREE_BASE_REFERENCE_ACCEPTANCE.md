# Explicit worktree base reference candidate

Status: the independently validated candidate in `artifacts/worktree-ref-20261019`
was integrated through 22 exact paths with verified backups and zero unrelated
source differences. Original Debug/Release solution builds have zero warnings/errors;
each passes Infrastructure 743 and related App 109. Full Debug regression 14404
is terminal: 8,182 passed, one failed, no skipped/aborted tests. All eight reports
and unchanged 2,432-source/2,429-runtime identities were reviewed; its freeze ended.
The failure is the environment flyout retaining DismissOnButtonClick after adding
a reference input. The original failure remains in regression-summary.json.
The prior original complete regression 54927 passed 8,153 tests;
its eight reports and unchanged source/runtime identities were checked and its freeze ended.
The initial candidate Debug statistics failure remains unclassified. A separately
reproduced observation defect has been corrected and tested, including a current
failure rejection case. Passing checks do not recover the missing historical code.

## Behavior and boundaries

The existing environment flyout has a localized shared input for a local branch,
tag or commit. Empty input retains HEAD. Selection is captured with the send
request, locked during preparation, ignored for project-directory sends and
refused as an existing-task override. Infrastructure resolves the supplied
revision with rev-parse --verify --end-of-options and commit peeling, then passes
only the verified immutable hash to worktree creation. A branch move after
resolution cannot substitute another commit. Invalid/noncommit/option-like,
oversized, control or internal-whitespace input refuses creation without falling
back, sending a model request or discarding the draft/reference.

This extends the existing Application port, factory and send snapshot. Existing
root/link/repository guards, cancellation, total timeout, bounded output,
task-owned native process trees, fixed conversation directory and persistence
rollback remain in place. No schema change: Git retains the actual checkout and
the task retains its managed worktree path. No fetch, remote operation, branch
discovery or command sandbox is introduced. Unsent environment choice persistence
is not added by this slice.

## Current evidence

- Candidate Debug/Release solution builds: zero warnings/errors after correcting
  a wrapper name and obsolete placeholder/PNG APIs in the new code/tests. Original
  failed build logs remain separately named in the candidate directory.
- Initial Debug feature selection: Infrastructure 60 and App 45 pass. Expanded
  feature selection, including keyboard and resolution lifetime negatives:
  Infrastructure 65 and App 51 pass. These are overlapping selections.
- Full candidate Infrastructure in both configurations: 743 pass each.
- Expanded App: Release 107 pass; Debug 106 pass / 1 fail. The failure is
  `WorkspaceFollowupToolBoundaryTests.QueuedWritesUseTheirSavedPolicyAndPublishRealResultsAndAudits(AskEveryTime,true)`
  at its background-statistics IsSuccess assertion. Its queued write and file
  content assertions pass. The log does not record the audit error code, so no
  specific cancellation/tool/root cause is established. Diagnostic replay adds
  audit metadata to the existing assertion message without weakening it.
- Diagnostic replay stops at iteration 10 on a different failure: the refused
  write waits 30 seconds after sending its denial before CurrentRequestId was
  published. The artifact-only Headless publication probe deterministically sees
  IsPending before a resolvable identity. The candidate now awaits the exact write
  tool, request ID and both commands' readiness; original policy/allow/deny/result
  checks remain. Six observation/tool-boundary checks pass. The probe is retained
  as a diagnostic text source, not a shipping test that freezes property order.
  This does not explain the earlier background-statistics failure. The revised
  readiness check then passes 20 independent processes / 60 overlapping checks;
  no statistics error code was observed in those replays.
- Final matching Debug/Release solution builds have zero warnings/errors. Both
  configurations pass full Infrastructure 743 and related App 107 checks, with
  no skips/aborts and unchanged before/after source/runtime path/hash sets. Four
  complete TRX hashes are retained in `candidate-verification-final.json`.
  These later passes do not identify or erase the first statistics failure.
- A subsequent artifact-only actual-Git/SQLite/production-executor Headless
  diagnostic gates the first statistics process completion until the next
  queued message replaces its file-card projection. At that replacement it
  records exactly the successful approved write and a completed statistics
  audit with `IsSuccess=false`, `ErrorCode=tool_cancelled`; a later query succeeds,
  the UI shows +1 and the write remains once. Both auditable snapshots are in
  `diagnostics/statistics-lifetime-observation.json`. This proves the old
  first-completed-audit predicate can observe superseded work. It still does
  not recover the error code missing from the original failure, so the original
  failure remained unclassified and the integration gate stayed closed at that
  checkpoint. No
  existing success assertion or approval policy was weakened. The first
  diagnostic without a Headless dispatcher timed out awaiting UI totals; its
  failed report is retained, followed by the passing dispatcher-backed report.
  Temporary probe types and fixture configuration are retained as text under
  diagnostics and removed from shipping test compilation; the original 2,428
  candidate source/configuration identities are checked after restoration.
- Expanded checks retain unchanged before/after path/hash sets for 2,428
  source/configuration and 2,429 executable/runtime files per configuration;
  generated test PNGs are excluded explicitly from that runtime identity set.
- Real temporary Git + SQLite + production Agent/ToolExecutor + simulated Responses
  requests cover an older branch, tag and commit, actual read result in the next
  model request, unchanged source dirt, later messages and history reopening.
  Negative cases cover unauthorized/link paths, invalid revisions, multiple roots,
  branch movement, cancellation and the original total deadline during resolution.
- Four shared-control Headless keyboard/render cases at 526/1000 widths in
  light/dark themes pass. Narrow images were inspected for readability/fit; these
  are not native Windows picker/focus acceptance.

## Revised observation and integration decision (2026-10-19)

The old assertion required every background query to succeed and the total audit
count to be exactly two. The controlled real-Git/executor/SQLite lifecycle proves
that this is not a valid invariant: replacing the file-card projection may cancel
its old query before the current query succeeds. This observation defect is
established independently of the unclassified historical failure.

The shipping test now awaits the final Queue/run state and the current statistics
projection, drains its owner, and validates every completed audit. There must be
exactly one write; other records must be correctly authorized read-only statistics
calls. Earlier failures may only be tool_cancelled, must precede the final query,
and the final query and UI totals must succeed. The bounded reader must not be
full, and duplicate, interrupted or incomplete records fail. Existing saved-policy,
approval/denial, write-once, visible/persisted result and draft checks remain.

Two new controlled Headless cases prove old-query cancellation followed by success
and current-query failure. The latter injects a nonzero exit through the test
process port after real Git execution, obtaining snapshot_review_failed through
the production pipeline/UI; the same assertion rejects it. Snapshot mutations
also prove rejection of an unexpected old error and a cancelled final query.
This changes test observation only, not the production statistics lifecycle or
approval policy. The optional fixture seam uses the existing production DI.

The 22-path candidate has zero structural violations. Matching complete Debug and
Release builds have zero warnings/errors; both configurations pass Infrastructure
743 and related App 109 with four complete TRX reports. All 2,432 source/configuration
and 2,429 runtime path/hash sets per configuration remain unchanged. See
`statistics-observation-20261019/reviewed-candidate.json` for report identities and
`DESIGN.md` in that directory for the required self-check and evidence limits.

Engineering decision: replace the invalid observation gate with these explicit
lifecycle and failure checks and permit guarded integration. This is a revision
of the previous gate, not a claim that the historical failure was explained.
The original regression is terminal and reviewed. The old 16/17-path manifests,
all failed reports and diagnostic logs remain. Before copying, recheck all 22
original/candidate hashes and back up originals; then validate builds and runtime
tests in the original checkout. No unrelated path may be overwritten.

The guarded copy record is `artifacts/worktree-ref-integrated-20261019/integration-result.json`:
2,421 original source/configuration files become 2,432; only the expected 22 paths
differ. The first integration script attempt stopped at a PowerShell parse error
before any mutation. A baseline build inadvertently ran after that refusal; its
logs are explicitly named build-Debug-preintegration and are not integration
evidence. The corrected script then passed all guards and retained originals.
Both subsequent original-checkout solution builds passed with zero warnings/errors.

Four complete post-integration TRX reports pass: Debug and Release each contain
Infrastructure 743 and related App 109, with no skipped/aborted/failed cases.
Both configurations retain identical before/after sets of 2,432 source/configuration
and 2,429 runtime files (test-generated PNG artifacts explicitly excluded). Report
hashes are in `artifacts/worktree-ref-integrated-20261019/related-reviewed.json`.
These related checks overlap the candidate checks and are not additional feature
coverage. Full Debug regression 14404 ended with 8,182 passes and one interactive
flyout guard failure. All eight reports and before/after path/hash sets were
reviewed; its freeze ended. No historical green result substitutes for this failure.

## Interactive reference flyout correction (2026-10-20)

The new reference input makes this a multi-step flyout. The App markup now uses
the existing shared default instead of opting into dismissal on every button
click. The existing selection commands still close it explicitly. No shared
component default, authorization, port, timeout, persistence or dependency changes.
The markup remains 28 lines and the single-type test stays below the file/method
limits. The existing guard is unchanged; the focused Headless test now types into
the real input, selects both environments with Space, verifies explicit dismissal,
reopens with the same reference and checks preparation locking in both themes at
526/1000 widths. This verifies keyboard behavior, not native Windows interaction.

Before the correction, a fresh check matched all 2,432 source/configuration and
2,429 Debug runtime paths/hashes to the reviewed failed regression. Backups and
new validation belong in `artifacts/worktree-reference-flyout-20261020`; the earlier
failure reports are retained. Debug/Release solution builds have zero warnings/errors.
Each configuration passes Infrastructure 743 and related App 112, including the
unchanged failing guard and all four enhanced keyboard cases. Four complete TRX
reports, counters, hashes and unchanged source/runtime sets are recorded in
`related-reviewed.json`. Full Debug regression 26906 is terminal and reviewed:
all 8,183 tests pass across eight distinct complete TRX reports, including App
5,450, with zero failures/skips/aborts and exit code zero. Before/after path/hash
sets match for all 2,432 source/configuration and 2,429 runtime files; the freeze
has ended. `regression-summary.json` records the counters and report hashes. This
full run covers the original checkout's flyout correction, not the isolated
Steer candidate. The earlier 14404 failure evidence remains unchanged.

## Concurrent runtime check

An isolated CLI harness in `artifacts/worktree-concurrency-20261019` loads the
original compiled App assemblies, with matching hashes checked before running
and at completion. Both Debug and Release pass. It starts two conversations in
one production App composition, from a branch at baseline A and a tag at baseline B.
Both actual Agents read their own real Git checkout through the standard file
tool, and the next local simulated model requests contain only the expected
baseline. Both runs are simultaneously tracked. An explicit cross-worktree read
through the standard executor returns path_denied with a completed persisted
audit and no target contents; source uncommitted changes remain intact.

Cancelling A through the selected conversation's real primary command leaves B
running. B then completes; task states are Cancelled/Completed, and both independent
drafts/history/fixed worktree environments survive service reconstruction with
the model request counts unchanged. The harness does not instantiate a native
window or prove simultaneous Git worktree creation. It does not test shell
confinement or change approval policy. Its source/type/method audit has zero
violations and both 2,429-file product runtime sets remain unchanged.

A separate synchronized creation scenario also passes against original Debug and
Release assemblies. Two factory preparations share one real repository; a
test-only launcher barrier requires both worktree-add requests to arrive before
delegating to the unchanged native Windows task launcher. Both creations succeed
with different paths, their fixed selected commits and checkout contents, and
exactly one persisted task/message each. Source dirt stays unchanged. Reports and
hashes are in `artifacts/worktree-concurrency-20261019/parallel-reviewed.json`.
This is a positive service-level parallel-creation case, not GUI concurrency or
acceptance of cancellation, crash or partial-failure combinations during creation.

The first two harness attempts failed explicit model selection because its
lane-B provider was accidentally saved as disabled. Adding a readiness wait alone
did not fix that. Correcting the harness's named IsEnabled argument permitted
the selected model; no product fallback or policy was added. Both failed logs,
the initial passing run and the final matching Debug/Release runs remain.

## Limits

Native interaction, live AI providers, other worktree lifecycle gaps and strict
command filesystem isolation remain open. This candidate is not whole-product
completion. Availability in the original App requires the integration record and
its matching validation, not candidate-only evidence.
