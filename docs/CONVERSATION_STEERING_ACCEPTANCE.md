# Active-run text guidance

Status (2026-10-20): integrated into the main checkout through 96 explicit paths
after exact `candidate-r4` full Debug/Release validation. Each configuration passed
all 8,282 tests with eight completed reports, no failures/skips/aborts and no
source/runtime drift. Guarded integration preserved 40 original-file backups and
reported no conflicts. Original-checkout Debug/Release solution builds and each
configuration's 147 focused checks / four process recovery cases also pass.
Earlier failed/stopped evidence remains preserved. This text-only
slice does not establish full Steer or full-product completion.

## Runtime and ownership

Entry: active composer “引导当前运行” or Ctrl+Enter. Enter continues to enqueue a
later run; idle Ctrl+Enter submits normally. Disabled alternate submission does
not fall back to another action. Existing repeat/IME/focus guards remain.

The command captures task/run identity and the draft revision, accepts bounded
text through `ConversationSteeringSession`, and atomically persists a receipt and
consumes that draft. At the next safe Agent boundary, a pending prefix becomes
ordered user messages in the same run. Tool batches finish before injection; no
tool/request is cancelled and replayed to imitate guidance. Run-start provider,
model, reasoning, agent, tool scope and approval policy stay selected.

All newly applied inputs must fit the next model context in full. Preparation
fails before a clipped request can be sent. Receipts mean durable local acceptance
or local boundary application, not confirmed server receipt. Required messages
remain required if more input arrives between adjacent boundaries.

Each run owns its gate, cancellation and acceptance state. Receipt IDs make exact
retries idempotent. An uncertain acknowledgement retains the same ID; reconciling
it preserves newer local draft text. Only the submitted draft entry pauses
background saving, so another conversation can save/start/accept concurrently.

## Persistence and authorization

Schema 25 is forward-only. Text is limited to 32,768 UTF-16 units per receipt,
16 pending entries and 256 KiB pending UTF-8 data. History pages contain at most
64 receipts. No attachment bytes or large tool output are added to SQLite.

App rechecks the run's project roots, source grant/account/binding and worktree
guard before acceptance/application. The candidate also passes an immutable
authorization snapshot into the existing SQLite open/accept/apply transactions.
Under the writer lock, those writes compare project identity/primary/authorized
roots and exact source binding, grant and enabled account revision. Forced
post-precheck revocation therefore cannot consume a draft or append guidance.
Storage-only repository tests may omit that snapshot; production App sessions
always supply it. OS worktree validation remains outside SQLite. These checks
do not promise atomic immediate revocation of an already sent HTTP request.

Stop/failure/shutdown close acceptance and mark unapplied receipts Interrupted.
Database reconstruction closes abandoned runs and exposes receipts without
replaying them, starting a model request or repeating any side effect. This has
repository/service reconstruction and separate owned-process termination
evidence below. Native GUI restart and termination during a database commit or
tool side effect remain unverified.

## UI and regression evidence

Evidence root: `artifacts/conversation-steering-20261020`.

- `app-Debug-composer-initial`: 45 passed / 6 failed. Receipt VMs were rebuilt on
  task updates; retaining them by task identity preserves expanded state.
- `app-Debug-composer-repair1`: 54 passed / 1 failed. A real global draft-write
  lock blocked another task; per-entry coordination then passed 55 cases.
- Window initial/diagnostic/repair1 logs retain four zero-height message viewport
  failures. Repair2 retained four unreachable nested-scroll action failures.
  Repair3 reached both actions but observed Queue before its asynchronous reload.
- `app-Debug-transaction-layout`: 82 passed, including four complete-window
  theme/size cases, actual keyboard controls and six forced authorization races.
- Latest focused `infrastructure-Debug-transaction-final`: 43 passed. Latest
  `app-Debug-transaction-final`: 12 passed, including eight post-precheck project,
  grant, credential-revision and binding races plus four complete-window cases.
- `structure-audit-final.json`: 2,260 C# files, 20,538 methods/accessors/lambdas,
  maximum file 496 lines, maximum method 78 lines, zero violations.

Headless screenshots cover 526×601 and 1280×820 in both themes, with four Queue
entries, two guidance receipts and a fourteen-line draft. A height-bounded composer
keeps message space below the floating context card. Stop and guidance controls
must scroll fully inside their ancestor viewport and remain keyboard reachable.
Long composer/history content remains scrollable. These are rendered Headless
frames, not native Windows interaction screenshots.

First full candidate Debug regression (`Full-Debug-current`, session 77358) is
terminal: 8,273 reported / 8,255 passed / 18 failed, no skipped or aborted tests.
Eight complete TRX reports and 2,485 source / 2,429 runtime hashes were reviewed,
with no identity drift. Failures comprise eleven missing publication-script
inputs, one Queue authorization notice, one obsolete Ctrl+Enter Queue contract,
one PR shutdown symptom and four project-root expansion workflows. Its failure
report remains historical evidence, not a result for a repaired candidate.

`candidate-r1` was copied with all 2,485 frozen source/config hashes checked, then
received two unchanged publication-script inputs and the notice repair. A
task-owned boolean carries a recognized authorization change through finalization;
Queue and guidance show the actionable scope error while remaining stopped.
No raw exception is displayed and no failed task is automatically continued.
Focused Debug checks now pass all 45 App and 11 publication-script cases, including
the original Queue failure. Publication tests use an isolated fake-dotnet sandbox,
not an actual release. The repair has 2,487 frozen source/config/script paths;
its 92 changed source/test paths are 53 additions and 39 modifications, no removals.
Structure audit: 2,260 C# files, 20,541 methods/accessors/lambdas, maximum 496/78,
zero violations. Its later full runs were explicitly stopped as superseded;
no full-run success in either configuration is claimed.

`candidate-r2` preserves the r1 snapshot and adds a reproduced history shutdown
repair plus the corrected keyboard integration contract. A blocked history read
holds the per-task operation gate; the production run-completion callback then
queues another refresh. Shutdown cancels that gate wait, which previously
escaped the loader's inner error handler and faulted task finalization. The
loader now covers registration/waiting as well as repository access, quietly
handles cancellation/disposal only during its stopped lifetime, and retains
localized retry state for ordinary failures. No run is replayed and no raw error
is exposed.

`app-Debug-history-repro-repair2` preserves the before-fix result: one passed,
one failed with `Task finalization failed` in the production App shutdown path.
`app-Debug-history-keyboard-repair2` then passed thirteen focused checks,
including that deterministic race, both PR context checks and actual composer
Enter/Ctrl+Enter behavior. This is controlled Headless/simulated-provider evidence;
it does not establish the precise cause of the old PR invocation retroactively.
Expanded failure-path and full-run evidence belong to this earlier checkpoint.
The r2 manifest contains 2,488 inputs and 94 changed paths
(54 additions, 40 modifications, no removals). Its structure audit covers 2,261
C# files and 20,566 methods/accessors/lambdas, maximum file/method 496/78 lines,
with no violations. At that checkpoint, no Steer source had been integrated into
the main checkout.

Both r2 expanded focused configurations subsequently passed 70 checks. A new
late-IO shutdown test nevertheless exposed a missing drain boundary: a read
error handled only outside the operation still faults `StopAsync`. The isolated
r3 pre-fix run (`app-Debug-late-read-repro-repair3`) preserves four passes and one
IO failure. r3 contains read failure inside its operation and gate cancellation
outside it, avoiding this regression without weakening ordinary failure hints.
`app-Debug-final-repair3` and `app-Release-final-repair3` each pass all 71 cases;
Release solution build has no warnings/errors. Structure remains 2,261 C# files,
20,568 methods/accessors/lambdas and maximum 496/78 lines, zero violations. The
source manifest still has 2,488 inputs / 94 changes. Full verification remains
required for this exact r3 identity; r2 success cannot validate r3.

The separate `process-probe` references existing candidate DLLs without rebuilding
src/tests. Its Debug r2 and Debug r3 runs each pass four actual owned-process
termination scenarios: project/projectless crossed with accepted-but-unapplied /
applied-before-held-response guidance. Production App DI, Agent, provider adapter
and SQLite recovery preserve the next flushed draft, close the old run, expose
the receipt and never automatically request a model. Pending input becomes
Interrupted without entering conversation messages; applied input stays once in
history. An explicitly entered new turn sends one new request and never reapplies
the old receipt. Parent and workers compare loaded App/Providers/Agent/
Infrastructure hashes with their supplied candidate directory. These are CLI
process checks with local simulated HTTP, no native window, tools, external side
effect or termination inside a transaction.

The first r3 Release process run passed three cases, then timed out waiting for
the second request. Read-only inspection found completed execution with guidance
already applied: the harness had not required the initial provider request to
arrive before accepting input. A parent/worker stdin handshake now waits for the
held first HTTP request, then permits acceptance, and only releases it after the
durable receipt. Both r3 gated configurations pass all four cases. The failed
log and metadata-only failure inspection remain; no product policy was weakened.

## Current repair and validation gate

r4 addresses the four unchanged project-authorization tests, reproduced separately
as four failures on r3 Release. Exact steering authorization checks were running
on empty mailboxes and stopping the ordinary frozen-scope Agent when project
roots were only added. App now permits this compatible change only for an empty
boundary, retaining the original tool roots. Actual acceptance and pending-input
application remain exact-snapshot operations. Root removal/replacement, primary
directory changes, deleted projects and source/worktree access remain refused.
The original four UI tests keep all assertions, including path_denied, rejected
approval/audits, separately authorized future tasks and no replay on reopen.

The current 2,490-input manifest has 96 changes (56 additions, 40 modifications,
no deletions). Structure covers 2,263 C# files / 20,589 methods/accessors/lambdas,
maximum 496/78 lines, zero violations. Debug related App checks pass 79 cases,
followed by 69 expanded cases including the two new root-expansion guidance cases.
Release related App checks pass all 81 cases. Both Infrastructure configurations
pass 45 steering cases. Release child-file-change checks pass six cases.
The two configurations' gated process probes each pass four cases, with matching
loaded production assembly hashes in `process-probe-reviewed-repair4.json`.

Six superseded r1/r2/r3 full runs were deliberately stopped after their confirmed
regressions, with executable paths/start times checked before terminating only
owned test/App and VSTest process trees. All six are terminal failures with
partial App reports; their reported pass counts are not full-regression results.
Source/runtime identity remains unchanged for each. Records are
`superseded-processes-stopped.json` and `superseded-regressions-review.json`.
Those runs also retain timeout, routing and one draft-selection failure not
reproduced in the current narrow checks; resource load is not a proven cause.

Independent r4 complete Debug/Release verification is now terminal and reviewed.
Each configuration has eight distinct Completed reports: Agent 160, Providers
617, Browser 104, Windows 148, Infrastructure 788, Tools 666, shared visuals 300
and App 5,499, totaling 8,282 passing cases. No case failed, skipped or aborted;
both build/test exit codes are zero. Both builds have zero warnings/errors.
Each run's 2,490 manifest inputs and 2,429 runtime files have identical
before/after path/hash sets; only generated PNG frames are excluded from runtime
identity. The five-minute per-test watchdog did not report a hang or memory dump.
These exact reports supersede no historical failure retrospectively.

`integrate-repair4.ps1` passed its read-only preflight, then rechecked both report
sets/hashes, current candidate source/runtime and all destination Before hashes.
Its Apply run integrated 56 additions and 40 modifications without deletions,
preserved the original 40 files under `before-integration-repair4`, and recorded
Integrated / zero rollback conflicts in `integration-repair4.json`. The original
2,434-input snapshot and Git status are preserved. The scope review confirms no
change to unrelated source/config/script inputs and all 40 backup hashes match.

Independent original-checkout Debug/Release solution builds have zero warnings
and errors. Each configuration passes all 147 focused cases: App 97,
Infrastructure 45 and Agent 5, with three distinct Completed TRX reports,
zero failed/skipped/aborted cases and exit codes zero. All 2,490 expected input
paths/hashes match the validated candidate and remain unchanged across these
checks. These focused runs are separate from the exact candidate full regression.
Reports/logs are prefixed `Integrated-Debug-repair4` / `Integrated-Release-repair4`.

The separately copied `process-probe-original` references these built original
assemblies without rebuilding src/tests. Its first diagnostic build attempt
failed: its App path omitted the `win-x64` directory, and concurrent restore
reported a file-already-exists error. Those build logs remain. After a separate
restore and serial configuration builds using the actual output directories,
both configurations pass all four owned-process termination/recovery cases.
`Integrated-process-reviewed-repair4.json` checks the four outcomes and loaded
App/Providers/Agent/Infrastructure hashes against current original outputs.
These checks use production DI/SQLite and local simulated HTTP, no native window,
real provider, tool side effect or termination during a database transaction.
No staging, commit, push, publishing, elevation or system ACL change was performed.

## Remaining scope

Delivery review on 2026-10-20 classified those 2,490 manifest entries as 2,455
source/config/script files and 35 old ignored build outputs under Application's
Artifacts subdirectories. Original manifests and hashes remain unchanged. Three
existing Application/Artifacts interface sources had also been unintentionally
ignored by Git; the delivery commit includes them and a narrow ignore exception,
while preserving generated-output exclusions. No product behavior changed. The
two interface EOF cleanups, separate PID test cleanup and focused reruns are recorded in
`DELIVERY_STATUS_AND_ROADMAP.md`, not retroactively part of the frozen candidate.

- Native Windows interactions, real provider responses and termination during a
  database commit or tool side effect remain unverified. Controlled CLI process
  recovery above does not establish those paths.
- Guidance currently accepts text only. Attached files remain in the draft with
  an explicit Queue alternative. User-configurable default/alternate submission
  behavior remains missing; the full reference feature is still partial.
- Command access outside authorized roots remains a separate unresolved product
  safety blocker. This candidate does not change Windows isolation or ACLs.
