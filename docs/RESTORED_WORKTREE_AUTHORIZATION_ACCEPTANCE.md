# Restored worktree operation authorization

## Plan and required self-check

1. Application owns binding a persisted task/project to worktree authorization;
   Infrastructure verifies paths and Git identity. Tools.Abstractions owns the
   portable per-context guard contract. No UI filesystem/process access.
2. Extend the existing Git worktree port with read-only validation and reuse the
   standard executor for previews, diff, statistics, undo, terminal and browser
   operations. Existing contexts without managed worktrees retain their behavior.
3. Verify the exact task-GUID path, authorization roots and matching registration
   before approval and again before execution. Reject stale/mismatched contexts,
   keep a bounded cooperative deadline and await cleanup. Validate before model
   execution/resume persistence and before direct branch discovery as well.
4. Keep guards and validation helpers in separate one-type files; do not grow the
   499-line WorkspaceViewModel or App.axaml. No cache of successful path checks.
5. Prove valid restored operation, forged/foreign paths, revocation, reparse
   replacement after context construction/approval, cancellation/deadline, safe
   audit/error output, no provider call and no consumed draft on denied resume.

## Status

The final candidate's expanded related regression passes. Complete current
solution regression and native acceptance are not established by this
checkpoint. This is not whole-product acceptance.

## Operation boundaries

- A saved `WorktreePath` is not a directory grant. Infrastructure's read-only
  validator requires the exact owning task's canonical GUID directory, current
  project authorization snapshot, matching repository metadata and registration.
  Validation neither creates application folders nor removes worktrees.
- Application binds the validator to immutable project/root snapshots in
  `ManagedWorktreeWorkspaceGuard`. The context must retain its working directory
  and authorized roots; successful validation is never cached across operations.
- The standard tool executor checks the guard after schema/capability validation,
  before approval and again before execution. Validation has a cooperative
  thirty-second ceiling, honors shorter tool timeouts and caller cancellation,
  awaits cleanup, and returns bounded localized failures without raw exceptions.
  Ordinary contexts without a managed-worktree guard keep their previous path.
- Resume validates the stored task against the repository's project before
  creating attachments/messages or persisting Running state. Run preparation
  validates before history hydration/provider invocation. Direct branch reads
  fail closed without a validator, with a mismatched project, or on rejection.
- Previews, Git diff, change statistics, undo, terminal controls and browser
  toolbar mutations retain their context guard through the existing tool
  pipeline. Child-agent context copies retain it through their own approval and
  execution checks. Task attachment creates only a lazy browser-session handle.
  Preview file picking is explicit user selection; opening the selected path
  still uses the guarded pipeline. Stored artifacts retain independent task
  ownership checks rather than acquiring a grant from a saved working directory.
- Explicit worktree cleanup also checks task/project identity and validates the
  owning task's path before removal. A valid worktree belonging to another task
  must remain registered/on disk, and rejected cleanup must not clear SQLite
  metadata. Dirty-directory retention and explicit confirmation are unchanged.
- Terminal guard rejection uses the existing actionable path-authorization
  message instead of the generic execution-failed label.

## Deterministic evidence

The fixtures use isolated temporary Git repositories, real SQLite restoration,
production App composition, loopback model responses and fake agent-model
streams; they do not access user windows, credentials or paid providers.

- Restored invalid source/foreign-task/relative/empty paths and project
  authorization revoked before reopening deny tools, preview/diff/terminal
  operations, branch discovery and continuation. Draft/history/status remain
  intact, no terminal session starts and no provider request is made. The valid
  restored task reads its own file and continues only on explicit submission.
- Real filesystem tests cover task-GUID/canonical-path rejection before native
  discovery, changed roots/working directory, replacement with a junction after
  earlier success, and cancellation/deadline process cleanup without deletion.
- Tool-pipeline tests cover both approval boundaries, safe audit completion,
  schema/capability ordering and cleanup completion before cancelled/timed-out
  validation returns. Child-agent tests independently reject at its pre-approval
  or post-approval boundary, and exercise the successful child-tool path.
- Cleanup's two task/project mismatch cases failed before the fix because no
  exception was raised. The reports are retained as
  `cleanup-authorization-red.trx`; the production fix adds task-bound validation,
  not a force-delete or relaxed test expectation.
- The first expanded App candidate found a stale branch-status test that created
  an arbitrary worktree path without any validator. The updated test creates an
  explicitly owned probe worktree; separate missing/rejecting-validator cases
  assert that no branch discovery starts. The original failure remains recorded.

## Final related checkpoint

`artifacts/restored-worktree-authorization-final-20261018` contains terminal
reports from the same frozen source/configuration and binary candidate:

| Validation | Result |
| --- | --- |
| Complete Tools assembly | 506/506 |
| Complete Infrastructure assembly | 442/442 |
| Complete Agent assembly | 140/140 |
| Related App workflows, excluding names containing Native | 799/799 |
| Real Windows Git process-ownership cases | 8/8 |
| Total above, without adding overlapping focused runs | 1,895/1,895 |
| Complete solution build | Zero warnings/errors |
| Changed handwritten C# type/size/syntax audit | 238 files, no violations |
| Maximum checked C#/AXAML file length | 499 lines |
| Maximum checked method length | 72 lines |

All five test reports have terminal Completed outcomes, no failed/skipped cases,
and successful command exits. `terminal-results-summary.json` retains commands
and counters; logs and individual TRX files remain alongside it.
`validated-identity-check.json` verifies all 2,070 source/configuration entries,
1,601 binary/native dependencies and the exact source ZIP contents without
drift. Documentation is separate from the tested code/configuration manifest.
The first expanded candidate remains 794/795 App cases, alongside 506 Tools,
442 Infrastructure and 140 Agent cases; its stale branch-status expectation is
not erased or counted as a pass. The focused cleanup red/green reports are
retained separately and are subsets, not additional final coverage.

The older `worktree-path-safety-final-20261018` complete run has now terminated:
7,326/7,326, including 5,127 App cases. Its frozen source predates the external
metadata and restored-task fixes, so that result is not current-candidate full
regression evidence. Any new full run must use this candidate's frozen binaries,
preserve its own reports and be awaited to a real terminal result.

The subsequent complete run of this restored-authorization candidate has now
terminated with exit code zero: 7,355/7,355 across eight Completed TRX reports,
including 5,137 App cases, without failed or skipped tests. All 1,601 recorded
binary/native dependencies still match their frozen identities.
`full-test-terminal.json` and `full-regression-summary.json` retain the real
command exit, timestamps and all counters. These are pre-Git-staging binaries;
source-policy tests may also inspect the live worktree. This result does not
establish complete regression coverage of the later staging candidate or native
user-window/restart acceptance.

## Remaining limits

Per-operation checks do not pin native filesystem identities and are not proof
against replacement during a native Git/file/process operation. Authorization
is an operation/task-context snapshot, not a demonstrated live revocation
subscription for already-running work. Cached display-only data may remain
visible after an external filesystem change; new tool actions are revalidated.
Creation-time crash recovery, native EXE restart, existing user-window acceptance,
and the complete product remain incomplete or unverified. No staging, commit,
push, packaging or publishing is performed.
