# App regression recovery

This checkpoint removes a deterministic test-harness deadlock that prevented
complete validation of the existing product. It does not establish native UI
acceptance, fix a demonstrated production shutdown failure, or complete the
Codex functional-parity goal.

## Required design self-check

1. The test harness owns this behavior. Production modules, authorization,
   persistence contracts and dependency direction remain unchanged.
2. Reuse `HeadlessTestSessionExtensions.DispatchAsync`, which returns a non-Task
   generic result through Avalonia's asynchronous dispatch overload. Do not add
   another dispatcher, duplicate callback runner or skip affected tests.
3. Tests use isolated temporary roots and deterministic local data. Keep test
   cancellation, exception propagation and disposal. Diagnostic tools target only
   identity-checked test processes; no user application/window, approval policy,
   credential or paid provider is involved.
4. Handwritten C# retains one type per file and existing size/method limits.
   No UI markup or per-view appearance is changed.
5. Prove the original hanging case, real production-composition cleanup, delayed
   draft flush, callback failure/cancellation and continued session use. Extend
   the existing syntax guard, retaining its async-Action rejection while adding
   Task-valued generic dispatch rejection and positive/negative parser fixtures.

## Confirmed cause

The original full-solution run was still live, not simply missing a result file.
Its App test process had an idle Headless queue. Managed and async-heap stacks
located the pending chain at:

```text
ConversationHeadingLayoutTests.WithWindow
  -> service-provider asynchronous disposal
  -> WorkspaceViewModel.DisposeAsync
  -> draft StopCoreAsync / RecoverCoreAsync
  -> DrainWritesAsync continuation after a completed Task.Delay
```

The five affected window helpers explicitly selected
`Dispatch<Task>(async () => ...)`, then called `Unwrap`. With TResult equal to
Task, an async Task-returning lambda binds to the synchronous `Func<TResult>`
overload, not `Func<Task<TResult>>`. The Headless dispatcher finishes its operation
when the inner Task is returned. Unwrap waits for that Task outside the dispatcher
operation; it cannot keep the UI loop alive for its pending continuations.
The awaited delay was complete in the dump; this was not evidence of a provider
or SQLite operation blocking. Avalonia's installed API documentation confirms
the two distinct generic overloads.

The existing source guard rejected untyped async-Action dispatch, but did not
inspect `GenericNameSyntax`, so these explicit Task-result calls escaped it.

## Correction

- Conversation heading, chat header, full inspector, inspector overview and task
  navigation window helpers now use the existing awaiting DispatchAsync helper.
  Their setup, production composition, assertions and cleanup remain intact.
- The syntax guard retains its original untyped async-Action protection and
  additionally rejects async callbacks explicitly returning Task/Task<T> as the
  generic result. Fixtures distinguish both unsafe forms from awaiting bool
  callbacks, DispatchAsync, and an intentionally context-independent cleanup
  Task returned by a synchronous callback.
- A new runtime regression flushes a delayed draft before the Headless callback
  returns, checks the saved text, and then dispatches another callback. Existing
  callback-failure and cancellation cases remain in the focused run.
- The intentional cleanup-retry test still returns a Task through a synchronous
  callback; its cleanup uses ConfigureAwait(false) and is not incorrectly banned.
  No allocation assertion was relaxed, no test was skipped, and no product
  permission or persistence semantics were changed.

## Evidence

Candidate: `artifacts/app-regression-20261018`, Debug/JIT, 2026-10-18.

| Check | Evidence |
| --- | --- |
| Original full-run identity and wait | Original session 21912, dotnet PID 9992 / App PID 11744, process metadata and repeated live polls |
| Native/async wait chain | `original-app-stacks.log`, `original-async-stacks.log`, `draft-writer-async-fields.log`, `completed-delay-root.log`; the heap dump is a local ignored diagnostic artifact, not a repository deliverable |
| Independent current-code reproducer | `results/before/heading-dispatch-before.trx` and its sequence file: test-host inactivity abort while SingleConversation_UsesAnAlignedBorderlessHeading was running |
| Corrected window/helper regression | **74/74 passed**, `results/headless-dispatch-fixed.trx`, no failures/skips; covers all five affected classes, callbacks and the first extended guard fixtures |
| Final callback/dispatch-guard rerun | **12/12 passed**, `results/final-dispatch-guards.trx`, including the additional async-Action fixture; overlaps the 74-case run and is not added to it |
| Final solution build | `build-final.log`, exit 0, **0 warnings, 0 errors**; includes the additional guard fixture retaining untyped async-Action protection |
| Source/size/whitespace audit | `source-shape-final.json`: 142 changed/untracked handwritten C# files, one type per file, maximum 497 file lines / 67 method lines, no syntax/size violations; `git diff --check` passes |
| Allocation diagnostic | **11/11 passed**, `results/allocation-diagnostic.trx`, with unchanged zero-allocation assertions; an isolated pass is not proof that the earlier full-run allocation failures are resolved |
| Original expanded full-solution regression | Session 96000 ended with exit 1: App has 3,812 passes and four settings/new-task failures, then an inactivity abort during fixture cleanup; the seven other test projects passed. This is not a complete/green App result. See `DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md` for the separate persistence lifecycle fix and current candidates |

The retained failed reproduction is not counted as a passing test. The 74-case
run predates the final additional async-Action detection fixture; the final build,
12-case guard rerun and full run include it. Focused evidence is not a full
App-suite result.

### Settings navigation follow-up

The expanded run passed the former deadlock point and exposed four
`SettingsConversationNavigationTests` failures (menu/shortcut in both themes).
The old assertion expected an existing task's draft to become the project-new-
conversation draft. That contradicts the independent scope behavior introduced
by the draft workflow: the destination correctly restored the fixture's older
project-new-conversation text instead of copying the task's text.

The follow-up candidate is isolated at
`artifacts/app-regression-20261018-final`; the running original candidate's DLL
is preserved and its SHA-256 is recorded in `full-app-binary-identity.json`.
No production draft behavior is changed to force the old assertion to pass.
The revised test captures the real SQLite destination draft, checks that the
new-conversation entry restores it, edits/saves a distinct destination draft,
checks the original task draft in SQLite, and navigates to both scopes to verify
each is retained. Original theme/menu/shortcut, settings-close, visible-chat,
task-count and zero-provider-request assertions remain.

The follow-up passed **31/31** cases with no failures/skips, recorded at
`artifacts/app-regression-20261018-final/results/settings-draft-navigation.trx`.
That includes all 17 settings-navigation cases, the two independent-draft
navigation cases and the 12 final callback/guard cases. These overlap earlier
focused runs and are not summed as unique tests. The follow-up complete solution
build passed with **0 warnings and 0 errors** (`build-final.log`). Its
`source-shape-final.json` covers 143 changed/untracked handwritten C# files, one
type each, maximum 497 file lines / 67 method lines; whitespace checks pass.

No full-candidate pass is inferred from this focused correction. The original
full run ended with four failures and a later fixture-cleanup abort; its compiled
candidate and results remain unchanged. Native/async diagnostics identified a
separate draft-writer dependency on a UI loop after the correct dispatch had
returned. The follow-up lifecycle fix, current full run and isolated final
candidate are recorded in `DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md`.

### Explicit retirement of irrecoverable test processes

After stack/heap evidence, source inspection and the independent aborted
reproducer confirmed the same deadlock, only identity-checked owned test
processes were retired. This was not a restart based solely on observation time.
`retired-test-processes.json` records the old App process, the queued PowerShell
job waiting for that obsolete candidate, and the orphaned reproducer child.

The queued session 11377 ended with exit -1. The old solution run then finished
with exit 1; its partial App record contains 2 allocation failures and 2,855
passes before the fatal test-process retirement. Its other project results do
not make that App run complete or green. All original logs/results are retained.
The newly built full run is a different candidate with the dispatch correction,
not a relabeling of the old failed run.

## Reproduction

```powershell
dotnet build MossAgent.slnx --artifacts-path artifacts/app-regression-20261018 --nologo -v minimal
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --artifacts-path artifacts/app-regression-20261018 --no-build --filter 'FullyQualifiedName~HeadlessAsyncDispatchTests|FullyQualifiedName~HeadlessDispatchUsageTests|FullyQualifiedName~ConversationHeadingLayoutTests|FullyQualifiedName~ChatHeaderInspectorTests|FullyQualifiedName~InspectorFullViewWorkflowTests|FullyQualifiedName~InspectorOverviewGeometryTests|FullyQualifiedName~TaskNavigationRowTests' --blame-hang-timeout 90s --blame-hang-dump-type none --logger trx --results-directory artifacts/app-regression-20261018/results/recheck --nologo -v minimal
dotnet test MossAgent.slnx --artifacts-path artifacts/app-regression-20261018 --no-build --no-restore -m:1 --blame-hang-timeout 3m --blame-hang-dump-type none --logger trx --results-directory artifacts/app-regression-20261018/results/full-recheck --nologo -v minimal
```

Diagnostic dotnet-stack/dotnet-dump 10.0.750501 were installed only under ignored
`artifacts/diagnostic-tools`, not globally. No commit, push, publish, AOT, trimming
or memory update was performed. The product gap inventory remains in
`FUNCTIONAL_PARITY_MATRIX.md`.

## Background-persistence candidate terminal result

The later frozen `draft-background-20261018-fixed` full run (session 57131)
completed with exit 1 rather than hanging or being aborted: App executed 5,019
cases, with 5,014 passes and five failures. The other seven projects passed.
The failures concern allocation, agent selection, two authorization scenarios and
model-menu initialization; their exact reports and follow-ups remain in
`DRAFT_BACKGROUND_PERSISTENCE_ACCEPTANCE.md`. Passing the former cleanup point
does not establish native application shutdown or a green product regression.

The newest `workspace-regression-20261018` candidate passes 99 focused cases,
ten separate model-search repetitions, a zero-warning/error solution build and
the 148-file type/size audit. Diagnostic session 89190 was invalidated and its
owned process tree retired after the .NET 10 hook prevented .NET 9 PowerShell
children from starting. Baseline and hooked commands reproduce the environment
failure; the four affected MCP/terminal cases pass without instrumentation.
Ordinary full session 54206 is now the only complete regression running, using
the same frozen binaries with no diagnostic hook. Its log/results remain under
`full-uninstrumented`; no old failure is erased by isolated passes, and no normal
user window was operated.

Further isolated candidates add full Workspace initialization waits to two
model-picker tests and test-only no-GC allocation measurement; 113 expanded App
cases pass with the original behavior/zero-byte assertions. A separate confirmed
internal-reader/atomic-edit defect is fixed in the production snapshot reader,
with 499 tool cases passing, zero solution-build warnings/errors and a clean
157-file source-shape audit. See `ALLOCATION_MEASUREMENT_ACCEPTANCE.md` and
`FILE_SNAPSHOT_SHARING_ACCEPTANCE.md` for candidate paths, earlier failed reports,
positive/negative controls and the remaining full-regression/native boundaries.
None of these focused results establishes a terminal full-run pass or identifies
the direct cause of the two older authorization `tool_failed` reports.

Ordinary session 54206 has subsequently reached its terminal result: **5,021
App passes / 2 failures / 5,023 executed**, no skips, hang or abort; the other
seven projects pass. Its only failures are model initialization and a 736-byte
allocation assertion. All eight authorization cases pass before the later
snapshot-reader change, without proving why their older intermittent reports
failed. The terminal aggregate is `full-uninstrumented-terminal.json`.

The newest candidate adds bounded composer/draft thumbnails and includes the
intervening fixes. Its 344 Infrastructure and 82 expanded App cases, zero-warning
solution build and 168-file shape audit pass. Session 95889 is its only ordinary
complete regression, started after 54206 ended, with frozen binaries under
`artifacts/attachment-thumbnails-20261018`. See
`ATTACHMENT_THUMBNAIL_ACCEPTANCE.md`; this run's terminal result is not yet known.
