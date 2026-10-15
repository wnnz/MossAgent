# Sidebar row reconciliation optimization

## Design self-check

- App owns this presentation-only refactor. Project, pinned and recent task
  lists retain their existing filtering, ordering, selection and catalog ports.
- One shared collection reconciliation helper replaces the project's repeated
  remove/insert/move loop and the task lists' clear-and-repopulate publication.
  Existing `ReplaceWith` users keep their current reset behavior.
- Equal immutable tasks retain their row instances after position changes;
  changed metadata gets a fresh row so task commands see authoritative state.
  Duplicate task occurrences remain independent, ordered row instances.
- Identical snapshots take the no-publication path without building row queues.
  Only changed snapshots build refresh-local candidate queues; there is no retained
  cross-list cache or mutable global state.
- No authorization, approval, timeout, cancellation, output, persistence or
  module dependency changes. Shared controls/styles and JIT single-file settings
  remain unchanged. Production methods remain below the 80-line hard limit.
- Public-view-model tests cover move/add/remove notifications, unchanged row
  identity, selected/running metadata, filtering, empty/retired tasks, duplicate
  task occurrences and repeated project references.

## Verification

- Before implementation, all 19 new task-list cases failed: position changes
  recreated rows and publication emitted `Reset`, including metadata-only updates.
- Post-change focused regression passed 99/99, including all 23 new task/project
  cases. The new project cases additionally cover repeated object references and
  removal of their final occurrence without resurrecting detached rows.
- `dotnet build MossAgent.slnx --no-restore -v:minimal` passed with zero warnings
  and errors. Whitespace verification for all five touched C# files passed;
  file/type counts and `git diff --check` passed.
- The first full solution test run passed 4834/4836 with no skipped tests. All
  3501 App, 8 visual, 76 browser, 83 agent, 171 infrastructure, 377 provider and
  483 built-in-tool cases passed. Two unchanged Windows cases failed under the
  concurrent run: `StopRetainsNativeResizeUntilReturnAndBlocksReplacement`
  timed out waiting for its native-resize entry, and
  `Reload_TracksEnabledUpdatedAndDeletedServers` did not discover its fixture
  tool before the startup deadline. Running the Windows project separately
  then passed 137/137, including both cases. No unrelated code was changed;
  the initial full-run failures remain recorded rather than called a clean run.

## Post-refactor native check

- Rebuilt the repository-external normal-JIT host after the refactor. Its copied
  App DLL SHA256 matched the current repository build. Fresh synthetic data lived
  in the earlier evidence root's `sidebar-post` directory; ordinary permissions,
  a light theme and a 1280-by-820-DIP window were used.
- Opened search through the actual Ctrl+K shortcut and entered `并发任务 B`.
  Project and recent lists showed only B while the A conversation stayed selected.
  Selected B through its retained sidebar row; both lists restored, B's selected
  state and message changed together, and existing conversation tabs remained.
- Opened B's row menu, pinned it and inspected the real pinned section. B left
  project/recent rows while A remained; B stayed selected in the conversation.
  A read-only query of `pinned_agent_tasks` contained precisely B's task identity.
- Used the pinned row's `取消置顶` action. B returned to project/recent lists,
  remained selected, and the pinned table was empty. No task was deleted or
  archived, no model request was sent, and no tools executed in this UI check.
- Passive Python captures saved `search-filtered.png`, `task-b-selected.png`,
  `task-b-pinned.png` and `task-b-unpinned.png`; actual screenshots were inspected.
  Host shutdown awaited the workspace and returned `QA_FINAL_ACTIVE=False`,
  `QA_EXIT=0`. Its PID was absent after exit.
- The helper initially could not click the search trigger because coordinate
  geometry was unavailable; Ctrl+K opened it. Accessibility value-setting reported
  a timeout but fresh observation verified success, so the action was not repeated.
  These are automation limitations, not silently repaired production behavior.
- The preceding external-Chrome concurrency acceptance used the pre-refactor
  build. Its results and the post-refactor native sidebar check have separate
  scope; browser regression tests, not another concurrent Chrome run, validate
  unchanged browser behavior after this App-only refactor.
- This refactor does not establish Codex pixel equivalence or whole-product
  completion. No commit, push or release is performed.
