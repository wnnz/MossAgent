# Conversation drafts

## Design and required self-check

- App owns editor state, selection, notices and write coalescing. Domain owns
  draft scopes and versioned text; Application owns the persistence port.
  Infrastructure implements it with SQLite. UI never accesses files or SQL.
- Existing conversation preferences describe model selection, not editor
  content, so drafts use a separate port. A scope is an existing task, a new
  conversation within one project, or a projectless new conversation.
- Draft persistence is local application metadata, not a model tool or a file
  permission grant. Task/project ownership and archive state are checked by
  the repository. Reads/writes have cancellation and bounded waits; stored text
  is bounded. Neither errors nor diagnostics expose draft content or exceptions.
- Versioned compare-and-swap prevents silent overwrites by another window.
  Batches are transactional, including clearing the new-conversation scope
  while promoting the next unsent text to a newly created task. Empty rows keep
  their revision so clearing a draft cannot introduce an ABA overwrite.
- A separate draft coordinator avoids growing WorkspaceViewModel beyond the
  500-line limit. Every new handwritten C# file contains exactly one type.
- Tests cover navigation, delayed reads, typing during loads/writes, reload,
  new-task promotion, actual send, failures/retry/shutdown and conflicting
  writers. SQLite tests cover forward migration/rollback, scope ownership,
  archived/missing tasks, cancellation, size limits and all-or-nothing writes.

## Scope and evidence

Before the fix, both new navigation tests failed: project text appeared in
projectless chat and one task's text appeared in another task. Reproducer:
`artifacts/composer-drafts-20261018-before/results` (0 passed, 2 failed).

This change targets unsent **text** drafts. Attachment history, attachment
restoration and provider-native multimodal payloads remain separate incomplete
requirements; adding text persistence does not establish those capabilities.
Normal navigation still clears selected attachments through the existing
authorization-scoped attachment manager.

No native user window, paid provider or live credential is used for these tests.
Final candidate validation is recorded after implementation below.

## Interaction and recovery semantics

- Editing is immediate; saved text is restored only if no newer local edit
  exists. Navigating keeps separate cached entries, so inactive completions cannot
  replace the current editor. Reads and writes use bounded waits even when a test
  port ignores cancellation. Disposal retracts late observable publication.
- Writes coalesce for 200 ms. Normal shutdown flushes them and reports failure
  rather than silently dropping local text. An abrupt process kill before an
  autosave commits can still lose the newest keystrokes; this is not a synchronous
  per-keystroke durability guarantee or automatic task replay.
- Scope ownership is verified at storage time. Empty text retains a revision;
  missing, archived or foreign task scopes cannot be used to overwrite another
  draft. Authorization roots and tool approvals do not change.
- Ambiguous failed writes are reconciled against their exact attempted revision
  and content. Ordinary retry cannot overwrite another window. The separately
  labeled local-draft action explicitly replaces all conflicting scopes in this
  window, still using compare-and-swap. Recovery operations are shared/serialized;
  competing actions cannot install stale baselines.
- The normal composer and failed-shutdown dialog expose recovery controls. Closing
  does not make conflict resolution unreachable or require force exit. Recovery
  only reads/saves draft metadata; it never repeats a provider/tool invocation.
- Secondary workspace fixtures now explicitly use isolated draft stores unless a
  shared repository is requested. This prevents the fixture's pre-seeded main
  composer from acting as an unintended competing real window. Production restart
  tests use the actual registered SQLite repository and independent composition.

The first HTTP assertions incorrectly searched raw JSON for Chinese text; they
were corrected to inspect decoded user-message fields. The initial expanded
navigation suite also retained an assumption that task and new-conversation text
are the same. It now seeds distinct drafts and verifies restoration of both,
retaining the original navigation, page, theme and no-request assertions.

A later non-UI concurrency run exposed dictionary enumeration changing during
scope creation. The coordinator now protects cache membership, captured updates,
write/recovery ownership and version publication with its instance-owned gate;
no repository operation is awaited while holding it. The final suite includes 64
independent rapid-scope/save workflows. This failure remains recorded in
`artifacts/composer-drafts-20261018/results/final-race-guards`; it is not a pass.

## Final synchronized candidate validation

| Check | Result |
| --- | --- |
| Complete solution Debug build | Passed, 0 warnings and 0 errors |
| App workflow/component regression | 149/149 passed, no failures/skips |
| SQLite draft, ownership and migration regression | 70/70 passed, no failures/skips |
| C# syntax, single type and size audit | 36 changed/new files passed; maximum 497 file lines and 58 method lines |
| Scoped whitespace verification and `git diff --check` | Passed |

The final focused TRX files are in
`artifacts/composer-drafts-20261018-final/results/synchronized-focused`.
Earlier narrow runs overlap this scope and are not added to these counts.
The 149 App cases include the real loopback send, independent production
composition restart, actual shared composer/shutdown controls in both themes,
task/model preferences, automation execution, projectless denial boundaries,
attachment isolation and the new recovery/race tests. They are not a native EXE
restart, system selector click, paid-model test or user-window screenshot.

The previous attachment candidate naturally completed: App 4929/4930, with its
unchanged collection-allocation assertion failure (4664 vs 0 bytes); the seven
other projects passed. The draft candidate's full solution regression then
started from its immutable `artifacts/composer-drafts-20261018-final` root with
`--no-build`, storing reports under `results/full`. It remains live (PID 9992)
and has reported two unchanged allocation assertions in tool-progress lookup and
sidebar filtering. Neither previous failures nor a pending full run are treated
as a full-suite pass. Later attachment-history changes use a separate build root
and are not validated by this draft candidate's binaries.

No existing user window, real configuration, approval policy, release EXE,
commit or remote branch was modified. The overall functional-parity goal remains
incomplete. Unsent attachment restoration was not implemented at that checkpoint;
the later implementation and current evidence are in `ATTACHMENT_DRAFT_ACCEPTANCE.md`.
Multimodal requests, PR/plugin workflows and other entries in
`FUNCTIONAL_PARITY_MATRIX.md` remain incomplete.
