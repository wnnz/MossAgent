# Managed browser cancellation at profile creation

The artifact-only JIT probe under artifacts/browser-creation-cancellation-20261019
uses the original App outputs, production DI/ToolExecutor, temporary SQLite,
AskEveryTime and two separately owned tasks with actual managed Chrome. Its local
page counts only requests to the cancelled task's target path. No provider,
credentials, existing user profile/window or arbitrary browser attachment is used.

## Boundary and observed behavior

A FileSystemWatcher requests cancellation when the previously absent task profile
directory appears, at the factory's profile-create/SDK-launch boundary. The
pending executor is awaited using an independent watchdog. Cancellation does not
simply abandon the caller's wait. The completed result must be tool_cancelled
after exactly one explicit approval, with no request reaching the target page.

Before explicit recovery, the parent verifies its worker's exact executable,
process and generated root. Native process inspection finds zero Chrome processes
with the cancelled profile. The other task retains one browser root and seven
auxiliary Chrome processes and still returns its clicked button "1" through the
standard approved pipeline. Its page/run is not closed by the other task's
creation cancellation.

The parent then signals explicit recovery. Manager release and reattachment use
the same task/profile. Four newly approved calls navigate once to the previously
unreached local page, read button "0", click, then read button "1". The final exact
audit counts are five for that task, including the one cancelled creation, and
four for the surviving task. All nine audits are complete and all nine calls had
explicit approvals. Both sessions close; no generated-profile Chrome process
remains after worker exit.

runtime-final.log and case-10891144f3c74619af78ee36c657b3bc contain the result,
checkpoint counts and module identities. All eight loaded modules match the
supplied original App directory, including App
258440CD42E0BA015C0A0CA49537A7B27BBF72308612ABEE50A07EBEE66CBD86 and Browser
FEE25149AE1ECFA6BC4D1CDB357282AD0E27FD18A91EE22B5B64264D5EF7882D.
Build has zero warnings/errors; original source/test/runtime files are not rebuilt.

## Preserved failure and limits

runtime.log retains the first harness failure: it expected exactly one Chrome
process containing a profile path, but Chromium propagates that argument to its
auxiliary processes. Its checkpoint already records zero cancelled-profile
processes and eight survivor processes. The corrected check requires zero for
the cancelled profile and exactly one main browser root, counted separately from
auxiliaries; it retains the original script and manifest. No production behavior
or approval policy changed for this correction.

Browser retains ownership of creation/cleanup; the existing shared pipeline owns
approval, deadline/cancellation, output limits and audit. This adds a real runtime
schedule to the deterministic API-proxy creation tests. It does not identify every
SDK-internal creation instant, indefinite transport hangs, cancellation during an
already-completed network/page side effect, embedded/daily/CDP modes or native GUI
and Agent/Queue continuation. BROWSER_PROCESS_RECOVERY_ACCEPTANCE.md separately
checks host termination after completed tools. Neither establishes command
filesystem isolation, all browser recovery cases or full-product completion.
