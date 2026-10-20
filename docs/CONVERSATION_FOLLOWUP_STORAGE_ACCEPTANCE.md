# Conversation follow-up storage foundation

Status: partial implementation, not Queue/Steer product acceptance. The storage
port is not wired to App composition, a queue UI or automatic model execution.
The command filesystem isolation blocker remains open independently.

## Ownership and safety review

- Domain owns follow-up/queue/dispatch values. Application owns persistence ports
  and bounded snapshot validation. Infrastructure owns schema 23, SQLite revision
  checks and transactions. App's foreground sender is split into a captured
  request and coordinator; no UI SQL/files/process/provider dependency is added.
- Reuse existing conversation attachment files, draft writer, task/message/failure
  writers, cancellation and task-owned run execution. A queued entry's ID becomes
  its committed user-message ID, permitting a database-enforced replay refusal.
- Queue operations run off the UI thread with a 30-second linked deadline. There
  are at most 16 entries, 1 MiB UTF-8 per serialized entry and 2 MiB per queue;
  existing prompt, attachment and draft bounds still apply. Binary/text attachment
  bodies stay in file-backed snapshots. Provider credentials are not captured.
- Projectless entries require ReadOnly, no roots and no worktree. Scope mismatches,
  archived/source-operation tasks, foreign attachment ownership, unsupported image
  input and stale revisions are rejected. These are storage boundaries, not proof
  that a saved root snapshot remains authorized at dispatch time.
- Before this port is connected to a runnable feature, an application coordinator
  must validate current provider/model/agent, roots/worktree/source-review access
  and attachment bytes. It must preserve task ownership, foreground drafts,
  approval and cancellation, including cancellation after commit. The repository
  itself has no model/tool side effects and does not establish that runtime gate.
- Handwritten files retain one type each and existing size limits. No source
  service locator, dependency reversal, AOT/trimming or user-data reset is added.

## Recorded checks

`artifacts/conversation-followup-storage-20261019/focused/queue-storage.trx`:
33 deterministic SQLite/file-backed tests passed. They cover atomic draft/queue
saving, revision conflicts, edit/order/delete persistence, two writers/dispatchers,
single first-entry dispatch, unchanged foreground draft, message-write rollback,
committed-ID refusal, stale/active/archived/foreign task refusal, cancellation,
projectless restrictions, attachment ownership and model compatibility, bounded
payloads, real text/image snapshot reopening, and atomic/retryable schema upgrade
without deleting existing messages/drafts/tasks. Restart checks reopen persistence
and run initialization; they are not process-crash or native GUI acceptance.

The earlier complete Infrastructure run passed 705 tests before the final three
attachment/binding checks were added; it is not the final whole-solution result.
Complete-solution Debug and Release builds have zero warnings/errors. The same
33 storage and 30 App send/submission cases also pass in Release, with no skips;
TRX files are in `release/`. This is not a complete Release regression.

The focused App send/submission regression passes 30 cases in
`focused/send-refactor.trx`. The first whole-solution run is still in progress;
seven projects have terminal results, including all 708 Infrastructure tests.
Platform.Windows reports 145 passes and one PID-file sharing failure before its
process-reaping assertions. `PTY_PID_PUBLICATION_DIAGNOSTIC.md` records a
deterministic old-fixture reproduction and five passing checks using an
artifact-only candidate handshake. It is not yet a repository fixture repair.
Do not describe the whole-solution run as passed or terminal while App is running.

Structural auditing covers the current dirty candidate's 531 handwritten C# and
27 AXAML files, with 2,911 methods/accessors, a maximum method length of 78 and no
size/type/whitespace violations. `WorkspaceViewModel` is now 461 physical lines.
The mid-run identity check records the same 2,379 source/configuration files and
2,035 Debug runtime files as the run's initial manifest, with zero path/hash
differences. This is an intermediate check, not the final test/identity result.

An initial test build missed a namespace import; another early test assertion
compared an empty attachment list with the existing null-on-reopen convention.
Those failures were corrected, and the focused results above use the rebuilt
candidate. No failed run is described as a successful product workflow.

## Remaining acceptance

There is no enqueue command, editable queue presentation, completion continuation,
manual-resume workflow or Steer input port in this slice. No controlled provider
request, approval/tool interaction, native screenshot or paid/live provider was
used to claim Queue acceptance. Do not change the functional matrix to implemented
until the settings-to-request-to-result workflow and failure/recovery branches are
actually connected and verified. See `CONVERSATION_FOLLOWUP_PLAN.md`.

## Runtime follow-up after this storage checkpoint

The frozen full run has now terminated: 8,053 passed and one failed out of 8,054;
5,391 App checks passed. Both final source and Debug runtime manifests have zero
hash differences. The preserved failure concerns PID-file publication in a
ConPTY test fixture. Its independently reproduced readiness handshake was then
applied with an original-hash check, and the real Windows project passed 147/147.
See `PTY_PID_PUBLICATION_DIAGNOSTIC.md` for the separate candidate and limits.

The next Queue runtime slice is now integrated in App composition; the earlier
storage-only statements above describe that checkpoint, not the current entry
points. See `CONVERSATION_FOLLOWUP_RUNTIME_ACCEPTANCE.md` for the controlled
settings-to-request-to-history evidence, UI and remaining acceptance gaps.
