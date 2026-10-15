# Conversation timeline acceptance

## Scope: 2026-10-12

The shared conversation timeline groups the existing message instances into
user turns, intermediate execution activity and a final reply. Completed turns
collapse activity by default; running turns expand it. A user's explicit choice
survives stream updates and completion. Replies remain outside the disclosure.
The extracted message view retains attachments, Markdown, code-copy commands,
tool details and nested subagent dialogue.

Recorded message timestamps flow through the existing history mapper. A live
reply receives its actual completion timestamp after persistence, including
failure/cancellation paths when a non-empty reply exists. Labels use the local
time zone and localized today/yesterday/date and duration resources. Missing,
backwards or incomplete timestamps never invent a duration. Tool-only history
remains accessible without manufacturing a user bubble or reply.

## Design self-check

1. App owns this synchronous presentation projection. Domain, Application,
   providers, tools and Infrastructure dependencies remain unchanged.
2. Existing conversation timestamps, repository ports, message instances and
   shared collection synchronization are reused. No new execution port is needed.
3. Disclosure and timestamps do not execute tools or HTTP requests. Authorization,
   approval, timeout, cancellation, output limits and audit remain in the existing
   pipeline. Dispatcher marshaling is retained for live timestamp publication.
4. Each handwritten C# file contains one type. After the incremental-refresh
   optimization, the timeline model is 141 physical lines and its turn model is
   83; all methods remain below 40 lines. The existing workspace model remains
   below the 500-line hard limit.
   Shared AXAML controls and semantic variants own appearance; App.axaml is 495
   lines, and the timeline controls do not introduce native visual controls.
5. Focused tests cover completed/live/orphan turns, missing/backwards timestamps,
   duplicate prompt text, stream identity, manual disclosure, activity availability,
   source replacement, removal, detachment, disposal and late updates. A real
   settings/model/MCP/SQLite fixture covers approval and denial plus reopen without
   model/tool replay. Headless rendering covers both themes and 280/740-DIP widths.

## Simplification and reproduced failures

- Turn projection scans ranges in the existing source rather than copying every
  group into a list-of-lists. Activity filtering avoids its previous LINQ closure
  and array copy; turns without activity allocate no activity list.
- Derived visibility and command availability notify only when their values change.
  Two new tests fail before this adjustment: unchanged history republishes flags,
  and activity availability publishes four times instead of the two transitions.
  Both pass after the change. This is structural/allocation work, not a measured
  claim about whole-application latency or memory.
- Clearing and rebuilding active messages discarded turn identity and disclosure
  state in the full fixture. The catalog now uses existing SynchronizeWith; the
  original identity assertions pass without weakening their expectations.
- A native intermediate screenshot exposed empty response bubbles and ghost
  copy/tool controls after cancellation. Visibility bindings now explicitly target
  the parent turn rather than the message view's changed DataContext. Two negative
  rendered tests cover tool-only history with and without a user message.
- The final shared heading uses CardBorderBrush instead of BorderDefaultBrush.
  Offline samples from actual saved screenshots read RGB(237,237,237) for both
  the Codex reference divider and the final MossAgent light-theme divider.
- The temporary reflection-based timestamp contract assertion is replaced with
  direct property access. No database migration, stored JSON shape, model context,
  dependency, publish setting or source-generated JSON change is introduced.
  Debug stays JIT; publishing stays Windows x64 single-file without AOT.

## Verification

- The first two functional contract tests fail against the previous UI. The first
  presentation regression passes 79/79. Expanded verification exposes six failures;
  after correcting catalog synchronization, a 49/49 run includes complete approval,
  denial, model/tool identity, restoration and layout cases.
- Final focused verification passes 74/74, including all 28 timeline cases and
  adjacent conversation, attachment, tool and subagent presentation regressions.
- The final serial solution run passes 5504/5504 across eight assemblies with no
  failed/skipped cases, including 3965 App cases. A later test-only whitespace
  correction is rebuilt with the solution and the same 74 focused cases pass
  again; production code and its native-verified DLL hash are unchanged.
- The solution builds with zero warnings/errors. Scoped whitespace verification and
  the repository whitespace check pass. The full Roslyn audit checks 1535 source
  files for the one-type, 500-line and 80-line-method limits; all pass. All 104
  AXAML files stay within the 500-physical-line limit.

## Native screenshot and interaction checks

The normal-privilege production Debug app uses an external synthetic data copy,
not the user's database. Its managed DLL SHA-256 is
`F1C6749B9F99AA6684E3124636E194514CA4501A937C6FBA8E336C80AA75A6BF`.

- Passive Python capture produces non-black 96-DPI images at 1280x820, 980x720
  and maximized 1920x1032, with light/dark captures. The Codex reference is a
  passive 1920x1032 screenshot; no Codex UI input is performed.
- Clicking the duration expands the existing subagent execution details. Enter
  collapses them again without hiding the final reply. Cancelled/tool-only turns
  no longer render empty reply controls. Reply-copy entries remain visible.
- Theme changes use only the synthetic copy's appearance page. The original light
  preference is restored, and the test app closes normally. Its stdout/stderr are
  empty. Task/message/tool/artifact counts remain 1/11/6/0, and all four persisted
  table hashes are unchanged after intermediate and final app sessions.
- No new model request, approval decision or tool execution is triggered by native
  verification. Paid/production-provider calls and clipboard transfer are not
  newly tested here; deterministic fixtures retain execution and rendering coverage.

Logs, TRX, process identity, passive Python screenshots and metadata evidence stay
outside Git in
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-goal-fidelity-20261012-180814`.
The screenshot scripts are not added to the repository. No staging, commit, push
or publishing occurs. This slice does not complete whole-window pixel fidelity
or the broader product goal.

## Incremental refresh optimization: 2026-10-12

This change remains an App-owned synchronous presentation projection, using the
existing messages and collection synchronizer. It adds no execution port, I/O,
database migration, stored JSON change, appearance variant or shared global state.
Authorization, approval, cancellation and output limits remain in the tool pipeline.
Debug remains JIT and publishing remains Windows x64 single-file without AOT.

- Collection add/remove/replace updates only affected subscriptions. Occurrence
  counts retain one subscription for repeated message references until the last
  occurrence leaves the source. Moves do not rescan or rebuild subscriptions;
  resets, source replacement and disposal still release the owned handlers.
- Unchanged turn projections retain the anchor dictionary and observable rows.
  Changed groups rebuild the current anchor mapping before publishing rows, so
  removed/reinserted turns cannot resurrect discarded disclosure state.
- Activity projection compares the existing filtered prefix directly. It creates
  a temporary list only after divergence; tail removals trim existing rows.
  Actual changes still use the shared identity-preserving collection synchronizer.
- Turn ranges still scan the source, and changed grouping reindexes its anchors.
  This is not an overall constant-time update or an application-latency claim.

Before production edits, all seven new allocation cases fail their ceilings;
the five new subscription behavior cases already pass. After the changes, all
seven allocation cases pass without weakening their limits. Warmed Debug samples
use synthetic messages without recorded timestamps:

| Case | Before bytes/operation | After bytes/operation |
| --- | ---: | ---: |
| Unchanged history, 32 turns | 5,504 | 0 |
| Unchanged history, 256 turns | 49,104 | 0 |
| Unchanged history, 1,000 turns | 206,816 | 0 |
| Invisible move and move-back, 256 messages | 13,232 | 144 |
| Invisible add and remove, 256 messages | 13,392 | 416 |

These measurements exclude initial binding, message/Markdown construction and
changed timestamp labels; they do not mean the real application allocates no memory.
A repository-external reference comparison uses the two saved original production
implementations: 20,000 deterministic mutations across 200 histories retain
equivalent projection, duplicate-reference ordering, row continuity, disclosure,
timestamps and command availability. Added regression cases also cover removing
and reinserting a turn, trailing response transitions, reset and independent owners.

The first external-output run passes 39/40 timeline cases; its sole failure is an
existing source-contract test locating the repository by walking output ancestors.
Using a separate ignored output beneath the repository passes 115/115 timeline
and adjacent tests without modifying that test or overwriting the running app.
After adding the two final reconciliation regressions, the complete App assembly
passes 3,979/3,979 cases and the visual assembly passes 8/8, without failed/skipped
tests. The fourteen added cases are included in that full run. All five touched
C# files contain one type and stay below 250 lines; scoped whitespace and the
repository diff check pass. The solution builds with zero warnings/errors.
Logs, original source copies,
TRX and the comparison project remain outside Git under
`%TEMP%/MossAgent-code-optimization-20261012`; isolated build output is ignored under
`.scratch/timeline-optimization-20261012`.
The existing native App session is not restarted. This slice does not newly
validate native pixels, live providers or the broader product objective.

Subsequent native verification of the optimized production build is recorded in
`BROWSER_CONCURRENCY_ACCEPTANCE.md`: running/cancelled/completed turns, independent
task switching, restored duration and real disclosure expansion retain their
messages and final reply. Normal and maximized light-theme captures and no-replay
restoration supplement the earlier evidence, not a full visual-state sign-off.
