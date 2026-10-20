# Explicit attachments in projectless conversations

The overall functional/workflow parity goal remains active. This extends the
existing projectless plain-chat and image-attachment flows without granting a
project or directory permission. Native selector/restart and real-provider
acceptance remain incomplete; this is not whole-product acceptance.

## Plan and required design self-check

1. Application owns a separate explicit-selection reader port; Infrastructure
   implements bounded exact-file reads and snapshot persistence. App calls it only
   after a user-initiated picker, never from a model tool. Existing project reads
   retain their root checks. No fake ProjectProfile or parent-directory grant.
2. Reuse the real text/image decoder, immutable binary stores, draft CAS, user
   message metadata, history hydration and original-turn native provider blocks.
   A selection-specific stamp distinguishes projectless snapshots from project
   authorization stamps; restoring a snapshot never re-reads its source path.
3. Read only the captured absolute files, reject links/reparse points in their
   ancestry and do not enumerate/read sibling files. Keep thirty-second I/O,
   cancellation, 10-file/text/image limits and model capability checks. Navigation,
   shutdown and authorization changes invalidate pending picker/reader results.
   Projectless Agent runs still have no roots, tools, subagents, browser or shell.
4. Use a forward SQLite migration preserving existing metadata and snapshot paths.
   Retain one handwritten type per file and existing file/method limits. Reuse
   composer wrappers and centralized labels; no new per-view visual variants.
5. Prove real selected-file/snapshot/SQLite round trips, independent composition
   reopen, scope switching, first-send promotion, later history, missing/corrupt
   snapshots, forged stamps, relative/link paths, limits, cancel/timeout and stale
   selection refusal. Verify actual loopback requests advertise no tools and
   preserve user-role text/image attachments; no native/paid-model claims.

## Implemented workflow and safety boundary

- App exposes the existing attachment button in plain chat, with centralized
  localized labels and an accessible name. Selection is user initiated. App
  routes exact picker paths to `ISelectedAttachmentReader`; the port is not
  registered as an Agent tool. The production reader is shared with the project
  reader, but the two authorization entry points remain distinct.
- The selected-file reader captures the input list before asynchronous work,
  requires absolute paths, rejects Windows ADS/device/GLOBALROOT paths and
  link/reparse ancestors, and reads only the named files. A parent directory is
  never added to task roots. File count, UTF-8 size, actual image decoding, image
  count/aggregate size, cancellation and thirty-second read limits remain active.
  App also refuses relative picker results and incomplete/foreign reader results.
- `CreateSelection` hashes the normalized path with the
  `explicit-file-selection:v1` domain prefix. This is an internal consistency
  marker, not an unforgeable authorization token. Project authorization stamps
  are not accepted as projectless draft stamps. Restoring a draft/history entry
  reads its internal immutable snapshot, never its original source path.
- Version 17 changes the draft-attachment scope trigger to require an actual
  parent draft row, including a projectless row. It does not rewrite existing
  snapshots or metadata. Migration failure rolls back the trigger/version and
  leaves the database retryable; existing project drafts remain readable.
- Text/static-image drafts share text revision checks and remain scoped to a
  new conversation or a particular task. Successful first sends consume only
  the captured selection, while navigation and late picker/reader completion
  cannot populate another scope. Sent attachments remain original-user-turn
  inputs on follow-up requests and independent composition reopen.
- Missing/corrupt snapshots retain text and removable unavailable chips, block
  sending, and recover after explicit removal. An unsupported model retains the
  image draft and disables sending; enabling/selecting supported capabilities
  restores sendability. Images can be sent without a text body.
- A selected file does not create a project/worktree, authorize a directory or
  enable shell, terminal, browser, tools or subagents. Real loopback requests
  still advertise an empty tool list. Agent tests additionally inject unsolicited
  calls with an image under ReadOnly, AskEveryTime and FullAccess: all reject
  execution with `tools_disabled`, without retrying the provider or reading the
  tool catalog. The normal tool pipeline retains its independent disabled-tools
  boundary.

### Startup and navigation semantics

Startup still selects the latest existing task when one exists. A saved
projectless-new-chat draft is not automatically inserted into that task. Choose
New Chat to return to the independent unsent draft. The integration test first
asserts this isolation, then explicitly navigates to the new-chat scope and
verifies text/image restoration after deleting the original selected file.
It does not claim startup automatically restores the last visible page.

## UI regression correction

The first expanded run completed with 298 passes and six failures; its original
`results/app-attachments.trx` is retained. Four failures were obsolete assertions
that projectless chat must hide attachments. They now require a visible/enabled
button with a null project parameter, while still requiring the project approval
control to be hidden.

Two failures exposed a real responsive-layout bug: toolbar reflow only reacted
to size changes. Once attachments remained visible, switching project mode could
change approval visibility without changing toolbar size, leaving a stale stacked
layout. The view now recalculates on approval visibility as well as size changes,
and pairs that event subscription with visual-tree detach cleanup. This remains
view-owned layout behavior, with no new I/O, permissions or per-view appearance.
Both themes cover repeated 320/360/460/800-width changes, project/plain-chat
switches, model/send placement and the retained attachment action. The focused
19-case rerun and expanded 304-case rerun both pass; no failing test was skipped.

## Verification checkpoint

Candidate: `artifacts/projectless-attachments-20261018`, Debug/JIT, 2026-10-18.
All paths below are relative to that candidate directory.

| Scope | Final evidence |
| --- | --- |
| Infrastructure full project | **328/328**, `results/infrastructure.trx`: exact-file reader boundaries, real codecs/files/SQLite, v17 migration/rollback, existing draft/history and project-root rejection cases |
| Providers full project | **600/600**, `results/providers.trx`: four native image envelopes, streaming and request/body limits; does not prove paid-provider image understanding |
| Agent full project | **137/137**, `results/agent-final.trx`: image context and all-policy projectless unsolicited-call rejection, plus repository-wide single-type/file-limit checks |
| App focused regression | **304/304**, `results/app-attachments-final.trx`: all attachment/composer/projectless-chat cases plus model-selector coverage; includes 13 explicit projectless-attachment cases |
| Tool-pipeline projectless boundary | **3/3**, `results/projectless-tool-boundary.trx`: disabled tool execution remains denied under all three approval policies |
| UI/projectless narrow rerun | **19/19**, `results/projectless-ui-fixed.trx`; overlaps the 304 App cases and is not counted twice |
| Solution build | `build-final.log`, exit 0, **0 warnings, 0 errors** |
| Changed/untracked handwritten C# | `source-shape-final.json`: 135 files, exactly one type each, maximum 497 file lines / 67 method lines, no syntax/size violations; whitespace check passes |

The primary project totals plus the separate tool-pipeline cases are **1,372
passing tests**, not a complete solution-wide test run or product completion.
The earlier 12-case workflow result predates the additional submitted-history
reopen case; that new case is included in the final 304-case run. Earlier failed
results remain evidence of the corrected defects, not final-candidate failures.

Reproduction commands, from the repository root:

```powershell
dotnet build MossAgent.slnx --artifacts-path artifacts/projectless-attachments-20261018 --nologo -v minimal
dotnet test tests/MossAgent.Infrastructure.Tests/MossAgent.Infrastructure.Tests.csproj --artifacts-path artifacts/projectless-attachments-20261018 --no-build --logger 'trx;LogFileName=infrastructure-recheck.trx' --results-directory artifacts/projectless-attachments-20261018/results --nologo -v minimal
dotnet test tests/MossAgent.Providers.Tests/MossAgent.Providers.Tests.csproj --artifacts-path artifacts/projectless-attachments-20261018 --no-build --logger 'trx;LogFileName=providers-recheck.trx' --results-directory artifacts/projectless-attachments-20261018/results --nologo -v minimal
dotnet test tests/MossAgent.Agent.Tests/MossAgent.Agent.Tests.csproj --artifacts-path artifacts/projectless-attachments-20261018 --no-build --logger 'trx;LogFileName=agent-recheck.trx' --results-directory artifacts/projectless-attachments-20261018/results --nologo -v minimal
dotnet test tests/MossAgent.App.Tests/MossAgent.App.Tests.csproj --artifacts-path artifacts/projectless-attachments-20261018 --no-build --filter 'FullyQualifiedName~Attachment|FullyQualifiedName~Composer|FullyQualifiedName~WorkspaceSendDraft|FullyQualifiedName~ProjectlessChat|FullyQualifiedName~ModelSelectorComponentTests' --logger 'trx;LogFileName=app-attachments-recheck.trx' --results-directory artifacts/projectless-attachments-20261018/results --nologo -v minimal
dotnet test tests/MossAgent.Tools.Tests/MossAgent.Tools.Tests.csproj --artifacts-path artifacts/projectless-attachments-20261018 --no-build --filter 'FullyQualifiedName~ProjectlessToolBoundaryTests' --logger 'trx;LogFileName=projectless-tool-boundary-recheck.trx' --results-directory artifacts/projectless-attachments-20261018/results --nologo -v minimal
```

## Remaining scope and evidence limits

These are this checkpoint's original boundaries. Selected/draft and sent/history
thumbnails were subsequently added and validated in
`ATTACHMENT_THUMBNAIL_ACCEPTANCE.md`; that evidence does not establish native
picker/restart, full-size viewing, EXIF, PDF or paid-provider acceptance.
JPEG preview orientation is subsequently covered by
`IMAGE_ORIENTATION_ACCEPTANCE.md`; other-format/native-image orientation remains
unverified.

- Tests use temporary files/databases, production service composition and actual
  loopback HTTP with scripted responses. Picker results are scripted. Keyboard,
  theme and layout checks use Avalonia Headless, not native Windows selector
  clicks, desktop screenshots or an installed EXE restart. No existing user
  window, real account, credential or paid model was accessed.
- Thumbnails, PDF/native documents, child-agent image sharing, real-provider
  accepted formats/interpretation and native picker/restart remain open. The
  conservative image context estimate and native decoder cancellation limits
  from the prior image-workflow checkpoint still apply.
- Link checks are not a claim of eliminating every filesystem TOCTOU race.
  Snapshot writes and SQLite commits remain separate; failed writes may leave
  unreferenced files and abrupt submission exit may restore an unsent draft.
  Restored drafts never execute or send automatically.
- Earlier long-running sessions `21912` and `11377` were re-polled and remain
  live without new output; their associated dotnet/vstest/App processes were
  checked. They were not stopped, restarted or overwritten. Earlier allocation
  failures remain unresolved and neither run establishes a successful current
  complete App suite. The focused results above do not erase those limitations.
- No commit, push, publish, AOT or trimming change was made. The full functional
  parity goal remains active; `FUNCTIONAL_PARITY_MATRIX.md` retains the gaps.
