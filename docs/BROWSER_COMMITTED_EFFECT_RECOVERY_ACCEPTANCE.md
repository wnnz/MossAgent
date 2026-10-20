# Browser effect committed before host termination

Status: the original Debug and Release App assemblies pass one controlled
post-effect/pre-result crash timing. No production code changes in this check.
This extends schema-24 audit recovery evidence, not full-product completion.

## Actual workflow

The independent executable uses production DI, Workspace Queue, Agent, standard
tool execution, AskEveryTime approvals and temporary SQLite. A local simulated
Responses service returns a navigation followed by a click. Actual managed Chrome
loads a generated form. The separately approved click submits a POST; the local
page server increments and flushes a synthetic counter, atomically publishes its
file, and withholds the response. The navigation audit is complete/successful;
the click audit remains incomplete. The controller independently reads counter=1
and its hash before terminating only its exact owned host.

All eleven bound descendants exit automatically; the controller does not kill the
tree to manufacture that result. Another separately owned Debug instance retains
its clicked page. A new executable opens the same target database and restores an
idle Interrupted task, the pending Queue entry and the unsent draft. The click
audit presents 已中断 / 执行结果未知 with the instruction to verify actual effects.
No model or page request occurs automatically. Explicit Queue continuation sends
only the remaining prompt using the saved model/medium reasoning.

The durable effect stays exactly 1, with unchanged file hash, after restart and
after continuation. Model requests are 3 before restart/continuation and 4 after
manual continuation; the form is loaded once. Three user messages remain unique
and ordered. Both audit identities and every original result/approval field are
unchanged; only the incomplete click gains its first InterruptedAt. The completed
navigation gains no interruption marker. Continuation does not rewrite that first
observation, fabricate success or replay the click. Actual approval was observed
before execution; its incomplete persisted approval field remains unknown.

## Evidence

`artifacts/queue-browser-effect-crash-20261019` retains:

- Debug case `56ac81f91bb34ddf813b93341acb5e51`, `runtime.log` and `build.log`.
- Release case `eba8b78a3b3048dfa2f2c73c998e5fe9`, `runtime-release.log` and
  `build-release.log`. Both artifact executable builds have zero warnings/errors.
- Each case's controller result, bound descendants, before/after audit JSON,
  recovered presentation, durable counter, model/page statistics and eight
  actual loaded module hashes checked against its selected original runtime.
- Source provenance and structural review: twelve handwritten/linked C# files,
  one type each, no file/method size violation. No original source, configuration,
  tested Debug runtime, existing window, project or user profile is modified.

## Limits

This is actual Chrome and process termination/restart, production composition,
local simulated model HTTP and a synthetic flushed page-server effect. It is not
native GUI or live AI/provider/platform acceptance. One POST form with a withheld
response does not establish all browser action/download/creation/transport
timings, embedded/daily/CDP modes, OS power loss or command filesystem isolation.
The application correctly retains uncertainty; this probe's independent counter
must never be used to infer arbitrary interrupted tool results in production.
The simulated model deliberately emits no new action for the remaining prompt;
this proves that composition does not replay the interrupted call. Whether a live
model proposes another operation after manual continuation is separate behavior.
