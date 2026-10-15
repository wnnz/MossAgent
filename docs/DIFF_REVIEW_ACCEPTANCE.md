# Diff review acceptance

## Design and safety self-check (2026-10-09)

1. Tools.BuiltIn owns Git arguments, authorized path preflight, process execution
   and bounded output. App owns selection and localized loading/failure/empty
   presentation. No infrastructure or UI dependency is added to the tool modules.
2. Review continues through `IToolExecutor`, `git.status` and `git.diff` rather
   than reading files or starting processes in UI. Untracked single-file review
   extends the existing diff contract and does not stage or modify user files.
3. Existing authorization, read-only approval, task cancellation, process-tree
   cleanup and output limits remain in the standard pipeline. New untracked mode
   requires one authorized file, rejects staged/directory requests, and disables
   external diff/textconv helpers. The Git runner accepts diff exit code one only
   for the explicit mode and when stderr is empty. Command-local `core.safecrlf=false`
   suppresses irrelevant read-only LF/CRLF conversion warnings without ignoring real
   errors or modifying the repository/user configuration.
4. Changes stay in separate single-type C# files and existing wrapper controls.
   Loading/empty/failure rendering reuses the shared empty state; no per-view
   colors, dimensions or native visual controls are added.
5. Focused tests must cover actual untracked text/binary Git output, missing paths,
   outside-root requests, staged/directory rejection, literal pathspecs, selection
   identity and late results. Native acceptance must use temporary real Git/SQLite
   data, preserve the draft and index, and exercise both themes/window sizes.

## Implemented behavior

- `git.status` lists individual nested untracked files rather than directory
  placeholders. An explicit `git.diff` untracked mode uses a no-index comparison
  against the platform null device. Added text receives real Git hunk/line data;
  binary additions retain Git's binary-file presentation. Neither mode stages files.
- Git paths are literal, Unicode header names remain readable and external
  diff/textconv helpers are disabled. An empty/missing file path, directory, staged
  combination or outside-root request cannot turn into unrestricted recursive review.
  A missing file that returns Git exit code one on stderr is still a failure.
- Refresh captures selection after the status response and preserves the current
  path if it remains listed. Equal-valued replacement records rebind by reference;
  unlisted instances cannot initiate tool reads. Filter changes update counts and
  retain the selected path when it also exists in the destination filter.
- Actual diff text is separate from localized loading, failure and empty messages.
  Errors no longer appear as fake code. Existing task/selection cancellation and
  version checks continue rejecting late responses. File preview still uses the
  existing authorized snapshot tool/reader and preserves the conversation draft.

## Verification evidence

Evidence root: `%TEMP%\MossAgent-diff-review-20261009`.
Native harness code, Python capture/comparison scripts, Git/SQLite fixtures and
screenshots remain outside the repository; build output is in ignored artifacts.
No paid provider, user project/index or user database participates in acceptance.

Four initial App selection tests fail red, covering refresh, pending selection,
unlisted records and the former untracked placeholder. The tool red run captures
untracked text/binary/directory/missing-path gaps and nested directory placeholders.
The tracked literal/textconv failure is reproduced separately after fixing only
the test fixture's Windows read-only Git-object cleanup. A later real CRLF probe
and red test expose valid diff exit code one being incorrectly rejected because
Git emits an LF/CRLF warning. The final command-local warning suppression fixes
that case while retaining the missing-file stderr rejection.

New tests contribute 18 cases: App selection (4), App states (3) and real Git
tools/pipeline behavior (11). The focused App run passes 146 tests including
existing inspector/file-preview/parser regressions. The final focused tool run
passes 15 tests including existing Git status/preflight boundaries.

The final full solution run passes 3286/3286 tests with zero failures or skips:
App 2483, Tools 188, Providers 283, Infrastructure 136, Browser 76, Agent 63
and Platform.Windows 57. `full-verified.log` and the seven independent TRX files
under `full-verified/` record this run. Final build logs are
`solution-build-verified.log` and `native-verified-build.log`; scoped formatting
and structural checks are recorded in `format-verified.log` and `source-verified.log`.

Scoped whitespace verification and the Roslyn/XML audit cover the nine changed/new
C# files and the inspector markup. Every handwritten C# file contains one type;
the largest is 179 lines, markup is 266 lines and the longest method is 45 lines.
Native and solution builds report zero warnings/errors. No persistence schema,
serialization, AOT or release-packaging change is introduced.

## Native interaction and pixels

The real App runs under ordinary permissions/JIT in Light 1280x820 and Dark
980x640 at render scaling one. A separate repository contains an actual tracked
file with different baseline, staged and working contents, a nested Unicode
addition and a binary addition. All review/preview calls use the production tool
executor and persist actual SQLite audit records.

Mouse interaction in both themes verifies selecting the added file, refresh
retention, authorized file preview, returning to the same selected path and
staged/unstaged filtering with counts three/one. Horizontal scrolling confirms
the staged content is baseline-to-staged and the unstaged content is
staged-to-working, rather than a cosmetic toggle. The small dark panel retains
reachable preview/refresh/filter controls and the existing compact preview toolbar.

The Light fixture also shows the real binary result. Removing only its synthetic
untracked file externally produces the localized empty/error state, not raw Git
stderr. Restoring that fixture file and refreshing restores the added patch.
The single failed Light audit is this intentional missing-file negative case;
Dark has no failed audit. Continuous checks confirm the draft remains unchanged.
Final Git cached diffs equal their pre-UI values and both cases exit normally with
`QA_INDEX_UNCHANGED: true` and `QA_EXIT: 0`.

The Light/Dark interaction runs precede the final command-local `core.safecrlf`
argument; their UI/renderer sources are unchanged by that tool-only repair.
`verified-CRLF-Light-1280-820` reruns the latest production assemblies against
the synthetic repository with `core.autocrlf=true`. Selecting the nested addition
and refreshing both return the real patch; all five ReadOnly audits succeed,
cached diff content remains unchanged, stderr is empty and the process exits zero.
After exit, the repository still has `core.autocrlf=true` and no local
`core.safecrlf` setting, confirming the read-only command does not persist it.
`QA_INDEX_UNCHANGED` compares cached diff content, not byte identity of Git's index.

Passive Python PrintWindow validates non-black output and exact dimensions.
`diff-review-before-after.png` uses unscaled 455x776 inspector crops. Before uses
the preceding App/tool assemblies and shows the former directory placeholder;
after uses the latest CRLF-case capture and shows the real nested-file patch.
It is a MossAgent before/after comparison,
not a same-state Codex reference. Individual captures include untracked, refreshed,
preview, staged, horizontal-scroll and negative/recovery states.

The first standalone QA harness captured the stopped Avalonia synchronization
context while synchronously waiting for its final external Git check after window
close. The harness-only async helper now uses `ConfigureAwait(false)`; two initial
cases are excluded from shutdown acceptance. Corrected baseline and final cases
exit normally. No production App shutdown behavior is changed by this repair.

## Remaining limits

A same-state Codex diff reference is still needed for pixel-fidelity acceptance.
Higher-DPI/user-font combinations, native loading/empty states, disabled/keyboard
focus/hover geometry, split-view and sticky-gutter behavior are not newly accepted
by these screenshots. Pending/empty/late-response states are covered by focused
tests instead. These reads do not implement staging, discard, commit or other
mutation actions. Existing managed/JIT development and Windows x64 single-file
release settings remain unchanged; nothing is staged, committed, pushed or published.
