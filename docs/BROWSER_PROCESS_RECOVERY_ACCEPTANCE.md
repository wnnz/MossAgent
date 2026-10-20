# Managed browser host-process termination and explicit recovery

## Controlled actual-runtime check

artifacts/browser-process-recovery-20261019 builds an artifact-only JIT executable
against original App outputs without rebuilding the frozen source/test/runtime.
Two owned worker processes use independent generated roots, temporary SQLite and
managed Chrome profiles, direct browser settings and local deterministic pages.
Production App DI, ToolExecutor and AskEveryTime approvals remain active. No
provider, credentials, existing user profile/window or arbitrary CDP port is used.

Before termination, the target performs approved navigation, one real DOM click
and a snapshot that reads button "1". All three audits are complete. Its one user
message, task/project identity, managed profile and eight module hashes are saved.
The parent validates the exact new executable/profile and binds handles to eleven
descendants before killing only its own host process. This differs from killing
the whole process tree, which would not test automatic descendant cleanup.

## Observed result

runtime.log and case-159b4dfc91ac4321803f48569fbcb6e5/result.json record:

- All eleven bound target descendants exit within five seconds. No surviving
  descendant requires diagnostic cleanup before recovery.
- The other owned worker still returns its exact clicked page through a newly
  approved snapshot and then closes gracefully.
- A new executable process, using the same saved target data, restores the task
  as Interrupted and idle. Its original three completed audit CallIds and one
  user message remain unchanged before manual action.
- Four explicitly approved manual operations recreate the task's browser from
  its saved managed profile: navigate to a new unchanged page, snapshot button
  "0", click, and snapshot button "1". Seven audits in total are complete; the
  user message is not duplicated. Explicit cleanup releases the recreated session.

The recovered record verifies all eight loaded module hashes against the supplied
original App directory. App is
258440CD42E0BA015C0A0CA49537A7B27BBF72308612ABEE50A07EBEE66CBD86;
Browser is FEE25149AE1ECFA6BC4D1CDB357282AD0E27FD18A91EE22B5B64264D5EF7882D.
The harness build has zero warnings/errors; failed stages would remain as logs
with bounded finally cleanup restricted to owned workers and bound handles.

## Ownership and limits

Infrastructure already owns startup interruption; Browser owns managed sessions
and Playwright cleanup. This check changes no production port, policy, dependency,
schema or behavior. It adds actual process evidence to
BROWSER_OPERATION_CANCELLATION_ACCEPTANCE.md and QUEUE_BROWSER_RUNTIME_ACCEPTANCE.md.

This is a direct-tool CLI host-process restart, not native desktop interaction or
Agent/Queue continuation. The host is terminated after completed tool audits;
termination during browser creation or an unfinished page/network side effect is
not tested. It does not establish embedded WebView2, daily-profile/CDP recovery,
manual takeover, transport hangs, boot-time orphan handling after OS/power failure,
network confinement, command filesystem isolation or full-product completion.
Complete original regression 93473 has ended with 8,136 passed / 2 failed, 8,138
total and unchanged source/runtime identities. The freeze has ended; this probe
does not erase those failures. AUDIT_INTERRUPTION_RECOVERY_ACCEPTANCE.md adds a
later production Queue/Agent process restart during a held real-Chrome navigation,
including explicit unknown audit state and no replay before manual continuation.
