# File preview workflow acceptance

Verification: 2026-10-09. This extends `FILE_PREVIEW_ACCEPTANCE.md` and
`FILE_PREVIEW_FIDELITY_ACCEPTANCE.md`; it is not complete product acceptance.

## Design self-check

1. App owns picker coordination and shared preview layout. `AppWorkspacePanes`
   owns expansion, retained content and preferred splitter width; the main window
   view model does not absorb more layout or file-selection responsibilities.
   Conversation and ordinary inspector headers reuse its guarded full-view commands.
2. The existing picker port only supplies paths. Reading reuses
   `FilePreviewPaneViewModel.OpenAsync`, the standard preview-file tool pipeline
   and the task-artifact reader. Selecting a file never authorizes its directory.
3. Task roots, approval policy, bounded snapshots and audit persistence are
   unchanged. Selection has a five-minute deadline for human browsing; individual
   preview reads retain their existing 30-second execution context. Cancellation
   rejects late paths without abandoning ownership of a native dialog still open.
   Full-view commands reject detached, hidden or disabled owners; opening updates
   the existing conversation-owned panel state through the two-way binding.
4. All affected handwritten C# files have one type and remain below 500 lines;
   methods remain below 80 lines. Views use shared controls and appearance variants.
   New labels and safe notices use the existing localized inspector resources.
5. Tests exercise actual authorized and unauthorized reads, deduplication, tab
   limits, missing services, cancellation, task/policy changes, noncooperative
   pickers, late completion, expansion lifecycle and retained draft/tab state.
   Header regressions additionally cover tab aliases, command replacement,
   disabled commands and independent startup/activity state in both themes.

## Implemented behavior

- The preview header has actual add-tab, expand/restore and close-panel actions.
  Add-tab offers file selection and the existing return-to-sources command.
  Unavailable expansion and an absent task are disabled, not inert text buttons.
- Picker state is separately owned, blocks reentry and remains occupied until
  a noncooperative picker returns. Hiding, disposal and task/policy/root changes
  cancel the request; stale paths cannot populate a subsequently selected task.
- At most eight paths are accepted per selection; empty paths are ignored and
  case-insensitive duplicates share existing tabs. The overall eight-tab limit
  remains enforced by the preview owner. Failures use fixed localized notices,
  never raw exception text or an extra unvalidated file read.
- Expansion hides rather than destroys the primary pane, gives the inspector
  the whole workspace and hides the splitter. Restoring preserves its preferred
  width, subject to the existing small-window minimum/maximum constraints.
- The conversation header can enter full view directly from the collapsed panel.
  Ordinary inspector tabs now offer the same expand/restore action as file preview;
  the labels distinguish entering/exiting full view from opening/closing the panel.
- Shared headers recalculate derived state only for consumed properties. Inspector
  selection reuses one instance-owned tab list and the existing command parameters.
  Workspace panes share presenter creation, lifecycle guards and command notification;
  collapsed and expanded layouts share their non-docked width reset.
- Closing/replacing/detaching the workspace, losing ownership and hidden/disabled
  ancestors reset expansion. Returning from the expanded file preview to sources
  also restores the primary pane, leaving a visible route to normal interaction.
- After a resize or expansion transition, the active file tab is brought back
  into the horizontal viewport. Search Escape returns focus to the visible copy
  action instead of the return action now inside the add-tab menu.

## Native interaction and screenshot evidence

Evidence is outside the repository:
`%TEMP%/MossAgent-preview-workflow-20261009`.
The Python capture/comparison scripts, isolated fixtures and QA host are not
product files and are not submitted to the repository.

- The host uses the production App/MainWindow, ordinary permissions, scale 1,
  isolated SQLite data and a completed read-only task. Synthetic files are
  previewed through the actual authorized tool executor and artifact reader.
  A loopback port-nine provider is seeded but never called; no message is sent.
- Light 1280x820: add-tab opens the real native picker. Selecting
  `SelectedSample.cs` produces a third tab, syntax-highlighted content and a
  separate bounded artifact snapshot. A previously timed-out selection produces
  a safe cancellation notice and no new tab or snapshot.
- The 30-second selection deadline was too short in native use: one dark picker
  returned at roughly 30.5 seconds and correctly discarded the result, but this
  made normal browsing impractical. Selection is now separately bounded at five
  minutes; deterministic clock tests cover both 45-second success and expiration
  without waiting five minutes. The file-read timeout is not extended.
- Final Dark 980x640: the native picker remains open for about 142 seconds before
  selection, then successfully loads the real third file tab and creates its
  snapshot. Expanding and restoring retain the active tab in view; closing the
  panel restores the floating project card. See `dark-verified/` screenshots.
- Light interaction retains an unsent synthetic draft while dragging the splitter,
  expanding, shrinking the native window from 1280x820 to 1080x720 and restoring.
  Returning to sources while expanded restores the conversation and its draft.
  See `light-final/resized-inspector-draft.png`, `expanded-narrow.png`,
  `restored-narrow-draft.png` and `back-restores-draft.png`.
- Passive PrintWindow captures the current Codex window at 1936x1048, including
  DWM edges, and Light MossAgent at 1920x1032. Both preview the same synthetic
  image. `preview-header-comparison.png` places original-pixel 754x238 and
  752x238 crops side by side without scaling, aligned to each inspector's origin.
  Tab counts differ; this is not a whole-window pixel-difference score.
- Separate native popups are observed through Computer Use; main-window
  PrintWindow alone does not establish popup rendering or interactivity.
  The Codex reference is captured passively; no Codex UI input is automated.
- All five native QA hosts close normally with exit 0 and empty stderr logs;
  no QA process remains running. They include the rejected 30-second selection
  and the final successful longer selection, not just the successful cases.

## Verification of initial preview workflow

- Final focused preview/workspace/search regression run: 128 passed, including
  real pipeline rejection of a picked file outside the task roots and successful
  reading only after explicit secondary-root authorization.
- Shared-control encapsulation and appearance gates remain enabled. Scoped
  whitespace checks, Roslyn single-type/size audits and AXAML/XML checks pass.
- Final solution build succeeds with zero warnings/errors.
- Latest full App run: 2436 executed, 2433 passed and three failures during
  sidebar-heading temporary-directory deletion with a sharing violation. All
  12 sidebar-heading cases pass on isolated rerun. A preceding full App rerun
  also encountered a SQLite handle-disposal exception during compatible-stream
  fixture initialization; all six compatible-stream cases pass in isolation.
  These are not a clean full-suite result; unrelated cleanup code is unchanged.
- Other complete suites pass: Agent 63, Browser 76, Infrastructure 124,
  Platform.Windows 54, Providers 283 and Tools 174. Combined latest evidence is
  3210 executed, 3207 passed and three failed, not 3210 passing tests.
  Full reports are in `full-tests` and `app-verified`; focused and isolated
  reports are in `verified-final`. Earlier failed attempts are retained too.
- No staged files, commit, push or release is created. Existing unrelated work
  and test cleanup code are preserved.

## Inspector full-view and shared-header cleanup follow-up

Evidence: `%TEMP%/MossAgent-controls-cleanup-20261009-2310`.
Earlier full-view evidence remains in
`%TEMP%/MossAgent-tool-deadline-20261009-213806/native`.

- The inherited 3805-test run had five failures: four obsolete binding/margin
  expectations and one sharing violation when a command-flow test read its child
  PID file. Binding expectations now specify two-way state and the measured card
  inset is eight DIP. The command-flow implementation/test is unchanged; its
  isolated rerun and the final full suite pass, not proof that the race is fixed.
- Four new header-state cases cover both themes. Final focused regression:
  100 passed. Final solution build: zero warnings/errors. Scoped formatting,
  Roslyn single-type/file/method audits and affected AXAML/XML checks pass.
- Final full solution test run: 3809 passed, zero failures/skips across seven
  suites: App 2876, Agent 63, Browser 76, Infrastructure 144, Platform.Windows 129,
  Providers 302 and Tools 219. Original failures and the rejected cross-thread
  initialization of the new test fixture remain in their separate reports.
- Native Light 1920x1032 and Dark 980x640 both enter inspector full view,
  restore split panes and close back to the floating card without losing the
  synthetic unsent draft. Python captures are passive original pixels; native
  resize/theme/button inputs use Computer Use. DPI remains 96; no global scale
  or forced DPI change is introduced. Screenshots include `moss-light-full-view`,
  `moss-light-restored-draft`, `moss-dark-narrow-full-view`,
  `moss-dark-narrow-restored-draft` and `moss-dark-narrow-final` PNGs.
- The QA process uses isolated synthetic data and a loopback-only provider.
  No message/model request is sent or daily database read. It closes normally
  and stderr is empty. Screenshot scripts, QA data and logs remain outside Git.
- This refactor changes no authorization pipeline, persistence schema,
  model routing, JIT or Windows x64 single-file publishing settings. Fewer
  redundant refreshes/temporary tab arrays are established by the implementation;
  no measured end-to-end speedup or whole-product visual fidelity is claimed.

## Remaining limits

- Shared toolbar shadows, image clipping and narrow breadcrumb allocation were
  implemented in the later `FILE_PREVIEW_FIDELITY_ACCEPTANCE.md` follow-up.
  Copy/image glyph dimensions and the Explorer badge are further verified in
  `PREVIEW_ICON_ACCEPTANCE.md`. Other action semantics and dark/narrow Codex same-state references,
  expanded reference geometry and its add-tab menu remain outstanding.
- External-editor selection is not implemented; the existing open action only
  reveals an authorized original file in Explorer through the tool pipeline.
- This slice does not validate automation scheduling, actual provider calls or
  whole-product one-to-one fidelity. The broader goal remains active.
