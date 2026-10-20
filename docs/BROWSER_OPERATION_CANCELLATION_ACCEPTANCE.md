# Browser operation cancellation and timeout

## Confirmed production failure

The artifact-only CLI under artifacts/browser-timeout-runtime-20261019 uses
production App DI, ToolExecutor, AskEveryTime, explicit approvals, temporary SQLite
and its own actual managed Chrome profile. A local deterministic page withholds
the target button until a click has returned tool_timeout. The old Playwright
WaitAsync cancellation only ends the caller's wait: releasing the button then
executes the old click and changes the real page to "late clicked".
runtime-baseline.log retains that failure, completed audits and eight original
module hashes. No user browser, credentials, provider or existing file is used.
The baseline source copies are preserved beside the log.

Twelve deterministic API-proxy cases independently fail before the change:
cancelled click/fill/navigation/snapshot leave the owned runtime open, and an
already-cancelled token still enters the SDK. These are simulated SDK fixtures,
separate from the actual Chrome reproduction.

## Change and ownership

Browser owns PlaywrightBrowserOperation. Every public session operation now
checks cancellation/disposal before entering the SDK. On cancellation or pipeline
deadline, it invalidates the task session, awaits existing owned-resource cleanup
and observes the original pending task before returning. It does not detach a
live action by cancelling only its wait. Disposal remains idempotent and closes
only the managed context or task-owned CDP pages/popups before disconnecting its
runtime; it does not close a borrowed user context or another task's connection.
The standard executor still owns approval, timeout, bounded results and audits.

Normal operation failures retain their original failure. Cleanup failures are
observed after runtime disposal and pending-task observation; they are not
converted into successful cleanup. A stale session is refused. Explicit release
and a new task-session attachment use the existing manager/factory recovery path.
There is no automatic replay or replacement inside a failed tool call.

The seven-file guarded integration includes this helper/session, cancellation
fixtures, two existing CDP ownership tests adapted to require invalidation while
retaining other user/task pages, and the separate queued-write audit assertion
fix from QUEUE_TOOL_AUDIT_ASSERTION_ACCEPTANCE.md. Baseline hashes and original
file backups are in integration-manifest.json and baseline/. No migration, new
module dependency, process launcher, UI appearance or approval-policy change.

## Current evidence

The independent candidate passes Debug/Release complete solution builds with zero
warnings/errors, each configuration's complete Browser 104 and relevant App 212
checks. Sixteen added cases cover owned/borrowed task pages, four operation shapes,
pre-cancel refusal, awaited cleanup, stale refusal and cleanup/ordinary failure.
An initial expanded run retains fourteen failures: eleven missing copied publish
script fixtures, two old post-cancellation reuse expectations and one deliberate
cleanup failure rethrown by test teardown. The script was copied unchanged, the
two expectations now require invalidation plus user/other-task preservation, and
the deliberate teardown error is separately asserted and marked expected.
Debug-browser-final and Release-browser retain the complete passing runs.

runtime-candidate.log confirms actual Chrome returns tool_timeout without a late
click, refuses the stale session and permits an explicitly reattached session's
new approved click. All seven calls have completed task audits and approvals;
eight loaded module hashes match the supplied independent App directory.
queue-runtime-candidate.log repeats Queue/Agent navigation cancellation with a
concurrent task's exact approval/run/draft intact and manual reconstruction, using
simulated model HTTP and actual Chrome. The structural check covers nineteen
selected candidate/harness C# files and 110 methods/accessors, maximum 56 lines,
one type each and no violations.

Original regression 55845 has now terminated: 8,122 total, 8,121 pass, one queued
audit assertion failure, no skips/aborts. All eight reports and 2,411 source/
configuration plus 2,035 Debug runtime paths/hashes match. The old failure is
retained and its freeze ended before seven explicit files were merged with zero
baseline conflicts. Original-checkout Debug/Release builds pass with zero warnings
and errors, and each complete Browser suite passes 104 checks. Related App checks
pass 212 in Release; the first Debug run passes 211 and fails one Queue agent
snapshot completion assertion. That failed report is retained and investigated
separately in QUEUE_SNAPSHOT_COMPLETION_ACCEPTANCE.md; no full-pass claim is made.

runtime-original.log now repeats the actual Chrome timeout/stale-session/explicit
recovery workflow against the original App assemblies: no late mutation, stale
refusal, successful newly approved recovery and seven completed task audits.
queue-runtime-original.log also repeats Queue/Agent cancellation, other-task
approval/draft preservation and manual service reconstruction. Module hashes in
both records match the supplied original directory. The first harness rebuild
failed because its default compile glob included baseline C# backups. The harness
now excludes baseline/**/*.cs without deleting those backups; both the original
failure log and corrected build/runtime records remain preserved. These are CLI
checks with real Chrome, separate from native GUI and paid-provider acceptance.

The subsequent guarded test-only completion-observation correction passes both
original configurations' full builds and expanded App 212 checks. Complete Debug
regression 93473 under artifacts/browser-cancellation-integrated-20261019 has ended:
8,136 passed / 2 failed, 8,138 total, no skipped/aborted tests. Eight reports and
2,414 source/configuration plus 2,035 runtime identities match; the freeze has
ended. The two preserved observation failures and guarded test-only fixes are
documented in RUNTIME_OBSERVATION_BOUNDARIES_ACCEPTANCE.md.

## Limits

This prevents a still-pending SDK action from continuing after reported completion
of cancellation/timeout cleanup. It cannot roll back a page action or network
request already completed before cancellation. Cleanup drains SDK operations;
creation/transport hangs and process-crash recovery remain separate acceptance
work. Production CDP creates task-owned pages; arbitrary user-page attachment is
not authorized by this change. Stale-session refusal currently uses tool_failed;
a more specific localized manual-recovery hint remains an interaction gap.
Native GUI, daily-profile/CDP live combinations and real AI providers are not
validated by these CLI/proxy checks. Command filesystem isolation remains an open
safety blocker; the whole product goal is not complete.

BROWSER_PROCESS_RECOVERY_ACCEPTANCE.md subsequently adds one bounded actual
host-process termination/restart case after completed browser operations. It
establishes automatic cleanup of the eleven bound managed-browser descendants,
independent-instance preservation and explicit newly approved recovery for those
module identities. Creation/in-flight-side-effect crash and native/Agent recovery
remain outside that check.

`AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md` subsequently adds actual production
Queue/Agent host termination during a held navigation and manual continuation in
a new executable. Schema 24 preserves uncertain audit results and presents an
explicit interrupted state; it does not determine whether a remote side effect
already occurred or establish other browser/native/live-provider combinations.
