# Automation plan-list acceptance

## Scope (2026-10-09)

This continuation repairs plan-list presentation and read ownership, not scheduling
or database failures. A pending first read no longer appears as an empty catalog.
Failure has its own safe message and real retry action. Refresh keeps previous cards
visible but blocks snapshot-dependent mutations and history navigation. Stop remains
available for a currently owned running plan. Successful retry clears the read error.

The editor and removal dialog share a list notice with retry; absence of a notice
allocates no extra notice spacing. Retrying does not discard an open editor's draft.
Row actions reject foreign objects and objects replaced by refresh, including direct
command invocation. A pending removal target is rebound only to an equal current
schedule; changed or missing targets dismiss confirmation and report conflict.

## Design and safety self-check

1. App owns observable list presentation and exact enabled-model catalog resolution.
   Reads reuse `IAutomationRepository`, `IWorkspaceRepository` and
   `IConfigurationRepository`; dependency direction remains unchanged. UI does not
   read SQLite, files or provider HTTP directly.
2. Execution continues through the existing Application scheduler and task-scoped
   runner. No new execution engine, provider fallback or task-creation path is added.
3. Reads coalesce into one owned operation. Scheduler notifications request a
   follow-up read rather than losing a state change during an existing read. The
   entire chain has a linked 30-second cooperative cancellation deadline; disposal
   cancels the owner and rejects late publication. A noncooperative port retains
   ownership until it returns. This does not change tool authorization, approval,
   execution timeouts, output limits or audit policy. Exception details are not
   rendered or logged by the list; history remains limited to 50 records.
4. New handwritten C# files each contain one type and remain below file/method hard
   limits. Views use shared Controls and appearance variants; existing markup and
   analyzer gates remain authoritative. JIT debugging and Windows x64 single-file
   publishing remain unchanged. No JSON/AOT changes or persistence migration occur.
5. Focused tests cover successful recovery, retained selection/snapshots/drafts,
   rejected foreign/replaced commands, pending-read blocking, running-task Stop,
   catch-up reads, reentrant observers, cancellation/disposal and safe error text.
   Execution tests use the existing deterministic loopback SSE provider and inspect
   persisted cancellation, rather than asserting a fake success status.

## Verification evidence

Evidence root: `%TEMP%\MossAgent-automation-list-20261009`. Python scripts, native QA
source/executable, synthetic data and pixel captures remain outside repository source
or in ignored validation artifacts. Nothing is submitted, pushed or published.

- The reproduced initial red run fails all 11 selected cases. Six added test files
  contribute 24 cases. The final automation-focused run passes 79/79 against the
  rebuilt assembly, including the final test-only formatting refactor.
- Final scoped whitespace verification passes. The solution build reports zero
  warnings and zero errors. The final full regression passes 3,326/3,326 cases in
  seven completed TRX files under `final-full/`: App 2,523, Tools 188, Providers 283,
  Infrastructure 136, Browser 76, Agent 63 and Platform.Windows 57; none fail or skip.
  The scoped Roslyn audit checks 11 C# files with one type each, no syntax errors,
  at most 224 physical lines and at most 34 lines per method/accessor. The three
  scoped AXAML files parse successfully and remain below 300 lines. Tracked and
  17-path scoped diff checks pass using the repository's normal newline conversion;
  no index entries are staged.
- Native QA uses real App startup/shutdown with ordinary privileges, JIT, isolated
  SQLite data and paused read-only schedules. A repository decorator blocks the first
  read and then throws a synthetic I/O exception; retry reads the actual SQLite
  repository. This proves handling of injected failures, not repair of a real SQLite
  or filesystem incident.
- Passive Python PrintWindow captures reject black frames and wrong dimensions.
  Light 1280x820 and dark 980x640 at render scale 1 cover loading, failure, actual
  mouse retry, restored cards and an opened editor with reachable footer. Light Escape
  and dark mouse Cancel dismiss the editor. Both final cases exit normally with plans
  still paused/read-only, zero new runs, zero new conversations and zero decorator
  writes; the workspace draft remains unchanged.
- Same-size light before/after comparisons use native crops without scaling. The
  old page misleadingly shows empty results while loading/failed and retains its
  old error after refresh. The new page shows distinct loading/failure states and
  removes the old error after retry. These compare MossAgent revisions, not Codex.
- Windows Computer Use needed a fresh screenshot-backed observation after a
  geometry-unavailable mouse call. Retry, edit and Cancel outcomes were then
  verified against the actual UI and native persisted-state checks. A stale cached
  tree or null tree alone is not accepted as evidence of the action's result.

## Remaining limits

- Native failure/retry while an editor is already open is covered by headless tests,
  not by this native run. Noncooperative timeout ownership has deterministic test
  coverage, not a separately timed native database experiment.
- Same-state current Codex reference pixels, higher DPI, user-font combinations,
  exhaustive hover/focus and pixel-perfect automation-page geometry still require
  comparison. No claim of one-to-one Codex fidelity or full product completion is made.
- No real paid model request, plan enabling, saving or removal is performed in native
  QA. Scheduler, migration, authorization and model routing are unchanged here.

## Metadata projection optimization (2026-10-12)

Design self-check for this focused refactor:

1. App owns row projection, using the existing repository and enabled-model ports;
   no dependency or direct infrastructure access is added.
2. Each successful read uses temporary identity indexes instead of scanning project
   and model catalogs for every row. No shared or cross-refresh cache is introduced.
3. Existing authorization, approval, 30-second cancellation, read ownership, audit
   and output limits remain unchanged. Fresh row identities and selection notices
   must remain intact because they invalidate stale actions and refresh history.
4. The existing list owns the small projection method. Handwritten C# retains one
   type per file and stays within the file and method limits; no style or JSON change.
5. Regression tests cover a bounded single project scan, duplicate first-match
   behavior, composite provider/model identity, unavailable or disabled metadata,
   empty lists, fresh reloads and immutable earlier rows. Existing automation tests
   retain command ownership, history refresh, cancellation and disposal coverage.

Implementation and verification:

- `AutomationPlanListViewModel` projects the current catalog through temporary
  first-match project and composite provider/model indexes, then builds one row
  array in schedule order. Empty plan snapshots skip indexing. Ownership checks
  use the sealed row type's reference equality via `Contains`, without a predicate
  closure. No row cache or collection reconciliation is added: replacement,
  selection rebinding, stale-command rejection and history refresh stay unchanged.
- The reproduced baseline passed 10 new cases and failed the single-scan case:
  32 rows over 64 projects performed 2,048 project-element reads. All 11 new cases
  pass after correction; the same case now requires exactly 64 reads. This proves
  removal of repeated catalog scans, not an application-wide latency improvement.
- The final automation/audit-focused run passed 207/207. The solution build passed
  with zero warnings/errors, and the repository-wide architecture checks passed
  7/7. Scoped whitespace verification passed. Roslyn checked the three scoped C#
  files: one type each, 136/164/24 physical lines, maximum method/accessor lengths
  28/33/5, and no syntax errors.
- The final-build App assembly passed 3,757/3,757, with zero failures/skips and no
  hang dump, in the completed `tests/projection-final-full.trx`. This includes the
  11 new projection cases and the preceding audit-presentation regressions. Test
  workers exited before any further build; no overlapping DLL rebuild was used.

External baseline, TRX and evidence directory:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-automation-projection-20261012-104438`.
This slice does not change visual controls, layout, styles, scheduler execution,
repository contracts, data schemas, packaging or JSON. No new native screenshot,
real-account or paid-provider acceptance is claimed; the prior native evidence and
the full-product goal remain separate.

## Editor and history refresh work reduction (2026-10-13)

This slice removes repeated presentation work, not scheduler behavior or visual
styles. Editor initialization previously rebuilt and validated its schedule after
each changed field, then rebuilt it again explicitly. History selection cleared
its rows before reading and then cleared them a second time during projection.

Design and safety self-check:

1. App owns editor initialization and history presentation. Existing Application
   and repository ports remain authoritative; dependency directions are unchanged.
2. No new port, execution path, cache, storage contract or migration is introduced.
   Editor preview uses the existing schedule validation and timing calculation.
3. History retains the 30-second linked cancellation deadline, selection/version
   guards, disposal behavior and navigation ownership checks. It publishes at most
   50 matching records in source order, including separate rows for duplicates.
   The change does not execute a schedule or alter approvals, models or tools.
4. An instance-owned initialization scope suppresses only intermediate preview
   calculations. Every changed field still publishes its normal notification and
   advances the draft revision. Context revisions remain unchanged. A finally
   block restores the scope, including after failed or nested initialization.
   The five scoped C# files contain one type each and are all under 200 lines.
5. Eight added cases cover complete-draft initialization/reopening, individual
   invalid edits, recovery after failed initialization, single collection resets,
   old-row rejection, duplicate ordering, foreign records and bounded reads.
   Existing cancellation, disposal, navigation and save-ownership tests remain.

Verification:

- The seven-case baseline fails three cases and passes four. The synthetic editor
  opening performs nine clock/preview calculations instead of one; both empty and
  nonempty histories emit two resets instead of one. The corrected cases require
  one calculation and one reset. This proves reduced work for these operations,
  not an application-wide latency or memory improvement.
- Final automation-focused App tests pass 126/126, including all eight new cases;
  visual smoke tests pass 12/12 and repository architecture checks pass 7/7.
- The solution builds with zero warnings/errors. Repository `git diff --check`
  and explicit whitespace checks for the scoped untracked source files pass.
  Tests use an ignored `.scratch` JIT output directory; the already-running native
  QA application's default-output assembly is not replaced or restarted.
- External logs and TRX results:
  `C:\Users\wzheng\AppData\Local\Temp\MossAgent-code-cleanup-20261013-071648`.

The complete App suite was not rerun in this slice. No new native screenshot,
same-state Codex comparison, real-account or paid-provider result is claimed.
Packaging remains Windows x64 single-file/JIT; JSON and AOT settings are unchanged.
The full-product goal and its remaining native acceptance work remain separate.

## Production scheduler execution and read-only restoration (2026-10-13)

These are real production-App scheduler/runner executions against a deterministic
loopback Responses service, not paid-provider or real-account acceptance. Original
evidence is in `%TEMP%\MossAgent-automation-production-20261013-0052`.
The executing App assembly SHA256 recorded in `runtime-files.json` is
`D60261459E5BE56095641242305D55A464DED6866AD359AD96FE9E7F99E3565C`.
Later rebuilt UI assemblies are not substituted for this execution identity.

| Run | Local time (UTC+8) | Entry and outcome |
| --- | --- | --- |
| `01116e07-f25c-4b56-bf45-69a413ba1fd4` | 00:54:13 to 00:55:57 | Scheduled execution completes; final synthetic assistant message persists. |
| `e0f213d3-348c-4af5-82b5-e8e55fe6e659` | 00:59:26 to 00:59:44 | Manual execution is cancelled; the server records connection disconnection and no assistant success message. |
| `a830f51c-5f00-4988-a97e-6efd4d3b9872` | 07:08:15 to 07:08:16 | Manual execution receives synthetic HTTP 503 and reaches Failed, with no assistant success message. |
| `6be7fe5e-4716-4051-8901-bb5142c703f4` | 07:09:10 | Explicit new manual execution succeeds after failure; it owns a new task rather than replaying the failed run. |

All times are on 2026-10-13. The four independent tasks use project B and saved
ReadOnly policy. Whitelisted request logs record `concurrency-b`, high reasoning,
stream=true and the synthetic prompt flag for each request; credentials and raw
request bodies are not logged. The final schedule is paused. Database checks
confirm Completed/Cancelled/Failed/Completed run states, manual flags 0/1/1/1,
six total tasks, unchanged original 15 tool audits and three hash-verified
artifacts, and synthetic assistant replies only in the successful runs.

Native screenshots cover light normal automatic execution and history, dark
narrow cancellation, and dark maximized failure/recovery history and opened
conversations. They establish those execution/presentation paths, not equal-state
Codex pixels or the current rebuilt UI's complete theme/size matrix. The first QA
process is later found absent between the cancellation and recovery observations;
the exit cause was not observed, so neither normal close nor crash is inferred.

Subsequent ordinary-permission read-only reopen checks use the production App with
the plan paused and an observer that records then rejects unexpected requests.
The prior window-state assembly is
`319A951FBB3562556F23F378B0EB6648059445D29B914560D107217D0E5D4FEC`;
the latest caption assembly is
`38B1E1992B6280581F6C7634A69DFC1AAC1FCC17436E6B93C66A3733601A2074`.
Their audit files retain all four terminal runs and only four historical requests.
No new scheduler execution is claimed for these rebuilt versions.

`WINDOW_CAPTION_FIDELITY_ACCEPTANCE.md` separates the earlier native inspector,
history and minimize/restore sweep from the current caption build's mismatched
Sky screenshot and black-padded PrintWindow frame. Current-version native
interaction acceptance remains open; the full-product objective remains active.
