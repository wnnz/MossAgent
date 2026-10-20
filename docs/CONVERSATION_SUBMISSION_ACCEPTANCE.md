# Atomic conversation submission

## Design and required self-check

- Application owns the submission persistence contract; Infrastructure commits
  the task, user message, attachment references and revision-checked draft updates
  in one SQLite transaction. App coordinates existing editor state through that
  port, without SQL, filesystem access or provider calls.
- Reuse conversation drafts, message/attachment persistence and the existing
  send-preparation cancellation lifetime. This is local metadata persistence,
  not a tool grant; projectless conversations retain no directory/tool access.
- Serialize draft writes while capturing and committing a submission. Preserve
  text and newly selected attachments entered during preparation or persistence;
  conflicts reject the submission before a model call. Never retry automatically.
- Keep attachment bytes in existing file-backed stores. SQLite atomicity does
  not make Git, snapshot files and the database one distributed transaction;
  failed operations can still leave unreferenced immutable snapshots.
- Keep one handwritten type per file and methods/files within AGENTS.md limits.
  Cover new/existing and project/projectless conversations, concurrent editors,
  rollback, cancellation, attachment promotion and independent database reopen.

## Validation status

The four project/projectless and new/existing conversation reproducers failed
before the fix: a provider request could observe its already-committed user
message with the original draft still stored. The reports are retained under
`artifacts/conversation-submission-20261019/baseline`.

Initial focused verification passed 11 App cases and 49 Infrastructure cases
(the latter includes existing draft tests). This uses production composition,
temporary SQLite, real attachment snapshots, controlled HTTP/SSE and independent
composition reopening. The full Debug solution builds with zero warnings/errors.
Those focused results alone do not establish complete regression acceptance.

The first full attempt in `conversation-submission-20261019` was interrupted
after confirmed mixed-store fixture failures, with six completed project reports
and unchanged 2,358 source/config and 2,035 runtime files. It is not a complete
or passing regression. Some custom test editors intentionally pair a real task
repository with an in-memory draft repository; atomic submission must not alter
another editor's persistent draft. The port now explicitly checks support for
the exact draft-store connection factory. Production DI binds that pair; custom
nonpersistent editors retain their existing save path. Two added binding tests
prove production/foreign-store separation and preservation of the other draft.
The repaired submission and affected old workflows pass 22 focused App cases;
the new full candidate is `conversation-submission-bound-20261019`.

That complete Debug regression has now finished: 8,014 passed, six failed, 8,020
total across eight projects; no skips. App had 5,384 passed and six failed; all
seven other projects passed. The before/after manifests contain the identical
path/hash sets for 2,359 source/config and 2,035 runtime files. The failure records
and terminal JSON are preserved. This candidate is not a passing regression.

Two authorization UI cases waited for !HasActiveRun immediately after Send,
which can be true while atomic submission is still preparing. A separate replay
reproduced the two failures. Their completion waits now use !IsRunning, retaining
file, request, approval and audit assertions. Paused-submission cancellation tests
explicitly assert IsRunning=true and HasActiveRun=false at the commit boundary.

The other failures were two provider-answer and two approval-button hit checks.
Separate baseline replays passed all six provider and four approval cases, so the
historical failures cannot simply be dismissed. A new moved-button reproducer
deterministically fails with the old provider layout-only Flush: layout geometry
is current while InputHitTest still uses the previous rendered scene. A shared
headless frame flush now publishes layout and subsequent render work before the
same hit/actual-click assertions. This proves a harness defect, not that every
historical approval failure has the same cause. New focused and complete
regressions must establish the resulting candidate's status.

The resulting `conversation-submission-waits-20261019` candidate passes all 32
focused App cases, including the moved-button reproducer and all affected UI
families. Its complete Debug solution builds with zero warnings/errors. The
structural audit covers 513 changed/new C# files, 27 AXAML files and 2,875
methods/accessors, with no violations and a maximum method length of 78 lines.
Light/large and dark/compact pending-approval headless screenshots were inspected;
this is not native Windows interaction acceptance. The complete Debug regression
has now finished: all 8,021 cases pass across eight projects, including 5,391 App
cases; no failures, skips or aborts, with all eight TRX outcomes Completed and
process exit zero. Before/after path/hash sets are identical for 2,361 source/config
files and 2,035 Debug runtime files. `regression-terminal.json` and the original
TRX/logs are retained. The older six-failure candidate remains separate evidence.
The full Release solution build also passes with zero warnings/errors using JIT,
PublishAot=false and PublishTrimmed=false; all 32 corresponding Release App
focused cases pass. This is not a full Release regression or a published package.

## Controlled process termination

`artifacts/submission-crash-probe-20261019/probe.log` adds four native Windows
child-process checks with production App DI and temporary SQLite: project and
projectless, each new and existing conversation. The worker persists its original
draft, commits through the production submission port, signals completion and
stays alive without disposing its service provider. The parent kills only that
owned process tree, opens fresh production composition and initializes recovery.
All four retain the submitted user message once, retain empty consumed drafts and
restore the task as Interrupted. Existing conversations retain their earlier
message. No automatic continuation is invoked.

This is real process termination after a confirmed commit, not a native GUI/EXE
restart test, a kill during SQLite commit, or a full running model/tool crash. It
has no provider request, attachment payload, Git operation or existing user data.
Pre-commit rollback, attachment promotion and UI cancellation retain their separate
controlled test evidence.

The UI retains local text and blocks sending on a revision conflict, offering the
existing explicit retry/keep-local path. A manual retry after resolving the
conflict is tested; it never starts automatically. Concurrent submissions of one
revision can commit at most once. SQL failures and archived/foreign scopes leave
task status, messages and draft revisions unchanged.

Cancellation before commit retains the original draft. Cancellation after commit
does not reconstruct an already-submitted draft: its saved conversation is
selected, marked Cancelled and displays a localized explanation. No model call
starts, and later work requires another explicit user message. The UI keeps
owning a noncooperative commit until it returns, preventing a second send.

No schema change is required; existing draft revisions, scopes and file-backed
attachment storage are reused. SQLite's atomic commit covers structured metadata
only. Debounced edits made after that commit, file/Git operations, unreferenced
snapshot reclamation and creation-time worktree crash recovery remain separate.
Separate native EXE evidence now covers a projectless send, real streaming
request and forced termination/restart without replay; see
`NATIVE_SUBMISSION_ACCEPTANCE.md`. Native input later returns access-denied
errors and captured pixels are black, so draft typing, stop interaction and
visual acceptance remain incomplete. No mid-commit crash, live-provider or complete Release
acceptance is claimed. Release build and targeted validation,
when available, are recorded separately from full-product acceptance.
