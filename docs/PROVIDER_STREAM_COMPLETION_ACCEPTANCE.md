# Provider stream completion and queued tools

Status: ten hash-checked files have been merged into the original checkout with
zero baseline conflicts. Original-checkout integration verification passes; the
subsequent complete Debug regression is terminal with one allocation-test failure.
This is a failure-handling slice, not full provider or Queue acceptance.

## Confirmed behavior and change

Responses and Anthropic previously ended their iterators silently after empty
HTTP 200 bodies or SSE EOF without a protocol completion signal. Agent therefore
could mark the task Completed, execute collected tool calls and advance Queue.
Gemini and compatible Chat already reject missing finish signals.

The adapters end Responses/Anthropic iteration on ModelCompleted or ModelFailed;
EOF without either becomes localized incomplete_response failure. Responses'
existing explicit [DONE] compatibility is retained. Stream failures pass through
the existing error sanitizer and Agent failure path. Agent already collects tool
calls until the model stream ends and discards them on failure; no new tool path
or retry is introduced. A manually continued queue sends its next uncommitted
message, without replaying an incomplete model tool call.

## Design self-check

1. Providers owns protocol completion, using the existing IModelProvider events.
   Application/Agent/UI dependency direction and database schema stay unchanged.
2. Existing cancellation, SSE limits, error sanitization and HTTP lifecycle remain.
   The ordinary tool pipeline owns authorization, approval, output and audit.
3. No new filesystem or external service authorization is granted. Test files
   live only in generated temporary roots. FullAccess is tested explicitly to
   prove an unterminated call cannot execute even when its write would be allowed.
4. New types each have their own file; modified providers remain small. No visual
   control or production command policy is changed by this candidate.
5. Deterministic fixtures cover empty/partial streams, terminal events followed
   by late text, real App failure/pause/manual continuation, pending drafts,
   saved approval policy, denial, approval, cancellation and incomplete calls.

## Evidence so far

The independent copy is artifacts/queue-runtime-workspace-20261019; evidence is
its artifacts/provider-terminal-boundaries directory.

- Original adapter checks: eight cases, four passed and four failed. The failures
  were Responses/Anthropic empty and partial responses.
- Forced-rebuild original App checks: all three new cases fail. Empty/partial
  responses produce a second HTTP request without manual continuation; the
  unterminated tool call actually creates its temporary file. Reports retain
  these direct failed assertions and an earlier Failed-versus-Completed check.
- The first original-App attempt reused a candidate DLL because the copied
  source retained an older timestamp. That three-pass report is invalid and
  explicitly excluded. Later comparisons force --no-incremental and record the
  provider binary hash. Candidate source restoration has zero hash differences.
- Earlier candidate checks passed 38 Queue/attachment cases. Two intervening
  failures were incorrect test expectations for ReadOnly audit codes/approval
  outcomes, repaired against the existing executor; no policy was relaxed.
- Forced-rebuild frozen candidate: 617 Provider and 300 Visual cases passed;
  App passed 37 and failed one assertion that read the queue during its transient
  refresh. The tool file stayed absent and the task was Failed. Its wait now
  requires the completed paused presentation, while still exposing old Completed
  behavior directly. The 2,399 source files and 1,041 selected Debug runtime files
  had identical paths/hashes before and after that run. Preserve its failed TRX.

The earlier original full regression is terminal: 8,070 passed and 15 failed out
of 8,085, with no skipped tests. Its 14 obsolete visual selectors and one
attachment-navigation timeout remain preserved. All 2,401 source/configuration
and 2,035 Debug runtime paths/hashes were unchanged across that run. The visual
fixture now selects composer-surface and supplies unavailable Queue state instead
of leaving bindings unset. All 22 compact layout/theme/scale/focus cases passed,
retaining original readable-width and geometry assertions; PNGs were inspected.
The attachment test passed eight cases in isolation and twelve two-case replays.
It now explicitly awaits draft readiness before sending; the historical timeout's
exact cause remains unconfirmed. No attachment-leak result was observed.

## Acceptance limits and next steps

The final independent candidate passes complete Debug and Release solution builds
with zero warnings/errors. Debug and Release each pass 38 Queue/attachment cases;
Release Providers passes all 617 cases. The prior frozen Debug Provider/Visual
results remain applicable: their sources did not change. Ten integration files
and 1,071 selected Debug/Release runtime files have zero before/after hash/path
differences. Outside those ten paths, comparison against the original manifest
finds only the intentionally older Windows PID fixture and missing new PID test;
neither will be copied. Candidate-source-ready.json and the guarded merge script
are prepared in artifacts/provider-stream-boundaries-20261019.

A final history-retention assertion also passes in both 38-case App selections:
partial received assistant text remains persisted after failure, while an empty
response does not invent that text. Both solution builds pass again. This latest
retention checkpoint has unchanged hashes for its ten integration files and 914
App Debug/Release runtime files; retention-terminal.json is its exact record.

The guarded merge completed against integration-baseline.json; its ten paths and
hashes are recorded in artifacts/provider-stream-boundaries-20261019/merged-files.json.
Original-checkout Debug/Release solution builds pass with zero warnings/errors;
Debug/Release each pass App 38 and Providers 617; complete Debug Visual passes
300. The rebuilt CLI harness also passes all four process-recovery cases against
the original checkout's loaded assemblies. The validation script is terminal
with all nine stages successful; see validation-terminal.json in that directory.
Structural checks cover 563 changed/untracked C# files, 29 AXAML files and 3,093
methods/accessors with no violations (maximum method length 78); whitespace passes.
These new results do not change the earlier full regression's failures. Preserve
every failed and invalid report. Current evidence is synthetic
HTTP, temporary SQLite/files and Avalonia Headless, not a paid provider, native
window interaction or OS process restart. Browser/process ownership through Queue,
dispatch-time authorization races, Steer and native/live acceptance remain open.
Windows command filesystem isolation remains an unresolved product safety blocker.

The subsequent complete Debug regression (session 97039) is terminal: 8,101 pass,
one fails, no skips, 8,102 total. All eight reports have been checked. The only
failure is the raw-counter WorkspaceTaskLookupOptimizationTests case (3,480 bytes);
its exact cause remains unconfirmed. The previous 14 visual failures and attachment
navigation timeout did not recur. All 2,404 source/configuration and 2,035 Debug
runtime paths/hashes were unchanged; its freeze has ended. Records remain under
artifacts/provider-stream-boundaries-20261019, including regression-summary.json.
The independent dispatch/allocation candidate is tracked separately in
CONVERSATION_FOLLOWUP_DISPATCH_ACCEPTANCE.md and is not part of this frozen run.

`CONVERSATION_FOLLOWUP_PROCESS_RECOVERY.md` adds four identity-checked CLI process
termination/recovery cases against this same independent candidate, covering
project/projectless sessions before and after Queue dispatch. They pass without
automatic replay and are explicitly separate from native GUI restart acceptance.
