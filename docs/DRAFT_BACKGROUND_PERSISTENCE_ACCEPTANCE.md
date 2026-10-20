# Draft background persistence

## Scope and design self-check

The full App regression reached 3,812 passing cases and four already-corrected
settings-navigation failures before its test host was aborted for inactivity.
The incomplete case was `FileCardFidelityTests.TypographyAndMoreAction`.
The orphaned App process's native and async stacks show its fixture disposal
waiting for draft persistence after the Headless dispatch had returned.
This is a separate lifecycle dependency from the earlier incorrect
`Dispatch<Task>` overload. It does not prove that normal native shutdown hangs.

1. App owns scheduling and observable state; Infrastructure still implements
   persistence. Only pure persistence work moves off the UI context.
2. Reuse `IConversationDraftRepository` and `SettingsOperationLifetime`; no new
   storage, tool, permission or provider contract is required.
3. Existing cancellation, thirty-second operation deadlines, revision checks,
   attachment validation and bounded payloads remain unchanged. A storage
   failure must keep input and prevent silent successful shutdown.
4. Keep one type per file and existing size limits. Do not enlarge the already
   near-limit `WorkspaceViewModel` or alter UI markup.
5. Focused tests must let the UI dispatch return with a pending read or write,
   then prove completion, failure handling and cancellation without another UI
   pump. Notifications must still occur on the owning UI thread. Existing
   conflict, scope, attachment and shutdown tests remain required.

## Reference feature inventory

Official [follow-up documentation](https://learn.chatgpt.com/docs/prompting#steering-and-queuing)
was fetched on 2026-10-18: steering changes the current run; queuing schedules a
later run and exposes editing, ordering, sending and deletion. These are still
MossAgent gaps, not features completed by this persistence fix. The work returned
to the reproduced lifecycle blocker before adding a partial queue entry point.

## Implementation

`WorkspaceComposerDraftsViewModel` starts pure repository reads, delayed writes
and reconciliation through background tasks rather than UI-context yields.
The existing gate still protects draft snapshots and revisions. An idle writer
publishes its idle state while holding that gate; flush/recovery follows a
replacement writer rather than mistaking an earlier completion for a fully
drained batch. Shutdown does not require a captured UI continuation to persist
data. Observable notifications still post to the owning UI context, and failed
saves still retain input and reject silent shutdown.

The next full run also exposed an agent-selection failure. The constructor's
unawaited initialization and an explicit `LoadAsync` could overlap their model
and agent reload phases. An isolated follow-up adds `WorkspaceLoadCoordinator`
to serialize whole workspace refreshes, without coalescing an explicit refresh
into a pre-settings-change snapshot. This stays in App and reuses the existing
load pipeline/ports; it changes no tool permissions or repository deadlines.
New deterministic tests cover ordered execution, fresh snapshots and release
after failure; real SQLite agent/model selection covers ordinary and overlapping
refresh requests. No new behavior is claimed for interrupted tool execution.

## Evidence

- Original full candidate: `artifacts/app-regression-20261018`.
- `continuation-app-stacks.log`, `continuation-async-stacks.log` and the retained
  test sequence identify the pending draft writer and fixture cleanup chain.
- `results/full/*10_35_22_net10.0.trx` records 3,812 passes, four failures and an
  aborted run. Those counts do not establish a complete App-suite result.
- The independent before candidate at `artifacts/draft-background-20261018`
  compiled but its pending-read test did not complete and the test host was
  aborted by the 90-second inactivity limit (`results/before.trx`). It has zero
  completed cases; this is an aborted reproducer, not four ordinary failures.
- `artifacts/draft-background-20261018-fixed/results/focused.trx`: **118/118
  passed**, no skips. Includes all four new background read/save/error/timeout
  cases, notification-thread checks, eleven file-card cases, draft/attachment
  scope/conflict/race tests and shutdown-related coverage.
- The fixed candidate's full solution build passed with **0 warnings and 0
  errors** (`build-solution.log`). `source-shape.json` checks 144 changed/untracked
  handwritten C# files, one type each, maximum 497 file lines / 67 method lines;
  `git diff --check` passes. The initial PowerShell audit attempt failed to load
  compatible Roslyn assemblies; this JSON was regenerated successfully by the
  ignored local .NET/Roslyn checker, not accepted from that failed attempt.
- Its full solution regression was session **57131**, rooted at
  `artifacts/draft-background-20261018-fixed`. The frozen App binaries' hashes
  are in `full-app-binary-identity.json`. It ended with exit 1: App completed
  **5,014 passed / 5 failed / 0 skipped**, with no inactivity abort. The other
  projects completed successfully: Agent 137, Visual 300, Browser 88,
  Infrastructure 328, Windows 142, Providers 600 and Tools 494. App failures
  comprise the 3,552-byte allocation check, original agent-selection assertion,
  two authorization scenarios and model-menu initialization described below.
  Allocation assertions have not been weakened or skipped. This is not a green
  full run, and isolated passing checks do not resolve its remaining failures.
- The load-serialization follow-up and expanded focused checks are isolated at
  `artifacts/draft-background-20261018-final`; they do not overwrite the running
  fixed candidate. Its `results/focused.trx` records **95/95 passed**, including
  all three load-coordinator cases, both real SQLite agent-selection refresh
  cases and the four background-persistence cases. This filter differs from
  the 118-case run, overlaps it, and is not an additive unique-test total.
- The final candidate's solution build passed with **0 warnings and 0 errors**
  (`build-solution.log`). Its `source-shape.json` covers 147 changed/untracked
  handwritten C# files, one type each, maximum 499 file lines / 67 method lines,
  with no syntax/size violations; whitespace checks pass.
- Its `results/allocation-diagnostic.trx` records **18/18 passed**, covering model
  lookup, sidebar filtering and progress allocation checks without changing any
  zero-allocation assertion. This isolated pass does not erase the current
  complete run's 3,552-byte failure or prove full-run allocation stability.
- Session 57131 is terminal and its eight TRX reports have been inspected. Neither
  the final nor the newer workspace-regression candidate inherits that result;
  their complete runs remain required. An observation timeout is not a reason
  to restart or overwrite a running candidate.

After native/async diagnostics and terminal test-host/runner results were
verified, only the two identity-checked orphaned App test executables were
retired. `retired-test-processes-continuation.json` records PIDs 24096 and 14368,
their exact candidate paths, creation times and reasons. Sessions 96000 and
25943 then ended with exit 1. No normal MossAgent/Codex window was affected.

Only this turn's 2,021,264,845-byte `continuation-app-heap.dmp` was removed after
the text diagnostics and SHA-256 metadata were retained in
`continuation-heap-metadata.json`. The earlier `original-app-heap.dmp` was not
removed. No raw heap dump was shared externally.

## Expanded regression follow-up

The frozen full run later reported two light-theme project-authorization cases
returning `tool_failed`, and `ModelPickerMenuTests.SearchWithNoMatches` seeing an
unready catalog. Together with the earlier allocation and agent-selection cases,
these are the five terminal failures in its 5,019 executed App cases. The original
run and binaries remain unchanged; its runner completed with exit 1.

The model-menu case was reproduced through the real xUnit runner against the
unchanged final candidate: `artifacts/project-auth-probe-20261018/model-menu-xunit-0`
records a failure at the single-provider `ShowSearch` assertion. The test resolved
Workspace, which begins asynchronous initialization, but awaited only a separate
picker reload. Workspace's later reload can supersede that read; from a test
thread without UI serialization, their catalog publication can also interleave.
Both production-composition menu tests now await `Workspace.LoadAsync` before
exercising the picker. Every existing model, search, provider-group, reasoning and
empty-state assertion remains. This corrects test initialization, not production
reload cancellation semantics or the expected product behavior.

This follow-up is test-only: App.Tests owns it, it reuses the existing load entry
point, invokes no provider/tool operation, changes no permissions or persistence,
and stays within the existing type/file/method constraints. The candidate is
isolated at `artifacts/workspace-regression-20261018`; its solution build passed
with zero warnings/errors. `source-shape.json` audits 148 changed/untracked C#
files, one type each, maximum 499 file lines / 67 method lines, without violations.
Whitespace checks pass. Its `results/focused.trx` completed **99/99 passed** with
no skips, including the menu, load coordinator, agent selection, background draft,
authorization, allocation, settings navigation and file-card cases. Ten separate
xUnit processes then repeated the model-search case successfully; each has its
own TRX under `results/model-menu-repeat-*` and the aggregate is
`model-menu-repeat.json`. These repeated cases are not additional unique tests.

After session 57131 ended, a complete solution diagnostic run started as session
**89190** against this frozen candidate. Its three App/App.Tests assembly hashes
are retained in `full-app-binary-identity.json`; output is `full-regression.log`
and `results/full`. The repository-ignored .NET 10 startup hook was inherited by
PowerShell-based terminal and stdio MCP children running .NET 9.0.10, which then
failed to load `System.Runtime, Version=10.0.0.0`. This polluted those scenarios;
the run is invalid, not evidence of new product failures or a full-suite pass.
The failure is reproduced outside MossAgent: baseline PowerShell exits zero,
whereas the same command with the hook exits -532462766. Bounded logs and exit
metadata are in `diagnostics/powershell-*` and `startup-hook-compatibility.json`.

Only that identity-checked diagnostic test process tree was retired, recorded in
`retired-invalid-hook-run.json`; session 89190 ended with exit 1. No product
binaries, global environment settings or normal user windows changed. Removing
the hook makes the four affected MCP/terminal baseline cases pass **4/4**, with
the original assertions (`results/uninstrumented-child-baseline.trx`). The hook
must not be enabled globally or across a mixed-runtime test process tree.

The only current complete run is ordinary, uninstrumented session **54206**, using
the same frozen candidate and explicit empty diagnostic variables in its own
shell. Output is `full-uninstrumented.log` and `results/full-uninstrumented`.
It is still in progress; inspect its terminal reports and remaining failures
before claiming any complete result.

This uninstrumented run has already reported
`WorkspaceCatalogLookupTests.WarmCatalogLookups_DoNotAllocate(missing: True)`:
736 bytes versus the unchanged zero-byte assertion. This is a new observed
allocation failure, not a terminal run total or proof of the same cause as the
earlier 3,552-byte model check. Keep both reports and diagnose without weakening
either assertion. As of 12:56 +08:00, runner PID 5856 and App PID 2432 belong to
this exact candidate; recheck process identity before any later action.

Additional diagnostic evidence, not substitutes for the failed full run:

- The original zero-allocation method passed 1,000 direct repetitions in one
  process and one repetition in each of 50 cold processes. Reports are under
  `artifacts/allocation-probe-20261018`. This neither resolves the 3,552-byte
  failure nor proves that JIT/PGO is its cause; the zero-byte assertion is intact.
- The final candidate's isolated authorization suite passed all eight xUnit
  cases. An external Headless/reflection probe completed six final-candidate and
  eight frozen-candidate repetitions of the expansion scenario without the
  unexpected failure; captured exceptions were the expected path denials only.
  These direct diagnostic repetitions are not additional unique xUnit tests and
  do not establish the cause of the full-run `tool_failed` results.
- A separate direct model-menu reflection attempt omitted the runner's UI
  bootstrap and did not complete. Its stacks and identity-checked retirement are
  recorded under `artifacts/project-auth-probe-20261018`. It is invalid evidence,
  not a passing/failed model test; the xUnit reproduction above supersedes it.
- A repository-ignored startup diagnostic was smoke-tested with the existing
  target-lock negative test: one xUnit case passed and captured an IOException
  with sharing-violation HRESULT `0x80070020` at atomic replacement. It stores
  bounded type/HRESULT/stack observations, not exception messages or credentials.
  This validates the diagnostic only; it does not identify the authorization
  failure's cause. No diagnostic hook is part of the product or normal test run.

## Subsequent isolated follow-ups

The later `workspace-reload-20261018` candidate also makes the two
`ModelPickerViewModelTests` await complete Workspace initialization before
changing model state. Its **107/107** focused cases pass without skipping or
weakening any model/disabled/event assertions. Its build has zero warnings/errors.

The diagnostic startup hook is now exact-process scoped and clears its variables
before the target creates any child processes. `results/scoped-hook.trx` passes
**12/12** authorization/MCP/terminal checks. Three diagnostic reports identify
the expected App.Tests process and `ChildHooksDisabled=true`, with no unexpected
IOException observations. This validates the diagnostic boundary, not the cause
of the original authorization failures; the hook remains outside the product.

An independent local .NET 10.0.12 X64 probe reproduces background-GC counter
anomalies. The short nonparallel measurement helper and retained zero/positive
controls are documented in `ALLOCATION_MEASUREMENT_ACCEPTANCE.md`; the
`allocation-isolation-20261018` candidate passes **113/113** focused cases.

The subsequent `snapshot-sharing-fixed-20261018` production change fixes a
deterministically reproduced internal-reader/atomic-edit conflict, with
**499/499** tool cases, **113/113** expanded App cases, a zero-warning/error solution build and the 157-file
source-shape audit. See `FILE_SNAPSHOT_SHARING_ACCEPTANCE.md` for the successful
and negative paths, intermediate fixture failures and exact scope. It does not
prove the cause of the two old full-run `tool_failed` reports.

At 13:17 +08:00 on 2026-10-18, ordinary frozen session 54206 and its exact App
process were still executing; CPU continued advancing and no terminal App TRX
was present. Its reported model-initialization and 736-byte failures remain.
Its binaries are not replaced by any newer candidate. Do not start a second
complete regression while this identity-checked run remains active.

No native screenshot, native shutdown or real-provider claim is made here.

## Ordinary frozen full run terminal result

Session **54206** has now ended with **exit 1**. The unchanged candidate executed
all **5,023** App cases: **5,021 passed / 2 failed**, no skips, abort or hang.
The only failures are model-picker initialization and the 736-byte catalog
allocation assertion already recorded above. All eight authorization cases pass
in this run, before the later snapshot-sharing production fix. This excludes
neither an intermittent issue nor other causes of the earlier authorization
failures; no direct exception evidence closes those old reports.

The other seven projects passed: Agent 137, Visual 300, Browser 88,
Infrastructure 328, Windows 142, Providers 600 and Tools 494. Original TRXs and
the bounded aggregate `full-uninstrumented-terminal.json` remain under the
same ignored candidate directory. No focused result relabels this run as green.

The later model-initialization/allocation and snapshot-reader fixes are included
in `attachment-thumbnails-20261018`, together with the new composer/draft image
thumbnail workflow. Its build, 344 Infrastructure cases, 82 expanded App cases
and 168-file shape audit pass. Its new ordinary complete regression is session
**95889**, with frozen assembly identities, `full-regression.log` and
`results/full`; do not overwrite it or start another complete run while live.
`ATTACHMENT_THUMBNAIL_ACCEPTANCE.md` records the feature and remaining native,
history/PDF and complete-regression boundaries.
