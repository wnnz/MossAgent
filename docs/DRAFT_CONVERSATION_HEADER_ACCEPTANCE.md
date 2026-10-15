# Draft conversation header acceptance

## Scope: 2026-10-10

The native sidebar-compose follow-up exposed a stale header: starting a new
conversation cleared the selected task and inspector, but the previous task title
and plus action remained in an unselected tab strip. The cause was deriving strip
visibility only by negating `IsSingleTab`, including when no tab was selected.

`WorkspaceConversationTabsViewModel.ShowTabStrip` now also requires a selected
tab. Drafts and disposed/empty workspaces hide both the single heading and tab
strip. Existing open tabs are retained, not closed, archived or deleted; selecting
a saved task or using navigation history restores their existing instances.
Selected single/multiple conversation layouts keep their shared appearance.

## Design and safety boundaries

- App owns this presentation state. The existing catalog selection events,
  commands and bindings are sufficient; no new port, tool or dependency is added.
- Only one production condition changes. No view-local geometry, native visual
  control, additional global state, persistence schema or database migration is
  introduced. Existing property notifications publish the derived state.
- Selection does not start a model request, approve a tool or replay work.
  Authorization, approval, task concurrency, cancellation and audit remain in
  their existing execution paths. History commands retain locked-state checks.
- All three changed handwritten C# files contain one type and stay below 500
  lines. The longest method is 68 lines, below the 80-line hard limit.
- Debug remains JIT; publishing remains Windows x64 single-file without AOT.
  No staging, commit, push or publish occurs.

## Regression evidence

- Initial test-authoring attempts used the wrong history command names and
  sidebar appearance-class name. Those compile/selector errors are corrected
  before treating the failures as evidence of the production issue.
- The corrected 13-case run fails against the original condition: all 12
  theme/width/tab-count cases fail on the visible stale strip, and the lifecycle
  case fails on initial empty-workspace strip visibility.
- New cases use the real MainWindow and composition root, sidebar-button input,
  catalog back/forward commands and synthetic SQLite/provider fixtures. They
  cover light/dark themes at widths 980/1280/1920, single/multiple retained tabs,
  task persistence without archiving, locked new-conversation rejection and
  disposed command/subscription ownership. No provider requests are sent.
- The existing draft-heading test now rejects a visible stale tab strip as well
  as the stale single heading. Its tab-reselection/persistence behavior remains.
- The 115-case targeted run passes. Focused whitespace verification passes after
  expanding one new test's object initializer; the solution build succeeds with
  zero warnings and errors. The subsequent full run passes 4298/4298 cases across
  eight assemblies, with zero failed or skipped cases. TRX and logs stay in the
  external evidence directory below. Repository-default `git diff --check` passes;
  the index remains empty.

## Native evidence and remaining scope

The ordinary-permission QA host uses the real App/MainWindow and new synthetic
data, not a visual mock or the user's database. Reference capture/interaction
artifacts stay outside Git:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-draft-header-20261010`.

- The fresh before capture is 1920x1032 at native scale. An actual sidebar click
  produces `selected=none; heading=False; strip=True; retained=1` and leaves the
  previous title visible in `before-light-draft.png`.
- The before host closes normally with both window/lifetime exit messages.
- The repaired 1920x1032 light window is exercised through actual sidebar,
  back and forward clicks: new/forward draft states have no title/strip, while
  back restores the selected title and inspector. Logs retain the same one
  open tab across all three states.
- The ordinary-permission 980x640 dark window also hides the old title/strip
  after a real sidebar click. Both after hosts report `QA_ELEVATED: False` and
  close normally. Dark empty-state wrapping is visibly still a separate gap.
- `before-light-draft.png`, `after-light-draft.png`, `after-light-back.png`,
  `after-light-forward.png` and `after-dark-draft.png` retain full native captures.
  The unscaled before/after header crop in `draft-header-comparison.png` changes
  from 681 foreground pixels below luminance 210 to zero in bounds
  `(320,48)-(660,92)`. This measures removal of stale chrome, not exact Codex
  contour/antialiasing equivalence. No screenshot is stretched.
- Read-only post-exit inspection of all three synthetic databases confirms nine
  stored tasks, one pinned task, zero archived tasks and zero messages per host.
  Counts are retained in `native-storage-verification.json`; no provider secrets
  or user database contents are queried. No QA host remains running.

This is a header ownership correction, not proof of whole-page Codex fidelity or
completion of the full product goal. Codex draft-state screenshots, every DPI,
responsive empty-state wrapping and all tool/provider workflows remain separate
acceptance requirements.
