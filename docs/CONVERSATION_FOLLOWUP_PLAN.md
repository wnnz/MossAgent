# Running conversation follow-ups

Status: Queue storage, production composition, commands and shared controls are
connected and have controlled settings-to-request-to-history evidence. Overall
follow-up parity remains partial: remaining live/malformed stream behavior,
native/live acceptance, default/alternate preferences and Steer are still open.
Responses/Anthropic missing-terminal failures and queued-tool approval boundaries
have now been integrated; see `PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md`. See
`CONVERSATION_FOLLOWUP_RUNTIME_ACCEPTANCE.md` for current evidence and limits;
`CONVERSATION_FOLLOWUP_STORAGE_ACCEPTANCE.md` preserves the earlier checkpoint.

## Reference and observed gap

The official [steering and queuing reference](https://learn.chatgpt.com/docs/prompting#steering-and-queuing)
was read on 2026-10-19. Steer adds input to the current run; Queue retains input
for the next run. Desktop queues support editing, reordering, sending and
deletion, with a default behavior and an alternate shortcut.

`WorkspaceViewModel` retains a separate stop action; the composer now routes Enter
and Ctrl+Enter to Queue during a real run. `WorkspaceRunState` owns cancellation and the selected
provider/model, while `AgentRunner` streams a provider turn and executes tools
before starting its next turn. There is no task-owned follow-up input port.

## Required design checks

1. Domain owns bounded follow-up identities, task ownership and durable state.
   Application owns queue persistence and live-input ports; Agent consumes input
   through those ports. Infrastructure handles SQLite and forward migrations;
   App handles presentation without SQL, files or provider HTTP.
2. Reuse task cancellation, conversation attachment snapshots, model selection,
   scoped tool execution and atomic submission. Split send coordination out of
   the already 500-line WorkspaceViewModel before adding responsibilities.
3. A queued entry must retain its intended model/reasoning, selected agent,
   attachments and authorization scope. Revalidate provider/model availability
   and roots/grants before dispatch; never silently select another model or
   expand projectless/source-review tool permissions.
4. Queue claims, committed messages and draft consumption must be atomic and
   revision checked. Normal completion can dispatch an explicitly queued next
   message once. Failure, cancellation and application restart leave remaining
   entries visible and require manual continuation; never replay interrupted
   side effects. Scheduling and other conversations keep their own state.
5. Steer needs a safe live-input boundary, bounded buffering and persisted
   acknowledgement. It must not mean canceling and replaying a run or tool.
   Preserve tool call/result ordering and append input before its next model
   request. Closing, stopping and disposing must drain owned operations.

## Acceptance order

- First deliver a real task-owned Queue path: enqueue while streaming, preserve
  the next draft, edit/reorder/delete, and dispatch once after successful completion.
- Verify settings/model/agent/attachment data through actual controlled provider
  requests, plus concurrent tasks, negative authorization, conflicts, cancellation,
  failure and independent persistence reopening with no automatic replay.
- Then deliver Steer through the Agent input port and prove that a controlled
  tool runs once while later guidance reaches the next appropriate model turn.
- Use existing shared controls/resources for keyboard and theme/viewport checks.
  Retain an explicit missing status for either behavior until its full path is
  implemented. Complete relevant tests, solution build and frozen full regression;
  keep native Windows and live-provider evidence separately labeled.

## Verified Steer semantics (2026-10-19)

The official [App Server reference](https://learn.chatgpt.com/docs/app-server)
defines `turn/steer` as appending user input to an active in-flight turn. It
requires `expectedTurnId` matching that active turn, refuses an idle thread,
does not emit a new `turn/started`, and accepts no turn-level model, cwd,
sandbox-policy or output-schema overrides. This is a reference contract, not
an implemented MossAgent API or evidence that ordinary provider HTTP supports
mutating an in-flight request.

MossAgent must bind accepted guidance to the captured task/run identity and
persist an acknowledgement before showing it as accepted. Completion, stop,
shutdown and concurrent submissions must serialize with that acceptance. A
stale or idle run is refused without silently queueing or starting another run;
the user's input remains available to recover explicitly. Accepted text must
reach the next safe model-input boundary in the same run without replaying
prior requests or tools. Model/agent/root/approval selection stays immutable;
configuration changes require a separately selected subsequent run.

For direct provider streams, define the safe injection boundary and explicitly
state when the guidance takes effect. Do not interrupt a stream and replay its
side effects to imitate immediate input. Test acceptance while streaming and
while a tool/approval is pending; run-identity mismatch, completion/acceptance
races, concurrent tasks, duplicate submissions, failure/cancellation and restart.
Assert one run identity, ordered tool calls/results, one persisted input and
controlled-provider receipt. A failure after acknowledgement remains visible
and never automatically replays guidance after restart. Native input/status and
live-provider behavior need separate acceptance. Steer remains unimplemented.

## Storage foundation (2026-10-19)

The foreground send flow now captures a `WorkspaceSendRequest` and delegates
preparation/start to `WorkspaceSendCoordinator`, preserving the existing draft
transaction, post-commit cancellation and task-owned execution behavior. This
reduces the workspace view model before adding queue responsibilities.

Schema 23 adds task-owned, revision-checked follow-up queues. Domain records retain
the selected model parameters, agent identity/prompt, approval policy, project/
worktree/root snapshot and file-backed attachment references. No provider keys or
attachment contents are added to queue payloads. Saving can consume a matching
task draft in the same transaction. Editing/reordering/deletion use the expected
queue revision; empty queues retain that revision to prevent stale resurrection.

The repository's dispatch operation atomically commits only the first queued
user message, Running task state and queue removal. Reusing a committed message
identity is refused. It invokes no provider, tool or process and is deliberately
separate from execution. At that storage checkpoint it was not registered in App.
The current runtime coordinator now performs model/agent, authorization,
source-review, worktree and attachment checks and handles committed cancellation
without replay. Controlled runtime evidence is recorded separately; persistence
tests alone do not establish those obligations or native acceptance.

The initializer preserves pending entries, marks committed Running tasks as
Interrupted and does not dispatch anything. Queue eligibility after successful
completion must be owned by the live run; failed/cancelled/restarted tasks require
an explicit manual continuation. Artifact retention/collection after deleting a
queued entry remains to be designed without deleting shared or committed data.

## Runtime integration gate

Automatic continuation must be attached to an explicitly queued live run, rather
than inferred from a nonempty database queue. A restart, failure or cancellation
must leave that eligibility unarmed. An ordinary later message must not silently
resume old pending entries. Reconcile enqueue/completion races using the captured
run identity and committed task status, with a single task-owned dispatch operation.

Before committing dispatch, reload the saved provider/model/agent identities and
current project/authorization/worktree/source-review state. Preserve the queued
model parameters and prompt; refuse missing/disabled selections or revoked/changed
authorization without selecting a substitute. Validate attachment contents and
model compatibility. Use the queue revision observed during that validation for
the claim; do not execute a changed row based on an earlier validated snapshot.

Start the next run through the existing task runner/executor. A background queue
must update only its task's run/messages, preserving the foreground conversation,
model selector, draft and attachments. Completion notification must follow browser
release and removal of the previous run. Shutdown must prevent new claims and
drain owned in-flight preparation; cancellation after the database commit must
record the committed task as cancelled/interrupted, without issuing a provider
request or restoring the message to the queue for automatic replay.

Inline queue editing owns a separate local edit buffer. Revision conflicts keep
that input and expose an explicit refresh/resolve action. Reordering/deleting
compete with dispatch through the same revision, and must not hide a committed
message or change the active run's immutable selection. The current coordinator
and shared queue view implement this gate; dispatch races and native/live evidence
remain explicit verification gaps. Completion/queued-tool checks are tracked in
`PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md`; CLI process-termination recovery is
separate in `CONVERSATION_FOLLOWUP_PROCESS_RECOVERY.md`.

The dispatch-configuration race is now addressed through the prepared persistence
port and transaction-local configuration checks. It does not move filesystem or
provider I/O into SQLite transactions. `CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md`
records the controlled repro, refusal/rollback, explicit recovery and actual
Windows queued-process cancellation evidence. It leaves source/worktree and
post-commit change semantics, native GUI, live providers and Steer explicitly open.

`QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md` adds the production Queue/Agent to actual
managed Chrome chain, including navigation cancellation while another task keeps
its approval/run/draft and service reconstruction without automatic replay.
It retains explicit gaps for browser creation cancellation, process crash/restart,
embedded/external/CDP combinations, manual takeover and native/live acceptance.
