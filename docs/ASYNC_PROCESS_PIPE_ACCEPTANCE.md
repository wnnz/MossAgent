# Asynchronous Windows process pipes and deterministic proxy evidence

## Scope and design checks

Platform.Windows owns the operating-system transport behind the unchanged
`ITaskProcessLauncher`. Parent stdin/stdout/stderr now use truly asynchronous
named-pipe server endpoints, rather than asynchronous emulation over synchronous
`CreatePipe` handles. Child endpoints stay synchronous and inheritable for
console/stdio compatibility; parent endpoints are not inheritable. Ownership
transfers the actual streams to readers/writers without reopening handles.

Each byte-mode pipe has an unpredictable GUID name, first-instance protection,
one instance, CurrentUserOnly access, 4096-byte buffers and a bounded local-client
connect. The launcher still uses its explicit inherited-handle allowlist and
task-owned kill-on-close Job. Shell, Git and stdio MCP keep their existing ports,
authorization, approval, timeouts, cancellation, bounded output and audit pipeline.
No ThreadPool tuning, global cache, new application dependency or database change
is introduced. Obsolete CreatePipe/security-attribute interop is removed.

The unrelated SOCKS5 fixture race is fixed only in shared test infrastructure:
publish the rejection decision before making its response observable. Production
proxy authentication, routing and security assertions are not weakened.
Eight changed/new C# files each contain one type, are at most 107 physical lines
and have no trailing whitespace. Existing test deadlines/parallelism stay intact.

## Deterministic red/green probes

External scripts and output remain outside the repository:
`%TEMP%\MossAgent-test-stability-20261015`.

- The old-binary SOCKS5 probe suspends a rejected response write after the client
  can observe it. Both missing-method and wrong-password cases report rejection
  count zero at that point and one only after return; exit 1. The final candidate
  reports one at both checkpoints; exit 0. Five new deterministic cases also cover
  success, malformed greeting and failed response-write/exactly-once publication.
- The old-binary process probe has two idle reads and an unrelated queued marker
  inside an isolated subprocess with at most two workers. Both parent streams are
  synchronous, workers are exhausted and the marker cannot run before pipe closure;
  exit 1. Final candidate streams are asynchronous, both reads remain idle, both
  workers remain available and the marker runs before closure; exit 0. Only this
  disposable probe changes its own worker limits, not the application or suite.
- Five new Windows pipe cases check both directions, Unicode, child-only handle
  inheritance, parent-only async I/O, pending-read cancellation and retry,
  full-buffer write cancellation without child reads, and transferred ownership.

Logs preserve `baseline-*-probe.log`, `fixed-*-probe.log` and
`fixed-final-*-probe.log` separately. This proves a worker-starvation mechanism,
not that every historical timing failure had the same exclusive cause.

## Production composition and native restoration

Fresh evidence: `%TEMP%\MossAgent-async-pipe-runtime-20261015`.
The accepted non-UI harness loads the candidate's real App composition through
reflection, without replacing any service. Formal repositories create one fresh
synthetic FullAccess project/task and one stdio echo-server profile. Existing
`WorkspaceRunExecutor`, AgentRunner, provider, tool executor, launcher, MCP manager,
audit and conversation persistence execute the scenario. No user policy or data
is edited. No terminal command is submitted through Windows UI automation.

The loopback Responses fixture discovers encoded tool names from actual requests.
One real `shell.run` emits stdout, stderr and Unicode, then one stdio MCP echo
returns its marker. Three model requests, two successful task-owned audits and
the exact persisted output strings are independently verified. No artifact is
created. This is real service/runtime execution, not a native composer-Send test.

The actual production App then opens the same data, expands persisted execution
history and both outputs, closes normally, reopens and repeats those disclosures.
Six passive Python/PrintWindow images cover Light/96 DPI at 1920x1032. Captures
bind the expected executable and PID, reject black/minimized output, and verify
ordinary permissions. No Codex UI or security/approval control is automated.

All nineteen table hashes, task/message/audit rows, script hash and the three
model requests remain unchanged throughout both native runs. Exactly one MCP
`tools/call` exists. The additional two initialization/initialized/tools-list
sequences are expected connection startup, not replay of the tool. Both App
processes exit zero with empty stderr; both owned MCP processes are gone. The
fixture exits normally after three requests. `verify.py` independently checks
the accepted evidence and candidate identities.

App SHA256:
`A5E5CE27C2D1DB93F6592B286DF59ABF407433080938064F4A26A060A014033F`.
Platform.Windows SHA256:
`A63FF27316A63A720219C2D9D340E2E2CF758750C1A8EE32D9922F067FC45E09`.

Two external harness setup failures remain separate: missing SQLite bundle
initialization, then a fixture incorrectly expecting an untruncated echo token
in a length-limited encoded name. The former has separate initialization data;
the latter retains its data/project/request logs. They are not accepted runs or
product fixes. The accepted run uses fresh data and actual MCP-prefix discovery.

## Build and tests

Candidate artifacts: `.scratch/async-process-pipes-20261015`.
Final solution build has zero warnings/errors. Scoped formatter verification,
tracked diff whitespace and explicit new-file type/size/whitespace checks pass.
Focused proxy/new-fixture cases pass 41/41; focused Windows cases pass 21/21 and
the complete Windows suite passes 142/142. These are not added to full-run totals.

The preceding intermediate complete run, with the fixture fix but old process
pipes, finishes 6,118/6,120: all 4,498 App cases pass, but two Windows PTY timing
cases fail. Its App worker computes for 11m40s; stack samples show different
Avalonia layout/test work, not an established deadlock. The original earlier
6,111/6,115 run and diagnostic reruns remain separate historical evidence.

The final candidate's complete solution test command exits zero: 6,125/6,125
across eight assemblies, with no failures, skips or aborts. The App worker finishes
in 12m25s; its late stack sample is still executing a different UI-layout test.

| Complete assembly | Passed |
| --- | ---: |
| App | 4,498 |
| App.Visual | 84 |
| Agent | 119 |
| Browser | 76 |
| Infrastructure | 199 |
| Platform.Windows | 142 |
| Providers | 516 |
| Tools | 491 |
| Total | 6,125 |

`verify-solution.py` independently validates all eight terminal TRX reports and
individual outcomes, records their test-binary hashes, matches App/Platform DLLs
between tests and native runtime, and checks both final deterministic probes and
the native audit. The complete run supersedes the earlier non-all-green gate;
focused/isolated runs are not counted again. Its output and
`final-verification.json` remain in the external evidence directory.

## Limits

This slice does not certify native composer Send, manual approval, live paid
providers, cross-user/remote named-pipe attack resistance, every concurrency
combination, DPI transitions or Codex pixel equivalence. Existing negative tests
and explicit current-user/handle restrictions remain, but do not replace those
native acceptance branches. Debug/JIT, JSON and Windows x64 single-file packaging
are unchanged. No default launch output, staging, commit, push, publication or
installation is changed. The complete product goal remains active.
