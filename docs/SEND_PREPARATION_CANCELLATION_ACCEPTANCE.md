# Send preparation cancellation

## Scope and required design self-check

1. **Ownership:** App owns preparation lifetime and composer state. Application
   owns the typed Git creation failure contract; Infrastructure reports a
   retained managed path after owned Git execution returns. Dependency direction
   remains unchanged; UI performs no SQL, filesystem or process operations.
2. **Existing ports:** reuse workspace persistence, attachment snapshots,
   `IGitWorktreeService`, run tracking and the existing shutdown coordinator.
   No database migration, provider protocol or tool capability is added.
3. **Safety/lifetime:** each preparation owns its cancellation source and tracked
   completion. Cancellation requests must not abandon an outstanding mutation.
   Recovery and final status persistence use an independent 30-second cooperative
   deadline. Preserve approval, authorized roots, output limits and process
   ownership; never force-remove a worktree or automatically replay a message.
4. **Size:** preparation ownership and typed exceptions have dedicated one-type
   files. Keep the existing workspace within 500 lines and methods within 80;
   run the changed-source shape audit before handoff.
5. **Proof:** cover pre/post-commit cancellation, input/attachment preservation,
   independent conversations, uncooperative writes, shutdown, finalization
   failures, partial Git creation and actual keyboard activation. Use temporary
   Git/SQLite, a loopback provider and production Headless controls, not paid
   services or existing user windows.

## Behavior

- The primary button shows a stop glyph and **取消发送准备** during preparation.
  After activation it shows **正在取消发送准备** and cannot be activated again
  until preparation has safely returned. Enter/Ctrl+Enter in the editor never
  submit again or cancel the pending send. Space on the focused button cancels.
- Keep original or subsequently edited text and selected attachments. Never
  consume their draft identities or start a provider after preparation is
  cancelled. Cancellation of one preparation does not cancel an independent
  conversation using the same runtime tracker.
- If persistence committed before observing cancellation, mark the task
  Cancelled using an independent token. Keep its history and owned worktree;
  expose a new task without navigating away from the current draft scope.
- If final status cannot be confirmed, keep visible task metadata and show a
  localized instruction containing its task ID: refresh and inspect before
  resending. Do not expose raw database/Git diagnostics or falsely report
  successful cancellation. The composer does not show a contradictory generic
  retry hint beside this recovery instruction.
- For a failure after worktree creation, query both active and archived task
  ownership before ordinary removal. Ambiguous ownership or failed cleanup
  retains the directory. A creation-time Git failure/cancellation with an
  existing managed directory reports its path instead of blindly deleting it.
- Shutdown joins the preparation. Synchronous disposal requests cancellation
  and detaches notifications; asynchronous disposal also joins it. Local
  workspace disposal is not global cancellation of all shared runtime tasks.
- Snapshot writes, Cancelled-status writes and unowned-worktree removal retain
  their actual operation rather than using `WaitAsync` to abandon mutations.
  Independent recovery deadlines request cancellation but do not release an
  uncooperative write. Late snapshot results are rejected before task/message
  persistence. A manual clock tests the 30-second deadline without real waiting.

## Shared composer layout

The environment picker adds a genuine third action beside project and agent.
At narrow widths the environment and branch move together to a second row;
project/agent values stay shrinkable and their buttons retain the full labels.
Shared appearance rules let this surface grow instead of clipping its second
row. Projectless controls remain hidden and grant no directory access.

When that extra row leaves less vertical space for an empty conversation, the
shared welcome layout uses a short-viewport appearance: smaller brand and
heading, finite measured height, and restoration of the original appearance
after resizing. It does not move the composer over the heading, alter model
state, remove the project action or shrink unrelated popup text.

## Validation

Candidate: `artifacts/send-preparation-cancellation-20261018`.

| Check | Terminal result |
| --- | --- |
| Final focused cancellation, mutation ownership, keyboard, context overflow and empty-session layout | 61/61 passed, no skips |
| Final wider related App checks, including sidebar allocation and worktree flows | 319/319 passed, no skips |
| Final complete shared visual/component suite | 300/300 passed, no skips |
| Final complete Infrastructure suite | 392/392 passed, no skips |
| Final attachment history, lifecycle and projectless boundary checks | 48/48 passed, no skips |
| Final complete solution build | 0 warnings, 0 errors |
| Changed/untracked handwritten C# shape audit | 205 files, no violations; maximum 499 file lines and 72 method lines |

All final runs are terminal. Counts are per-run and overlap; do not sum them as
unique cases. TRX reports are under `results/validated-focused`,
`validated-related`, `validated-visual`, `validated-infrastructure` and
`validated-attachment-boundaries`. The build log is `build-final.log`.
`terminal-results-summary.json`, `validated-source-identity.json`,
`validated-binary-identity.json` and `validated-identity-check.json` identify the
candidate and retained results. The identity checks cover 2,052 source/config
files and 105 copied/module/test binaries. Changed AXAML also stays within its
500-line limit, with App markup at 499. Retain initial failed builds and TRX
results; do not substitute earlier candidates' passing counts for this revision.

This is not a new complete solution test run. The older frozen full regression
remains 7,154 passed and 1 failed across 7,155 cases; its sidebar allocation
failure is historical evidence, not erased by this candidate's six passing
allocation checks. Neither that result nor these slice checks completes the
product goal.

The initial broader run finished with 292 passed and 8 failed. Three failures
were obsolete primary-button/stop-glyph assumptions; three were markup/action
assumptions predating the environment picker; two exposed real branch-label
compression. The old expectation that Enter cannot submit again is retained
and now also verifies that Enter never invokes the new cancel action.
An intermediate two-row attempt failed 9 of 83 checks: type selectors did not
match the UserControl wrapper's style key, leaving the old fixed height; hiding
the content host also deferred creation of its projectless control, and the
legacy no-environment component unnecessarily changed rows. Class-based shared
variants and environment-aware reflow fix these without weakening containment,
non-overlap, availability, icon geometry or projectless access assertions.
The intermediate `Height=Auto` build error was corrected to Avalonia's `NaN`
value. These original failure logs are retained.

Revision 5 then passed 86 focused checks and 48 attachment-boundary checks,
but its broader App run finished 300/304: all four failures were the compact
empty-conversation heading overlapping the newly taller composer by five
pixels. Its shared visual (300/300) and Infrastructure (392/392) runs also
finished; those passes did not make the App failure disappear. The welcome
layout previously returned its unconstrained natural height even when the
remaining viewport was smaller. The shared short-viewport variant and finite
measurement fix that cause. Coverage now includes actual saved agent profiles,
light/dark themes, long project names and restoring the wide appearance.

Sixteen revision-5 Headless screenshots remain under `images-final`: preparation
and cancellation in light/dark themes at 526/1000, long labels at 360/400/740,
and the full main window with its inspector docked. Light/narrow, dark/docked
and dark/cancelling frames were inspected directly. This is rendered production
control evidence, not native Windows input or EXE restart verification.
Final renders are recorded separately under `images-validated`, including the
saved empty conversation with and without a configured agent. All 28 frames
were generated; final light/long-name/agent and dark/short-name/no-agent empty
conversation frames were inspected directly in addition to the earlier frames.

## Remaining limits

- No native EXE restart, original-window interaction, paid model call or hard
  process-crash recovery is established by Headless/loopback checks.
- A cancellation request cannot force a noncooperative persistence port to
  return. Continue owning mutations until they return rather than permitting
  a second send or closing storage underneath them.
- Snapshot files, SQLite and Git do not form one distributed transaction.
  Unreferenced snapshots can remain, and abrupt exit can restore an older draft;
  restored input is never sent automatically.
- A retained path is recovery information, not proof that a partial worktree or
  branch is healthy. Branch/base selection, automatic branch deletion,
  creation-time crash recovery and complete worktree link/reparse/race auditing
  remain incomplete or unverified.
- This slice does not establish complete Codex Desktop functional parity.
