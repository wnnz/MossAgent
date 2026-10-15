# Automation mutation acceptance

## Scope (2026-10-09)

This continuation fixes real plan-editing and mutation ownership. Previously Save
had no cancellable command token, relinquished busy state before read-back, closed
newer/replaced drafts on late completion and could publish conflict after disposal.
Direct rejected invocation also replaced the command's existing execution task.
The initial seven regression cases fail against that implementation.

App-owned operation state now covers editor loading, save, enable/pause, removal
and manual-run start. Guarded commands reject disabled invocation before touching
the existing operation. Shared notices show meaningful progress and an actual cancel
action; a read-back phase is not presented as cancellable storage rollback.

The visual tests additionally reproduce a broken enabled binding: assigning the
editor's own DataContext caused its `IsBusy` binding to resolve against the wrong
model. It now explicitly resolves the owning dialog model, so pending save really
disables fields while leaving the independent cancel action available.

## Design and safety self-check

1. App owns observable operation state, editor coordination and draft revisions.
   Existing repository/catalog ports, Application scheduling and task-scoped
   execution remain authoritative. Dependency direction and composition stay valid;
   production UI does not access files, processes, SQL or provider HTTP directly.
2. Existing guarded commands and repository/scheduler contracts are reused. There
   is no new execution engine, provider fallback, serialization contract or migration.
   New plans stay paused/read-only. Manually starting a run still requires an explicit
   action and uses the existing approval, cancellation, output and audit pipeline.
3. Catalog/write work receives linked command and lifetime cancellation plus a
   30-second cooperative deadline through TimeProvider. Cancel reaches the port but
   cannot forcibly interrupt a noncooperative write. Ownership persists until return
   and through read-back; the list owns its separate 30-second read deadline. Busy
   commands block reentry and history navigation, not the running task's Stop.
   Disposal rejects late UI publication and new work. Safe localized failure hints
   never display raw exception text or claim that an uncertain operation rolled back.
4. One type per handwritten C# file remains enforced. The scoped Roslyn audit checks
   14 files with no syntax/size violations: maximum 213 physical lines and maximum
   44 lines per method/accessor. Views reuse shared Controls and existing appearance
   variants. The new notice and modified page remain below AXAML size limits; markup
   and analyzer gates pass. JIT debugging and win-x64 single-file publishing remain
   unchanged; no AOT/JSON migration is introduced.
5. Twenty added cases cover cancellation, noncooperative timeout ownership,
   rejected synchronous/asynchronous reentry, late disposal, superseded contexts,
   retained newer drafts, post-commit external changes, failed read-back and recovery,
   successful cancelled writes, bound/reachable controls and guarded cancellation.
   Existing deterministic execution and safety tests remain in the focused/full runs.

## Save identity and draft contract

- Success records the committed ID/revision before read-back. A new draft saved
  before cancellation does not become a second plan on the next explicit Save.
- Read-back must match expected revision, content and millisecond timestamp before
  rebasing or closing. External edits are not silently accepted as the draft baseline.
  The known committed identity remains retained even after a conflicting read.
- An unchanged current draft closes only after successful matching read-back. A
  newer draft retains its fields and uses the saved identity/revision for the next
  Save. A replaced/reopened context cannot be closed or receive an old notice,
  including reopening during an observable snapshot notification.
- Failed read-back preserves the editor and records a safe saved-but-unconfirmed
  notice. Successful retry confirms the stored result without another write.
- Cancelled writes may still commit. The UI reports that possibility, retains the
  draft and confirms/rebases only the actual stored result. There is no automatic
  mutation retry or rollback. A started background run must use its existing Stop.

## Verified evidence

Evidence root: `%TEMP%\MossAgent-automation-mutations-20261009`.

- Initial red run: 7/7 fail. Final automation-focused run: 99/99 pass. Full solution
  regression: 3,346/3,346 in seven completed TRX files: App 2,543, Tools 188, Providers
  283, Infrastructure 136, Browser 76, Agent 63 and Platform.Windows 57. None skip.
- Final solution and native QA builds report zero warnings/errors. Scoped whitespace
  verification passes without a repair iteration. Source AST/XML and diff checks
  preserve the repository constraints and normal newline handling.
- Native QA runs real App startup/shutdown at ordinary privilege, managed/JIT,
  render scale 1, Light 1280x820 and Dark 980x640, with separate SQLite/project data.
  Its provider is deliberately non-executable and the seeded plan remains paused
  and read-only. A repository decorator delays the first save before storage; actual
  mouse cancellation reaches that port. SQLite read-back confirms no first-write
  changes while the editor retains its changed name and enables explicit Save again.
- The second real mouse Save updates the same stored plan and closes the editor.
  Normal shutdown confirms exactly two attempts, one committed write, one revision
  increment, unchanged plan identity, zero runs, zero conversations and intact
  workspace composer draft in both cases. No user schedule or provider is changed.
- Passive Python PrintWindow captures check non-black pixels and exact dimensions.
  Saving/cancelled/saved frames cover disabled fields, live cancel, safe error,
  retained name and restored cards. Footer actions remain reachable at 980x640.
  Python screenshot/montage scripts, QA source, data and captures stay outside source
  or in ignored validation artifacts and are not submitted or published.
- One accessibility SetValue call timed out after successfully changing the light
  field. A fresh observed value verified the outcome; no duplicate input was sent.
  The dark case uses observed field focus and real keyboard text input. Cached or
  null accessibility output is never accepted alone as proof of the input result.

## Remaining limits

- Same-state current Codex plan/editor pixels, high DPI, user fonts and exhaustive
  hover/focus still need comparison. These frames verify MossAgent behavior and
  rendering, not one-to-one Codex equivalence or full product completion.
- Noncooperative commit/timeout, external conflict, failed read-back and draft
  replacement are deterministic tests, not additional native database experiments.
  No real paid model request, enabling, removal or security-setting change is made
  in native QA. Scheduler, persistence schema, model routing and tool policy do not
  change in this slice.
