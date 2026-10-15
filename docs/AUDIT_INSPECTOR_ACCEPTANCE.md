# Audit inspector acceptance

## Scope (2026-10-09)

The right-side execution history now uses the shared `AppAuditExecutionRow`.
It distinguishes pending, success, cancellation, denial and failure, with
14-DIP status icons aligned to the tool name and outcome label. Local start time
and elapsed time replace the old UTC-only time. Tool names are single-line,
control-character filtered and limited to 160 UTF-16 units without splitting
surrogate pairs. Narrow rows ellipsize names/metadata and wrap actionable hints.

Raw stored error codes are not displayed. An allowlist maps known failure
categories to localized hints; unknown codes cannot contribute diagnostic text.
In particular, an actual `path_denied` with `ApprovalGranted=false` points to the
project's authorized directories rather than suggesting approval escalation.

Refresh preserves the selected execution, including a selection made while the
read is pending, and rebinds equal-valued replacement records to the new snapshot.
Unlisted record instances cannot replace selection or trigger artifact reads.
The current task's unique execution rows are capped at 200. Selected-execution
artifacts are task/execution filtered, deduplicated and displayed up to 50 with
a visible limit notice. Artifact opening reuses the existing audited snapshot
preview; it does not rerun the tool or consume the conversation draft.

## Design and safety self-check

1. App owns semantic presentation, selection and shared component appearance.
   The existing tools audit reader contract remains in Tools.Abstractions and
   SQLite remains in Infrastructure. No dependency direction changes.
2. The implementation reuses `IToolExecutionAuditReader` and the existing file
   preview command/reader. The row accepts presentation strings only; it does
   not read files, execute commands, query SQLite or access model providers.
3. Existing task-scope cancellation and generation/version checks reject late
   reads after task or selection changes. No new execution/approval path or
   timeout is introduced. The visible collection caps do not make the underlying
   `GetArtifactsAsync` query bounded; its existing port still returns a full list.
   File snapshot authorization, digest/size checks and cancellation stay in the
   existing reader. Error hints never echo the stored diagnostic code.
4. Each changed handwritten C# file has one type. The scoped Roslyn audit and
   XML parse cover file/method limits. Shared styles own geometry and colors;
   markup and C# appearance gates prevent component content/appearance replacement.
5. Focused tests cover selection identity, refresh races, foreign/unlisted data,
   collection limits, localized failure hints, Unicode truncation, real arranged
   icon/text alignment and both markup/C# component boundaries. Existing inspector
   tests remain part of the focused run to check late-result and disposal behavior.

## Regression evidence

Evidence root: `%TEMP%\MossAgent-audit-inspector-20261009`.
Temporary QA projects, Python capture/comparison scripts, data and screenshots are
outside the repository. Native build output is in ignored validation artifacts.
No user database, provider credentials, paid API calls or external shell execution
are involved in native acceptance.

The first red runs captured seven failures:

- Refresh replaced the current selection with the first record.
- An oversized/foreign-task history response exceeded the task-owned UI limit.
- An unlisted record could initiate an artifact read.
- Foreign-execution artifacts and an oversized response entered the visible list.
- An equal-valued fresh record did not replace the selected record instance.
- A pending refresh overwrote the user's more recent selection.
- A real pipeline path rejection showed the generic approval hint.

The final focused run passes 77/77 tests. The four new test files contribute
27 cases: selection/bounds (6), semantic presentation (13), arranged visual
alignment (4) and appearance policy (4). Whitespace verification passes on the
eight changed/new C# files; the scoped Roslyn/XML audit passes on ten files.
The largest audited C# file is 154 lines; the largest markup is 266 lines.
The longest audited method is an existing policy method at 71 lines.

Final solution validation passes 3,268/3,268 tests across seven assemblies
(App 2,476; Providers 283; Tools 177; Infrastructure 136; Browser 76; Agent 63;
Platform.Windows 57), with no skips or failures. `dotnet build MossAgent.slnx
--no-restore -v minimal` reports zero warnings and errors before the full
`dotnet test MossAgent.slnx --no-build --no-restore` run. The native QA project
also builds with zero warnings/errors against the final source. Scoped tracked
and new-file diff whitespace checks pass; no QA or test-host process remains.

`solution-build-final.log`, `full-final.log`, `format-final.log` and
`source-final.log` contain the final checks. `full-final.log` preserves all seven
assembly summaries; the shared full-run TRX filename was overwritten between
assemblies and its final file contains only App results. The focused TRX remains
separate in `focused/audit-focused-final.trx`.

## Native interaction and raw pixels

The QA executable starts the real App with separate temporary SQLite/project/task
directories. Four results are produced by the actual standard tool executor and
SQLite audit: an authorized README preview, path rejection, invalid arguments
and unknown-tool rejection. Pending and unknown-failure rows are explicitly
synthetic layout fixtures, not proof of running or failed real tool execution.
Cancellation presentation is covered by tests, not a native cancelled-tool run.

Final normal-permission JIT runs use fresh cases:

- `verified-Light-1280-820`: Light, 1280x820, render scaling 1.
- `verified-Dark-980-640`: Dark, 980x640, render scaling 1.

In both windows, mouse scrolling reaches the successful tool row; selecting it
shows its single real README artifact. Refresh keeps the successful selection and
artifact. Clicking README opens the actual audited snapshot in the existing file
preview. Closing the preview returns to the same selected execution. The fixture
continuously verifies that the conversation draft remains unchanged. Both windows
close through the real title-bar action, finish App shutdown and report `QA_EXIT: 0`.
Final audit records remain six; no new tool is executed by opening the snapshot.

Passive Python PrintWindow captures validate dimensions and non-black output:
`window-top.png`, `window-refreshed.png` and `window-preview.png` in each final case.
The small dark panel keeps its status, wrapped path hint and artifact button
reachable by scrolling; its existing compact file-preview toolbar remains usable.

`audit-inspector-before-after.png` places unscaled Light inspector crops side by
side. The baseline uses the preceding App assembly and separate seeded data.
It shows raw diagnostic codes and UTC-only times; the final capture shows safe
hints, status icons and local metadata. The image compares MossAgent before/after,
not MossAgent against a same-state Codex reference. Timestamps differ between runs.

## Remaining limits

This change does not establish pixel-for-pixel Codex equivalence. Same-state Codex
execution-history references, higher-DPI windows, user font preferences, native
hover/focus/disabled states and real cancelled/long-running tools remain outside
this slice's native acceptance. Terminal/browser/provider/subagent workflows and
automation scheduling are not changed or newly accepted by these screenshots.

The repository stays managed/JIT for development and Windows x64 single-file for
release. No AOT, JSON serialization, packaging or persistence schema changes are
part of this slice. Existing unrelated dirty work is preserved; nothing is staged,
committed, pushed or published.
