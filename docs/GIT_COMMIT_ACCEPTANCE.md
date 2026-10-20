# Staged Git commit workflow

## Plan and required design self-check

1. Tools.BuiltIn owns independent preview and commit tools, a bounded index/HEAD/
   branch snapshot reader and typed failures. Reuse the Infrastructure repository
   authorization port; App coordinates tools and never accesses files/processes.
2. Use the existing owned Git runner, task-workspace guard, shared approval/audit
   pipeline and shared modal controls. Do not add a shell bypass or a second index.
3. Read-only preview captures repository identity, symbolic/detached/unborn HEAD
   and the complete bounded staged-index representation. Confirm a snapshot token
   after ordinary write/process approval. Reject changed state, conflicts, empty
   index changes and invalid messages. Run ordinary staged-only `git commit`,
   retaining configured hooks/signing: no automatic stage-all, amend, identity
   changes, hook bypass, push or retry. Hooks are approved effects, not OS-sandboxed.
4. Keep types/methods split within repository limits. A dedicated dialog view
   model owns generation/cancellation/cleanup; do not grow the 499-line files.
5. Verify real initial/normal/detached commits, staged-only content, branch/HEAD/
   index changes including approval-time mutation, denial/ReadOnly, hook failure,
   cancellation/deadline/output/audit and task/draft isolation. Exercise production
   UI/keyboard confirmation with real Git, shared light/dark Headless controls and
   focused regression plus the solution build.

## Status

Implementation is present; expanded regression of the frozen candidate is in
progress. This does not complete push, Pull Request, hunk/group staging,
native-window/restart acceptance or the whole product. Snapshot checks do not
claim native handle-level TOCTOU protection.

## Runtime behavior

- Preview authorizes the complete repository and metadata through the existing
  port. Two bounded reads capture HEAD, symbolic/unborn/detached branch and the
  full staged-index representation around reauthorization. Queries disable
  optional locks/fsmonitor and reject incomplete or oversized output. The staged
  file count and repository/metadata/index/HEAD/branch token are returned together.
- Confirm uses the captured authorization roots and state token. The standard
  pipeline rechecks the task guard after approval; the tool reauthorizes and
  rejects changed HEAD, branch, index, conflicts and empty staged changes before
  executing ordinary staged-only `git commit` with a literal bounded message.
  Messages cannot become command switches. No stage-all, amend, identity edits,
  hook/signing bypass, push or automatic retry is added.
- The production inspector owns an accessible shared confirmation dialog with
  multiline message, staged count, branch/HEAD, refresh, cancel and busy states.
  Success refreshes actual Git status; failure/cancellation retains the message,
  invalidates the preview and requires explicit refresh/reconfirmation. A cancelled
  commit may already have taken effect: users must inspect history before retry.
  Per-task message drafts are in memory only, not persisted across restarts.
  Navigation rejects obsolete results and asynchronous disposal awaits actual
  tool and refresh completion, including noncooperative cleanup.
- Both dedicated Git runners strip `GIT_` child environment variables, after
  inherited values and explicit overrides are combined. This closes inherited
  index/object/config/trace redirection without mutating the parent process.
  Windows validates prefix inputs. Environment-only identity/config/SSH settings
  are intentionally not inherited; normal repository/user/system configuration,
  hooks and signing still apply. Shell tools retain their existing environment
  semantics, and hooks are not operating-system-sandboxed.
- The shared dialog now returns focus when its trigger is reenabled by the close
  command, deferring only if needed. Newer focus, hidden/disabled targets, reopened
  dialogs and detached views prevent delayed focus theft. No per-view appearance
  overrides or additional native visual controls are introduced.

## Focused evidence

The isolated candidate is `artifacts/git-commit-20261018`. No real user repository,
window, credentials, API or existing database is used for test side effects.

| Check | Terminal result |
| --- | --- |
| Commit/Git environment/runner tools | 35/35 in `git-commit-3.trx` |
| Commit UI, shared dialog, caption focus | 87/87 in `git-commit-app-2.trx` |
| Windows process input/environment | 11/11 in `git-commit-windows-env-1.trx` |
| Changed C# single-type, syntax and size audit | 293 files, no violations |
| Largest audited C#/AXAML file; method | 499 lines; 72 lines |

The tool cases include actual normal/initial/detached commits, staged bytes vs
later working edits, unselected-file preservation, conflict/empty-index rejection,
approval-time branch/HEAD/index changes, hooks rejecting commits, missing identity,
denial/ReadOnly, guard revocation, cancellation/deadline cleanup, output bounds and
audit completion. An injected alternate index and trace destination remain
untouched by real Git stage/commit. Windows cases cover inherited and explicitly
overridden variable removal, invalid prefixes and unchanged parent environment.

Production App composition with temporary SQLite and Git exercises the actual
inspector entry and keyboard confirmation: HEAD advances, only staged bytes are
committed, the unselected file stays uncommitted and conversation input survives.
View-model probes additionally cover task/message isolation, failure recovery and
waiting for real execution/refresh cleanup. Four light/dark, 280/640-DIP Headless
cases generate eight ready/interrupted images; inspected narrow-light and
wide-dark output has readable controls and no clipping. This is not native UI or
screen-capture acceptance.

The first expanded focused App run retained four genuine focus-return failures
(9 passed/4 failed). Closing the modal changed visibility before the disabled
open command could update, so immediate focus restoration skipped the trigger.
Shared deferred restoration and six negative/reenabled-trigger regressions now
pass. The original failed report is preserved, not overwritten. Earlier focused
passes overlap the final grouping and must not be added as separate coverage.

## Expanded frozen checkpoint

The original candidate's `Run-Validation.ps1` has now terminated with actual exit
zero. All seven commands succeeded and all six reports are Completed:

| Scope | Passed/total |
| --- | --- |
| Complete Tools | 565/565 |
| Complete Infrastructure | 442/442 |
| Complete Agent | 140/140 |
| Related App/settings/automation/navigation, excluding names containing Native | 1,927/1,927 |
| Complete shared Visual | 300/300 |
| Complete Windows | 146/146 |
| Total, without overlapping focused reports | 3,520/3,520 |

The solution build has zero warnings/errors. There are no failed or skipped cases
in these reports. `validation-terminal.json` records the actual exits, times and
scope. All 2,158 source/configuration and 2,439 runtime entries, source ZIP bytes
and source/runtime path sets agreed at terminal. Only documentation changed
during the run. This is expanded related regression, not whole-solution or native
acceptance. This snapshot is frozen and will not be rebuilt.

A subsequent candidate, `artifacts/git-commit-completion-20261018`, adds explicit
success-state and production focus assertions. Its source changes are not part
of the earlier frozen snapshot; the earlier 3,520-case pass must not be relabeled
as full regression of later code.

## Success-completion follow-up

This change belongs only to App's existing commit view model: no new port, tool,
authorization, approval or persistence behavior. Tool and refresh ownership still
last until actual completion; failure, cancellation, task generations and bounded
output retain their earlier semantics. File/method limits are unchanged.

Four deterministic combinations of immediate/delayed commit and refresh initially
failed: observers received `IsOpen == false` while `IsBusy` was still true. The
production real-Git keyboard/focus case passed in that same red run (1 passed,
4 failed), so this is not claimed as a reproduced native focus-loss defect.
Success now records its close intent and publishes non-busy/closed state together
in final UI completion, after operation ownership has been released. Generation
checks prevent old completion from closing a newer task's dialog. No new command
can enter between the two earlier dispatcher updates.

The focused green run passes 91/91, including those four state combinations,
production inspector focus, Escape restoration, cancellation/cleanup, per-task
draft isolation and failure requiring renewed confirmation. It overlaps the later
related regression and must not be added to that report's total. The solution
build is again zero-warning/error; the 293-file single-type/syntax/size audit and
changed AXAML limits pass. The original red report is preserved. The follow-up's
final related run has now exited zero: 373/373, no failures or skips. Its filter
covers Git commit/index, diff, inspector, shared dialogs and caption focus. All
2,158 source/configuration and 2,439 runtime identities, source ZIP bytes and
path sets agree at terminal; `validation-terminal.json` records this check.
Compared with the earlier 3,520-case checkpoint, exactly three source files
changed: the commit view model and its two state/production workflow test files.
That relationship is recorded in `baseline-source-delta.json`; it does not make
373 tests into a new full-solution run or authorize summing overlapping passes.

## Remaining limits

Push/PR, group/hunk staging, Git identity/configuration UI, native restart and
real signing/interactive-hook acceptance remain incomplete or unverified.
Success-path production Headless focus and Escape-close restoration now have
explicit assertions; actual native-window focus under load remains unverified.
Hooks and Git configuration are approved external effects, not path-constrained OS
sandbox operations. Snapshot validation cannot eliminate changes after its final
read. No rollback/retry is inferred from cancellation, timeout or a hook failure.
The development checkout is not staged, committed, pushed, packaged or released.
