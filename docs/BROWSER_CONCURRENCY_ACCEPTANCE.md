# External browser concurrency acceptance

## Plan and design self-check

- Previous goal work made concrete progress: search projection/subscription
  changes and current 4813-case regression evidence. The wider goal remains active.
- This slice verifies a remaining runtime boundary: two concurrent agent tasks
  using real external Chrome sessions, cancelling one without affecting the other,
  and releasing owned browsers/drivers after task completion and app shutdown.
- Browser owns session/profile lifetime; App keeps its actual composition root,
  MainWindow, settings, task navigation and agent runner. Existing tool contracts,
  provider protocol and approval/audit/output pipeline remain authoritative.
- Use fresh synthetic SQLite, authorized project roots, managed profiles and a
  loopback-only model/page fixture. Never attach user Chrome/CDP or daily profiles,
  change safety policies through UI, use paid endpoints, or log credentials.
- The repository-external host observes public view-model state and owns its
  synthetic setup only. Real task start, switching and stop must use normal UI.
  Host fixture gates may release synthetic provider responses; they do not invoke
  browser tools, execute models or change task state on the App's behalf.
- First verify simultaneous browser/process ownership and isolated local storage;
  then cancel A, execute further B browser tools, complete B and verify cleanup.
  Preserve failures, screenshots, audits and exact scope; fix genuine failures
  with focused negative tests before repeating the affected runtime scenario.
- No production feature is added merely for QA. Any required change must stay
  within file/type/method limits and shared UI wrapper/style boundaries. Keep JIT
  debug, Windows x64 single-file release and all unrelated dirty work unchanged.

## Native runtime evidence

On 2026-10-11, a repository-external ordinary-JIT host used the real App composition
root, MainWindow, browser settings and task controls at 1280 by 820 DIP. It reported
`QA_ELEVATED=False`; its build completed with zero warnings and errors. All pages
and synthetic provider responses stayed on loopback. No user database or daily
browser profile was opened.

- The real settings UI selected and saved Chrome. A read-only query confirmed
  `default_browser=1`, `profile_preference=0` (Managed), and no profile directory,
  CDP endpoint or proxy override. No security policy was changed through the UI.
- Real composer sends started A, then B without stopping A. Each task executed
  navigate, type, click, snapshot and screenshot through the standard tool pipeline.
  Both tasks remained Running with distinct Chrome/Node processes and managed
  directories named for their task identity.
- Same-origin pages displayed and stored `VALUE-A` and `VALUE-B` independently.
  The screenshot artifacts were actually opened and inspected, not merely hashed.
- The real A stop button cancelled A and released its Chrome/Node processes.
  B remained Running, using the same Chrome PID 20452 and Node PID 8244 with
  unchanged process creation times. No B restart or direct tool invocation occurred.
- Releasing only the synthetic response gate let B execute five more tools. Its
  second screenshot visibly showed `VALUE-B-FINAL` in the input and localStorage.
  A remained Cancelled. All 15 tool audits succeeded with approval granted; all
  three screenshot artifact lengths and SHA256 values matched persisted metadata.
- B completed through the normal runner, rendered its final reply and restored
  the send action. SQLite confirmed A Cancelled (5) and B Completed (3). No owned
  Chrome/Node descendants remained; the original four process IDs were absent.
- Request metadata recorded 6 A and 11 B turns, no invalid task route and no
  `VALUE-A` in any B request. Only route/value-presence flags were logged, not raw
  requests, API keys or headers.
- Normal host shutdown awaited `Workspace.ShutdownAsync()` and returned
  `QA_FINAL_ACTIVE=False`, `QA_EXIT=0`. The loopback fixture also exited normally.

Local, untracked evidence is under
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-browser-concurrency-20261011-145244`:
`both-running`, `a-cancelled`, `b-continued`, `b-completed` PNG/state/evidence sets,
three browser tool PNGs, bounded request flags and the isolated host build log.
The screenshots and Python/native QA helpers are not repository files.

## Limits

- On A cancellation, the fixture's keepalive write threw Windows
  `ConnectionAbortedError` (10053), outside its narrow disconnect counter handler.
  Thus its final `disconnections=0` is not proof that A stayed connected, and no
  disconnect signal was produced. Cancellation, browser cleanup and B survival
  are established independently by App/SQLite/process/artifact evidence.
- This native scenario precedes the sidebar reconciliation refactor. It is not
  post-refactor browser runtime acceptance. The later Browser test project passed
  76/76 and the solution build passed with zero warnings/errors. Separate native
  sidebar evidence is recorded in `SIDEBAR_RECONCILIATION_ACCEPTANCE.md`.
- No full-product completion, same-state Codex pixel equivalence, native multi-DPI
  acceptance, daily-profile/CDP validation or hostile-site compatibility is claimed.

## Current production App acceptance: 2026-10-12

The latest ordinary-JIT `MossAgent.App.exe` uses its unmodified production
composition root, not the earlier repository-external QA host. Its separate build
includes the sidebar, context and incremental timeline changes. Assembly SHA-256:

- App: `13DA45B41CB17628E0D65DBE3BD4A6BE39E6F6AAEA3DBE82BBF082988E3E6F38`.
- Agent: `5E3AD0FF3FCB6D16ACBE44A77F0D015A67FEA92C480B098B3B06DB8690197E85`.

The already-live older synthetic App was closed normally before starting this
build; its fixture had received no model requests or tool executions. The same
new synthetic SQLite store and authorized project roots were retained. Both tasks
already had FullAccess and Chrome/Managed configuration, so no safety policy,
permission request, user database or daily browser profile was changed through UI.
All provider/page traffic stays at `127.0.0.1:51792`; request logging remains a
bounded whitelist of task/model/effort and value/root-presence flags.

### Native execution and isolation

- Real composer sends start A with `concurrency-a`/medium and B with
  `concurrency-b`/high. Two independent stored conversation choices reach the real
  Responses adapter; the model fixture receives six A and eleven B requests.
- Both tasks simultaneously reach Running (1), each after navigate, type, click,
  snapshot and screenshot. Chrome roots 20580/24332 have separate task-named
  managed profiles; their Node owners are 23004/2320. The two same-origin screenshot
  artifacts visibly show independent `VALUE-A` and `VALUE-B` storage and inputs.
- The real A stop button transitions A to Cancelled (5). A's Chrome/Node disappear,
  while B remains Running with unchanged PID, creation time and managed profile.
  The fixture also records A's actual disconnect; its Windows
  `ConnectionAbortedError` branch is now handled rather than producing a traceback.
- Releasing B's synthetic response gate performs five further browser tools using
  those same process identities. Its third screenshot visibly shows
  `VALUE-B-FINAL` in the input and localStorage. A remains Cancelled throughout.
- B reaches Completed (3), renders its final reply and measured duration, collapses
  execution activities by default and restores the composer send state. All 15
  tool audits report success and approval granted. Three PNG lengths and SHA-256
  values match their persisted artifact metadata; all images were actually opened.
- B completion leaves no owned Chrome/Node descendants. Whitelisted request flags
  report no invalid route, foreign authorized root or A value in a B request.

### Restoration, presentation and cleanup

- Normal UI close releases the production App. Reopening the same synthetic data
  restores A Cancelled/B Completed, the final reply and its unchanged duration.
  Clicking the real duration disclosure expands all ten B activities while the
  final reply remains outside them. Model requests remain 17, tool audits 15 and
  screenshot artifacts three: no model/tool/browser replay occurs.
- The restored App's token is read using its explicit current PID 24204 and confirms
  `elevated=false`. The external token-check script initially ignored its arguments
  and reread a stale identity file; that helper was corrected outside Git. Only
  the explicit-PID restored token result is used as direct elevation evidence.
- Passive Python captures record running, cancelled, continued and completed
  light-theme views at 1280x820/96 DPI, plus restored collapsed/expanded views at
  maximized 1920x1032/96 DPI. The helpers do not send UI input; native interactions
  use only the supported Computer Use API against MossAgent.
- Final normal UI close leaves the App, fixture and all original browser/driver
  PIDs absent; App stdout/stderr and fixture stderr remain empty. A supplementary externally
  attached `Get-Process` observer returned a null exit code, so zero App exit status
  is not asserted. Cleanup is established by process, task, audit and artifact
  evidence instead. Fixture final counts are A=6, B=11, pages=2, disconnections=1.

Evidence and helpers stay outside Git in
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-production-concurrency-20261012-195930`.
`runtime-identity-latest.json`, `permission-restored.json`, stage evidence JSON,
passive PNGs, three browser artifacts and `requests.jsonl` retain exact scope.
No production exception, new feature, schema change or packaging change was needed.
The older host evidence above remains historical, not a substitute for this run.
The current Browser suite passes 76/76 and Agent suite 119/119, including their
ownership, cancellation and context regressions. Another isolated solution build
passes with zero warnings/errors; repository and scoped whitespace checks pass.
This closes the current production two-task browser cancellation gate; it does not
prove full same-state Codex fidelity, dark/narrow state coverage, daily profiles,
arbitrary sites or the whole product objective.
