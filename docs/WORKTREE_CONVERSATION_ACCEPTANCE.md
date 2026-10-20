# New-conversation worktree selection

## Scope and ownership

The composer now offers **项目目录** and **独立工作树** for a new conversation
with a project. Project-directory execution remains the default. Worktree
selection delegates through `WorkspaceTaskFactory` to the existing Application
`IGitWorktreeService` port and Infrastructure implementation; UI performs no
filesystem, SQL or process operations. This is an explicit application action,
not a model tool and not a grant of FullAccess.

The picker is hidden without a project. Existing conversations display their
persisted environment but cannot change it. Preparing a send locks and closes
the picker. A choice survives repeated notifications of the same context, but
resets when project/task context changes; unsent environment choice is not
persisted as a draft or restored after application restart.

Git creates a managed directory and `mossagent/{taskId:N}` branch from the source
repository's current HEAD. It requires an existing commit and does not copy
uncommitted source changes. Existing conversations reuse their saved worktree.
The task's primary authorized root becomes the worktree; other explicitly
authorized directories remain. Projectless conversations gain no roots.

## Required design self-check

1. **Owner/dependencies:** App owns selection and preparation coordination;
   Application owns the Git port and Infrastructure owns native Git execution.
   No dependency direction, database schema or provider protocol changes.
2. **Existing contracts:** reuse `IGitWorktreeService`, `IWorkspaceRepository`,
   attachment snapshots and the existing task-context/tool-execution pipeline.
3. **Safety/lifetime:** retain task approval policy and standard tool gates.
   Git keeps its existing owned-process, 30-second and bounded-output behavior.
   Failed preparation recovery has a separate 30-second cancellation source so
   a cancelled caller does not prevent safe cleanup. Never use forced removal.
4. **Size:** selection and typed recovery exception have separate files;
   handwritten C# stays one type per file. The changed-source audit covers
   188 files, with a maximum of 500 file lines and 72 method lines, no failures.
5. **Evidence:** selection/locking, persistence ambiguity and cleanup failure
   unit tests; real temporary Git/SQLite and loopback model workflow; standard
   executor authorization negatives; Headless theme/width/keyboard checks,
   including a real send held at persistence to prove preparation locking.

## Failed preparation and recovery

If message snapshot or task/message persistence fails after a worktree was
created, first query both active and archived tasks with the independent
deadline. Remove the new worktree only when neither contains that task ID.
Successful cleanup preserves the original preparation failure. If a task may
already have committed, lookup fails, or Git refuses removal, preserve the
directory and raise `WorkspaceWorktreePreparationException` with its path.

The composer shows a localized recovery notice advising the user to refresh and
inspect the retained task/path before retrying. It does not display raw inner
exceptions or Git stderr. SQLite is not a distributed transaction with Git or
file-backed snapshots: this is cautious recovery, not guaranteed atomicity.
Explicit cleanup failures also use a fixed localized recovery message instead
of displaying arbitrary exception/Git output; task metadata is not replaced by
an assumed successful cleanup.

## Current evidence, 2026-10-18

Candidate: `artifacts/worktree-conversation-20261018`.

| Check | Terminal result |
| --- | --- |
| Initial selection/preparation unit checks | 18/18 passed |
| Final worktree/environment cases within related App checks | 29/29 passed, no skips |
| Final related App checks, including the six sidebar allocation cases | 97/97 passed, no skips |
| Final complete shared visual/component suite | 300/300 passed, no skips |
| Final Infrastructure Git worktree/process/repository checks | 17/17 passed, no skips |
| Complete solution build | 0 warnings, 0 errors |
| Changed/untracked handwritten C# shape audit | 188 files, no violations |

The final three test runs total 414 cases; the 29 worktree cases are included
in the 97 App cases, not an additional 29. Final TRX files are under
`results/final-app`, `results/final-visual` and `results/final-git`.
`terminal-results-summary.json`, `validated-binary-identity.json` and
`validated-source-identity.json` record the bounded checks and tested candidate.

The initial expanded run passed 25/26: its sole failure compared LF against
Windows Git's CRLF checkout. The test now normalizes line endings when checking
the committed baseline; it does not change global Git settings or production
checkout behavior. A later 27/27 run includes an additional real SQLite
rejection case; the final related run also includes the additional authorized-
root/resume and narrow long-label checks. Initial test API/analyzer build errors were corrected before
the successful solution build; failed reports are not erased.

Real workflow tests use only disposable temporary repositories. They explicitly
commit a fixture baseline, leave a source edit uncommitted, select a worktree,
send through the production composition and a loopback SSE provider, follow up
without creating another task/worktree, and reopen a fresh composition over the
same SQLite store. They verify the branch, original dirty file, task history,
persisted worktree and absence of automatic replay.

Using the standard executor, reading the worktree succeeds, reading the
unselected source root is rejected, and ReadOnly rejects writing in the
worktree. Cancelling a running model call retains the worktree. Explicit
two-step cleanup refuses dirty worktrees without losing the task/path; after
the fixture removes its own dirty file, confirmed cleanup removes the worktree
but preserves conversation history. A real SQLite first-message rejection
rolls back the task, cleans the unowned worktree, retains the input and sends no
provider request.

Headless renders the production composer in light/dark themes at 526/1000
pixels. Tests verify inherited business bindings, button bounds, Space-key
opening, option commands/labels, popup close on selection/locking, and the
projectless hidden state. Additional checks keep long project/agent names and
the environment button separate while resizing through 526/360/740/360 pixels.
The first resize test observed stale Headless window bounds: draining queued
dispatcher events before layout fixes the test timing without changing
production layout or relaxing its bounds/overlap assertions. Its failed reports
remain under `focused-expanded` and `narrow-before`.
One later 96/97 related run exposed another invalid fixture assumption:
manually setting `Catalog.IsLocked` can legitimately be overwritten by workspace
draft-state notifications. The control test now starts a real send and holds
its test repository's save, verifies the actual preparation-owned lock, then releases
and joins shutdown. It no longer simulates ownership by toggling the catalog
flag. All six control cases pass after that correction; the failed report is
retained under `validated-app`.
Four screenshots under the candidate's `images`
were generated; light/narrow and dark/wide were inspected directly. This is
not native window interaction or Codex pixel-fidelity evidence.

The previous frozen full-solution regression, session 95889, is now terminal:
7,154 passed and 1 failed across 7,155 tests. App's sole failure is the recorded
1,176-byte sidebar filter allocation assertion in that older binary. The other
seven projects passed. This candidate's focused passes do not turn that older
full regression green; related current-source checks are recorded separately.
The current six sidebar allocation cases pass in the related App run. No new
full-solution test run was substituted for the older frozen result.

## Remaining limits

- No native EXE restart, original-window operation, real model service or paid
  API call was performed. No commit, push or release was made.
- This frozen candidate did not establish preparation-stage cancellation.
  The later `SEND_PREPARATION_CANCELLATION_ACCEPTANCE.md` records the new
  per-preparation lifetime, pre/post-commit recovery and partial Git creation
  checks separately; its results do not rewrite the counts above.
- Later Git creation recovery can report an existing managed directory after
  cancellation/failure, but does not prove every partial side effect is known
  or healthy. Normal removal retains the task branch; automatic branch deletion
  is not implemented.
- Existing worktree service path-prefix validation is unchanged. These checks
  do not establish complete worktree-link/reparse-point/metadata race safety.
- Branch/base selection, uncommitted-change transfer, parallel worktree startup,
  and crash-time recovery during worktree creation remain incomplete or unverified.
- Functional parity with the whole Codex Desktop product remains incomplete;
  see `FUNCTIONAL_PARITY_MATRIX.md` rather than treating test counts as completion.
