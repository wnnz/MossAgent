# Durable input for an active run

Status (2026-10-20): text guidance is integrated into the main checkout through
96 explicit hash-checked paths, with backups for the 40 modified files. The exact
`candidate-r4` passed all 8,282 tests in each complete Debug/Release run, with eight
completed reports per configuration and no source/runtime drift. Original-checkout
Debug/Release solution builds have zero warnings/errors; each configuration's
147 focused checks and four controlled process recovery cases also pass, with
unchanged source inputs and reviewed loaded-assembly hashes. This remains a partial reference
feature; attachments, configurable submission behavior and native/live acceptance
are open. `CONVERSATION_FOLLOWUP_PLAN.md` records the reference contract.
Evidence lives under `artifacts/conversation-steering-20261020`; earlier failed
and explicitly stopped candidates remain preserved. See
`CONVERSATION_STEERING_ACCEPTANCE.md` for validation scope and remaining gates.

## Ownership and boundaries

1. Domain owns immutable run/input receipts, bounded text and explicit states.
   Application owns persistence and live-input ports. Infrastructure owns a
   forward schema migration and atomic transactions; Agent owns safe injection
   boundaries. App owns draft coordination, keyboard commands and receipt display.
   No UI SQL, filesystem, HTTP, tool or operating-system dependency is added.
2. Reuse task identity, draft revision guards, messages, context budgets, existing
   cancellation, tool execution and task finalization. Input accepts text and the
   exact expected run ID; it accepts no provider/model/agent/root/policy overrides.
3. A per-run asynchronous gate must serialize durable acceptance, boundary
   application and closing. Cancellation stops new acceptance; already committed
   receipts remain visible. Stop/shutdown drain acceptance before finalization.
   A stale/idle run fails without Queue fallback, a new run or lost input.
4. Bound pending input to 16 entries, 32,768 UTF-16 code units per entry and
   256 KiB UTF-8 total; bound each history page to 64 receipts. Durable IDs make
   exact retries idempotent and reject identity reuse with different text/scope.
   Database operations run off the UI thread with a linked 30-second deadline.
   Store text/status only, never credentials or attachment bytes.
5. Each handwritten file retains one type and the existing file/method limits.
   Focused tests must cover concurrent accept/apply/close, stale identity, draft
   conflict/rollback, bounds, cancellation, migration retry and no replay on reopen.

## Persistence foundation

A run registration belongs to one existing Running/Waiting task. One open run is
allowed per task; a closed run ID cannot be reopened. Pending receipts are separate
from committed conversation messages. Acceptance and matching draft consumption
commit together. A duplicate receipt returns its original acknowledgement without
consuming a newer draft; a mismatched duplicate fails.

At a safe boundary, a revision-checked pending prefix becomes user messages once,
after any preceding unpersisted assistant text. The same transaction updates its
receipt states. It never runs a provider or tool. Acknowledgement means durable
acceptance; boundary application means local model-input preparation, not proof
of server receipt or successful model processing. Recovery closes abandoned open
runs and marks unapplied input Interrupted in the same transaction as task/audit
recovery. It never inserts messages, reopens a run or dispatches work automatically.

## Runtime integration gate

Storage is insufficient for shipping. Start the mailbox only after capturing the
run's initial history, and require the captured task/run identity on every UI
submission. Use the same run, model, agent, tool context and cancellation token.
Apply input before a subsequent model request, only after all results for the
current assistant tool-call batch. A stream with no tool calls must atomically
take pending input or seal acceptance before declaring completion; accepted input
can therefore produce another request in the same run without cancel/replay.

The existing context builder can clip recent user messages. Before committing
boundary application, prove that every newly accepted input is retained in full
in the next bounded request, or fail visibly without sending a clipped request.
Preserve provider continuation/tool-result ordering. A tool, request or browser
side effect must never be repeated to simulate steering. Failure/cancellation and
turn-limit exhaustion retain actionable receipt states; reopening is read-only
until an explicit user action, with no implicit replay into a later run.

## Required evidence before integration

- SQLite success/denial/failure/rollback/concurrency and forward migration tests.
- Agent deterministic streams with input accepted during streaming, approval and
  tools; one run ID, one original tool execution and ordered provider receipt.
- Real App DI with simulated HTTP and actual file tools/audits, task switching,
  competing drafts, cancellation/failure, settings immutability and reconstruction.
- Shared localized controls, explicit next-boundary status and visible historical
  interrupted receipts; keyboard/theme/viewport checks and separate native/live
  acceptance. Relevant tests and Debug/Release complete solution builds.

Do not register a storage-only repository as a product capability or mark Steer
implemented. Command filesystem confinement remains an independent safety blocker.

## Shutdown and keyboard repair review

The second repair candidate was copied only after validating every one of r1's
2,487 source/config/script hashes. It changes App history loading and its focused
tests; it adds no dependency, database migration, command or tool permission.
History uses the existing per-task operation gate, cancellation lifetime, bounded
repository page and UI dispatcher. Both operation registration and waiting for
the gate must be covered by the history loader's error boundary. Expected
shutdown cancellation/disposal must not fault the run-completion callback; other
read errors retain the localized retry state. The same file/type/method limits
apply; the r2 structural audit reports no violations.

The deterministic production-composition test blocks an actual history read,
lets an actual simulated-provider run complete and queue its final history
refresh, then shuts down. Before the repair this reproduces `Task finalization
failed`; afterward shutdown drains both reads without another provider request
or changed Completed status. Independent IO, cancellation and disposal errors
outside shutdown must remain visible and explicitly retryable. This reproduces
a cause consistent with the earlier PR test's symptom, not retrospective proof
of the original failure's exact exception.

The existing composer integration test must distinguish Enter Queue from active
Ctrl+Enter Steer. Keep actual focused keyboard input, task/run identity, no
cancellation, receipt persistence, provider input, once-only history insertion
and preservation of the next unsent draft. Do not remove the Enter Queue case.
Full independent Debug/Release gates, evidence identity and guarded integration
remain required after these targeted checks.

The subsequent late-read test forces an IO error after shutdown cancellation.
Moving only the error handler outside the operation allowed its drain task to
fault even though the loader handled the read error. r3 therefore keeps read
failure containment inside the operation as well as the outer gate-wait boundary.
The pre-fix r3 test run retains that failure; both configurations then pass all
71 relevant cases, including both shutdown paths and non-shutdown IO/cancellation/
disposal recovery. The revised structure audit remains within all limits.

## Existing task authorization compatibility

The first full candidate regression finished with eighteen failures, including
four unchanged project-authorization workflow cases. A separate r3 Release run
reproduced all four: even an empty steering mailbox rejected an additive root
change before the ordinary Agent could exercise its original frozen tool scope.

r4 adds a separate read-only empty-boundary validator through the existing App
access guard. Application chooses it only after reading an empty pending mailbox
under the run gate. App permits an unchanged primary directory and a superset of
all original roots in this case; it never updates the task's tool context. Actual
acceptance and nonempty application keep exact authorization equality and the
SQLite transaction guard. Removal, replacement, deletion, worktree invalidation,
source grant/account/binding changes remain denied. A concurrent mailbox revision
invalidates sealing and repeats validation. Existing callers without this optional
validator retain strict behavior. No approval policy, tool or path validator is
changed.

Keep all four original UI workflow assertions unchanged, including the attempted
new-root write's path_denied result, rejected approval, two audits, future task's
separate access and no replay after reconstruction. Additional App cases cover
pending/new guidance refusal after adding roots and preserved drafts; repository
session cases prove the empty guard cannot authorize acceptance or application.
The r4 audit reports 2,263 C# files, maximum file/method 496/78, zero violations.
