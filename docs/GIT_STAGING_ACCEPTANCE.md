# Git file staging workflow

## Plan and required self-check

1. Tools.BuiltIn owns separate stage/unstage tools; Infrastructure implements a
   portable repository-authorization port using the existing bounded Git identity
   inspector. App coordinates commands without accessing files or processes.
2. Reuse the standard tool pipeline, owned process launcher, worktree guard,
   shared controls, status parser and diff pane. Add no shell-command bypass.
3. Require write/process capability and normal approval. Authorize the complete
   repository and metadata, capture native Git directory arguments, validate
   literal file paths and rename sources, deny directory/metadata/link/path
   escapes, bound operations and await cancellation cleanup. Never stage all,
   commit, push or delete working files implicitly.
4. Keep ports, scopes, tools, helpers, view models and test fixtures in separate
   one-type files. Do not grow the 499-line WorkspaceViewModel or App.axaml.
5. Prove real temporary-repository stage/unstage including unborn HEAD, deletion,
   rename and literal names; test schema, policy, denial, external metadata,
   links, cancellation/timeout, output/audit and selection lifetime. Verify the
   shared-control UI in light/dark Headless rendering and build the solution.

## Implemented workflow

- The diff inspector offers stage or unstage for the selected file in its current
  working-tree/index filter. It uses the production tool executor, not a direct
  UI process call, and refreshes real status after success. ReadOnly and
  projectless/disabled-tool contexts cannot invoke the action.
- `git.stage_file` uses `git add -- <literal files>`; `git.unstage_file` uses
  `git reset --quiet -- <literal files>`, including before the first commit.
  Neither requests a working-file reset or a commit. A rename includes its source
  only on the selected rename side; a copy never unstages independent source edits.
- Infrastructure authorizes the complete discovered repository and its metadata
  under a cooperative thirty-second ceiling. Nested directory access alone does
  not grant the parent repository. External standalone metadata requires explicit
  additional authorization; linked worktrees reuse the existing identity checks.
- Commands pin the captured Git/worktree directories and disable pathspec magic.
  Explicit files and optional rename sources are validated before approval and
  again before native execution. Directories, `.git` paths, path escapes, link
  ancestors, ADS, trailing aliases and DOS device names are rejected.
- Both tools declare write/process capability and external-side-effect risk.
  Git filters or hooks may execute programs; this is approval classification,
  not an operating-system sandbox. The standard task-workspace guard runs on both
  approval boundaries; timeout, cancellation, output bounds and audit remain in
  the shared pipeline. Authorization timeouts receive the tool-timeout result.
- The action view model snapshots roots, prevents duplicate execution, exposes
  cancellation, ignores obsolete-task results and awaits actual tool/refresh
  cleanup on asynchronous disposal. Interrupted operations explicitly warn that
  the index may already have changed and require checking status before retry.
  Failures show localized guidance rather than raw process/exception output.

## Deterministic evidence

Tests use temporary repositories, production App composition, real SQLite/audit,
owned Git processes, controlled process/authorization stubs and Avalonia Headless.
They do not operate user windows, use real credentials or invoke paid APIs.

- Real index checks cover committed/unborn repositories, Chinese and bracketed
  literal names, rename, deletion, source-copy isolation and external file removal.
  Unselected files, working bytes and HEAD remain unchanged by the core operations.
- Negative checks cover denied approval, ReadOnly, malformed schemas, unsafe
  destination/rename source, independently stored metadata and repository scope.
  Real junctions are denied even when both destinations are authorized and when
  the link appears during approval. Fixtures unlink junctions before cleanup.
- Controlled cancellation/deadline cases wait for actual authorization/process
  cleanup before returning or completing audit; no index command starts after
  interrupted authorization or a revoked task-workspace guard. Both streams stay
  bounded and captured command arguments preserve literal repository/file identity.
- UI checks cover localized failures, root snapshots, duplicate prevention,
  task switching, non-cooperative operation/refresh disposal and source selection.
  Production keyboard actions drive real stage/unstage and verify filter refresh,
  index contents and successful persisted audits; failed Git writes persist failure.
- Light/dark Headless frames at 280 and 640 DIP verify keyboard activation,
  busy/disabled controls, cancellation and wrapped interrupted-state feedback.
  Eight images are component renders, not native desktop screenshots.

## Validation status

The first focused tools run retained four test-fixture failures caused by a
duplicated temporary-directory separator; the corrected run passed 38/38.
Expanded UI rendering initially checked a bound button before the asynchronous
relay command delivered its completion notification. The bounded eventual-state
check passed 22/22, including production Git and four visual cases. Original
reports remain in `artifacts/git-staging-20261018/results`.

The final expanded checkpoint in `artifacts/git-staging-20261018` has terminated
with all seven recorded commands exiting zero and six Completed test reports:

| Validation | Result |
| --- | --- |
| Complete Tools assembly | 549/549 |
| Complete Infrastructure assembly | 442/442 |
| Complete Agent assembly | 140/140 |
| Related App workflows, excluding names containing Native | 1,124/1,124 |
| Complete shared Visual assembly | 300/300 |
| Real Windows Git process-ownership cases | 8/8 |
| Total above, without overlapping focused runs | 2,563/2,563 |
| Complete solution build | Zero warnings/errors |
| Changed handwritten C# one-type/size/syntax audit | 261 files, no violations |
| Maximum checked C#/AXAML file and method length | 499 / 72 lines |

There are no failed or skipped cases in the six final reports. A separate
current-binary focused App run passes 24/24, including real copy-source
preservation and externally removed-file failure; these cases are already in
the related-App total and are not added again. Original failing/intermediate
reports remain separately available. This is not a complete-solution test run.

`validation-terminal.json` retains actual command exits, arguments, timestamps
and counters. All 2,135 source/configuration entries and 2,439 binary/runtime
entries retain their SHA-256 identities. The source ZIP's entry names, lengths
and bytes match the source manifest, and `identity-path-set-check.json` checks
that neither source nor runtime paths were added or removed. Documentation is
outside the tested code/configuration manifest. The earlier restored-worktree
candidate's 7,355-case full pass uses pre-staging binaries and is not counted as
complete regression of this candidate.

## Remaining limits

File-level staging is not the still-missing commit/push/PR, stage-all or hunk
workflow. Git index locks are surfaced as failures rather than silently removed.
Cancellation does not roll back an index mutation that already happened, nor
automatically replay it. Checks do not pin native filesystem identities and do
not prove handle-level TOCTOU protection or isolation of repository hooks.
Native-window/restart acceptance and the overall product remain incomplete.
No changes are staged in this checkout, committed, pushed, packaged or released.
