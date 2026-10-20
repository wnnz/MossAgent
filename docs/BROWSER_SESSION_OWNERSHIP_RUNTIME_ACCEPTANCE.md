# Managed Chrome session ownership runtime check

Status: a controlled CLI check passes through production App composition,
ToolExecutor, SQLite audit and actual managed Chrome/Playwright. It does not
exercise Queue/model calls, Avalonia windows, native keyboard/pointer interaction,
embedded WebView2, daily profiles or an existing browser/CDP attachment.

The separate JIT harness under artifacts/browser-ownership-runtime-20261019
references existing original-checkout assemblies; it never rebuilds src/tests.
It saves Chrome/Managed/explicit-direct browser settings in generated temporary
data, creates two persisted task identities and explicitly attaches their own
manager-created sessions. Only a bounded local HTTP page is used. No credentials,
existing tabs or user profiles participate. Cleanup releases only these sessions.

## Established behavior

- Both sessions navigate through the shared pipeline and show their own page.
- Task two creates a second tab; task one's real tab ID is refused by task two's
  browser.switch_tab execution, with tool_failed rather than a schema rejection.
- A real DOM button click changes task two's accessible button name to exactly
  "1"; the bounded local-page snapshot is retained in the generated case folder.
- Releasing task one refuses subsequent operations on its captured session.
  Task two's page and button change remain readable and its session closes normally.
- ReadOnly navigation is refused with capability_denied. AskEveryTime is unchanged:
  eleven operations receive explicit harness approval decisions. All twelve calls
  have completed, task-scoped audit records (four for task one, eight for task two).

The final record is runtime-identity.log. The earlier runtime-initial.log has a
weaker click assertion and covers fewer operations; runtime-final.log adds exact
button/tab checks. The final identity run also asserts the ReadOnly error code
and verifies all eight loaded MossAgent module hashes against the supplied
original App directory, avoiding stale referenced-assembly evidence. App:
22BD6CDF717B9CE31C83725C0AC5BB49F1F909F1A1A3861BBFDF5F1A03230CE1.
Browser: 67D540875E324C6E2F23A8C6B636CA8D0D842651973397173DD45CEBCACE45C2.
The complete module identities and probe source manifest are retained beside it.
The harness builds with zero warnings/errors and uses ordinary JIT.

## Limits and next step

This is one controlled runtime workflow, not twelve independent test cases or
a complete browser acceptance run. It is real Chrome API execution, separate
from Headless rendering, native GUI operation or a live AI-provider workflow.
The later QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md adds a separate production Queue/
Agent check with actual Chrome, cancellation during navigation, concurrent
approval/draft preservation and service reconstruction with manual continuation.
Browser creation cancellation, process crash/restart, embedded/external switching
and manual takeover still require their own end-to-end checks. No filesystem sandbox, arbitrary
network confinement or full product completion is established.

Original complete regression 55845 is terminal: 8,121 pass/one queued audit
assertion failure, 8,122 total, no skips or aborts. All eight reports and 2,411
source/configuration plus 2,035 runtime identities were checked before its freeze
ended. This record describes the earlier assemblies identified above. The later
BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md records guarded integration and new
original-assembly actual-Chrome timeout/recovery and Queue runtime checks; neither
changes the failed full regression into a pass.
