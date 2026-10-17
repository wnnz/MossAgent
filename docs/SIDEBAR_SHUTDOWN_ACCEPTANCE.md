# Sidebar storage ownership during shutdown

## Design self-check

1. App owns view-model lifetime and shutdown coordination. Dependency direction
   remains unchanged, and no platform-specific code is added to App.
2. Existing `ISidebarPreferenceStore` and guarded load/sort/move/reorder commands
   retain their behavior. No new port or service is needed.
3. Shutdown retains the existing linked cancellation and ten-second operation
   deadline. It now waits for canceled storage operations to release their owned
   resources instead of treating cancellation alone as completion. There are no
   new capabilities, approval changes, persistence formats or migrations.
4. Production view models remain below the hard file/method limits. Every added
   C# file declares exactly one type; no partial class split is introduced.
5. Production-workspace tests cover shutdown and async disposal for initial load,
   retry, both sort modes, both move commands and drag reorder. A real synthetic
   file lock proves resource ownership; negative checks cover blocked commands,
   canceled writes, late results, source changes and observer notifications.

## Confirmed issue and correction

- Fourteen new cases fail against the previous implementation. Graceful shutdown
  reports completion without canceling sidebar storage, while async disposal
  cancels it but reports completion before the storage operation releases its
  exclusive file handle. The tests release the blocked operation in `finally`.
- Sidebar projects now expose one shared stop task and async disposal. Stop first
  disables commands/unsubscribes/cancels through the existing synchronous Dispose,
  then joins the initial load and all six guarded command execution tasks.
- Workspace graceful shutdown and async disposal join that stop task alongside
  their existing repository/statistics cleanup. No fire-and-forget file operation
  is promoted to a completed cleanup merely because its token was canceled.
- Four additional cases verify repeated stop identity, synchronous-then-async
  disposal and cancellation of an individual waiter without abandoning storage.
- These tests use independent synthetic repositories, a loopback provider fixture
  and test-owned temporary files. They do not alter user data, call paid services,
  or operate existing desktop windows.

## Evidence boundary

- The earlier full-run restart cleanup `IOException` leaves an empty owned
  directory; the original failure and directory remain preserved. The current
  inspection does not identify its locking owner. The earlier sidebar allocation
  assertion also remains unchanged, including its strict zero-byte expectation.
- The new deterministic lifetime failure is confirmed independently. It is not
  asserted to explain either earlier complete-run failure without further evidence.
- Reports are outside the repository in
  `%TEMP%/MossAgent-sidebar-shutdown-20261015`; `baseline.trx` retains the fourteen
  failing cases and `fixed.trx` retains their first passing run.
- Native captures remain postponed. This lifetime slice is not full-product or
  current same-state Codex visual acceptance.

## Validation checkpoint (2026-10-15)

- All 69 related cases pass, including the eighteen new workspace/async-disposal
  cases, the original strict allocation and conversation-restart cases, sidebar
  ordering/drag/lifetime checks, graceful shutdown, close coordination and seven
  architecture checks. The original full-run failures are not replaced by this
  subset's success.
- Solution build has zero warnings/errors. Scoped whitespace verification passes.
- All eight complete suites pass 6,467/6,467 with no skips or aborts: App 4,706,
  Agent 123, Infrastructure 204, Providers 542, Tools 494, Browser 76,
  Platform.Windows 142 and pixel rendering 180. App finishes normally in 13m25s;
  all processes exit. The eight tested assembly hashes remain unchanged.
- The three original conversation-restart cases and six strict sidebar allocation
  cases all pass in this complete run, not only in isolation. The original failing
  reports and retained empty directory remain preserved. Their historical causes
  are still unproven; this successful checkpoint is not attributed to the new
  sidebar lifetime fix without evidence establishing that causal link.
- Baseline/focused runs are not added to the complete-suite count. All eighteen
  new lifetime cases, the preceding header/alignment cases and prior workflow
  regressions participate in the current complete App/pixel suites.
