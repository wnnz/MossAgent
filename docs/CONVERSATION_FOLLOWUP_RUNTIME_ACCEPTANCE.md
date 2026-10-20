# Conversation follow-up Queue runtime acceptance

Status: the production App now connects Queue storage, preparation, commands,
shared controls and run completion. The overall conversation-follow-up scope is
still partial: Steer and default/alternate-mode preferences are not implemented,
and native GUI restart/live-provider acceptance remains open. Controlled CLI
process termination is recorded separately from native GUI acceptance.

## Ownership and behavior

Domain retains bounded immutable entry snapshots; Application's
`ConversationFollowupPreparer` rereads task, provider/model and agent availability,
checks saved model identity and reasoning, intersects image/file capabilities,
revalidates exact project/worktree roots through existing ports, checks source
review access and reads owned attachment snapshots. UI contains no new SQL,
filesystem, process or HTTP operation. Infrastructure remains schema 23; no new
migration or reinterpretation of old data was needed.

App composition registers the repository and preparer. Queue is available only
when its repository can atomically consume the same composer draft store. Enter
and Ctrl+Enter during a real run enqueue; Shift+Enter remains a newline. The
separate stop button still cancels. Each task has its own queue, preparation
cancellation and serialization gate. Preparation is registered before waiting
for the gate, so another foreground send cannot claim the task in that gap.

Enqueue saves attachment snapshots and atomically consumes only submitted draft
identities. New input is retained. Normal completion can advance only the chain
armed by an explicit enqueue on that owned run. Failure, cancellation, browser
cleanup failure, application reconstruction and shutdown pause continuation.
Appending to an old paused queue does not silently arm it. Restored entries need
manual continuation. Dispatch atomically removes the first entry and saves its
one user message and Running status; cancellation after that commit records
Cancelled without another provider call or putting the message back.

The queue shows saved model/reasoning and attachment counts and supports editing,
reordering, deletion, refresh and manual continuation. Editing uses a separate
buffer and the captured revision; conflicts preserve the buffer. Task switches
retain task-owned editors/drafts. UI publication uses the Avalonia dispatcher;
shutdown cancels and drains preparations before stopping owned runs. Controls
use existing wrappers and central appearance variants, with bounded scrolling.

## Failure discovered through the runtime checks

A controlled Responses `response.failed` previously disappeared in the mapper,
allowing the run to become Completed and advance its queue. It now becomes a
sanitized ModelFailed; `response.incomplete` is also a localized failure. Failed
streams stop before later text/tool events. Nullable/malformed error fields use
safe fallback text. The reference schema documents the nested `response.error`:
https://developers.openai.com/api/reference/resources/responses/streaming-events#response.failed
The official completion guidance also separates failed/incomplete streams:
https://developers.openai.com/siwc/token-sharing-open-source/models-and-inference#3-wait-for-completed-inference

The later completion slice also rejects empty HTTP 200 and missing-terminal SSE
EOF in Responses and Anthropic, and stops reading after completion/failure.
Compatible Chat and Gemini already reject missing finish signals. Responses'
explicit `[DONE]` compatibility is preserved. The bounded fixtures and queued-tool
negative paths are recorded in `PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md`;
they do not establish every live provider/proxy's behavior.

## Controlled evidence

The isolated source copy passed 215 App checks covering the Queue, model selection
and composer neighborhood, and all 607 Provider checks. Earlier failed runs are
retained: they revealed the missing response.failed mapping, a synthetic server
that stopped serving after a cancelled connection, two obsolete active-Enter
expectations and a two-editor test-fixture draft conflict. The fixture now drains
the first workspace before constructing the second; no conflict policy was relaxed.

After hash-checked merge of 39 files into the original dirty checkout:

- 21 Queue/runtime/safety/lifecycle/control checks passed in Debug.
- The six current control cases passed at 360/480/1000 widths and 600/800 heights,
  in light/dark variants, including project and projectless sessions. Four cases
  overlap the previous 21; these counts must not be added as unique coverage.
- Debug and Release complete solution builds passed with zero warnings/errors.
- Structural checks cover 558 changed/untracked handwritten C# files, 29 AXAML
  files and 3,070 methods/accessors; maximum method length is 78. Whitespace was
  repaired separately and its final result is recorded beside these checks.

These use production App DI, temporary SQLite/artifact directories, the real
provider HTTP adapter against a local synthetic server, and Avalonia Headless
controls. They prove saved model/reasoning and agent prompt reach HTTP, task and
draft isolation, edit/order/delete effects, cancellation/manual continuation,
provider failure, settings/root revocation, corrupted snapshot rejection,
revision conflict preservation, reconstruction without replay, and cancellation
after dispatch commit. They are not paid-provider, native picker/window, or real
process-restart acceptance. Rendered PNGs are Headless evidence; visual inspection
checks readability/overlap only, not pixel fidelity with Codex.

Artifacts: `artifacts/conversation-followup-runtime-20261019/` for original-checkout
builds, checks, screenshots and source identities; the independent-copy evidence
remains under `artifacts/queue-runtime-workspace-20261019/artifacts/`.

## Remaining work

Audit remaining malformed/live stream cases, queue/manual-send races and
source/worktree access changes at the dispatch boundary, native GUI restart and
long queues. Saved approval/denial/cancellation now have queued-tool evidence in
the completion slice. A later real Windows Queue process case confirms that
cancelling one task kills its parent/child tree while another task completes and
retains its draft; task-owned browser isolation, broader command/terminal cases
and real-provider text/image input still need verification. Implement Steer through
a safe acknowledged input boundary without cancelling/replaying tools. The open
Windows command filesystem-isolation blocker is unchanged. This slice does not
complete the product goal, approve a release or replace the functional matrix.

## Original-checkout Release and full-run status

The final original-checkout Release checks passed: 23 Queue checks, 607 Provider
checks and 147 Windows checks. Both full solution configurations passed with
zero warnings/errors after the final source changes. The structural/whitespace
check passed with no violations. That full Debug regression (session 14700) is
terminal: 8,070 passed, 15 failed, no skipped tests, 8,085 total. Its 2,401 source/
configuration and 2,035 Debug runtime paths/hashes were unchanged. These records
remain under artifacts/conversation-followup-runtime-20261019 and must not be
rewritten as a passing run.

That regression reported 14 visual selector failures (the Queue
adds another AppCard) and one attachment-navigation timeout. The latter did not
reach its HTTP-content assertions; it is not evidence of an attachment leak.
Its freeze ended after terminal manifests were verified. Ten explicit EOF/
completion, queued-tool and fixture paths were subsequently merged with zero
baseline conflicts; the original Windows PID fixtures were preserved. Both
solution configurations, Debug/Release App 38 and Providers 617, complete Debug
Visual 300 and four CLI process-recovery cases now pass under
artifacts/provider-stream-boundaries-20261019. Its complete Debug regression
(session 97039) is now terminal: 8,101 pass and one allocation assertion fails,
8,102 total, no skips. The prior visual/attachment failures did not recur. All
2,404 source/configuration and 2,035 runtime identities matched; the freeze ended.
The raw-counter failure and separate dispatch-validation candidate are recorded
in CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md; no complete-pass claim is made.

The subsequent seventeen-file dispatch integration is validated in the original
checkout: Debug/Release complete builds and each configuration's App 52 and
Infrastructure 723 checks pass, as do four rebuilt CLI recovery cases. The new
complete Debug regression, session 55845 under
artifacts/followup-dispatch-integration-20261019, is terminal: 8,121 pass and one
queued audit assertion failure, 8,122 total, no skips/aborts. All eight reports and
2,411 source/configuration plus 2,035 runtime paths/hashes were checked unchanged
before the freeze ended. The targeted audit assertion was subsequently corrected
without changing production execution or audit behavior, as recorded in
QUEUE_TOOL_AUDIT_ASSERTION_ACCEPTANCE.md. These results do not erase previous
failed full runs or establish a passing new complete regression.
See `PROVIDER_STREAM_COMPLETION_ACCEPTANCE.md` and
`CONVERSATION_FOLLOWUP_PROCESS_RECOVERY.md` for exact evidence and limits.

An independent artifact-only check now connects production Queue dispatch and
Agent to real managed Chrome using a deterministic loopback model service.
Cancelling one pending page navigation pauses only its task's Queue; a second
task retains its exact click approval, completes a real click/snapshot and keeps
its draft. New production-service composition retains both histories/drafts and
requires manual continuation without replaying the cancelled navigation. See
`QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md`; this is CLI/Chrome API evidence, not native
GUI, real-provider or EXE crash/restart acceptance. It does not change the frozen
meaning of regression 55845. After the later guarded cancellation integration,
the Queue/Chrome probe was repeated against original App assemblies; see
BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md for exact records and limits.
