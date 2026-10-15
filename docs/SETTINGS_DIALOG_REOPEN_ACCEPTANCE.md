# Settings dialog reopen acceptance

## Native diagnosis: 2026-10-12/13

The current production App was exercised with a copy of a normally closed,
synthetic concurrency database. No user database or real provider account was
used. Dark theme survives a normal close/reopen, completed task B restores its
tool activity and final reply, and opening the browser inspector does not restart
an external browser or replay tools. Passive Python captures record 96-DPI
windows at 1920x1032, 1280x820 and 980x640.

The narrow model editor exposed a real lifecycle defect: after scrolling its
body, cancelling and opening it again, autofocus brings the first input into
view while leaving its preceding label above the viewport. The editor therefore
starts with a clipped `模型 ID` label instead of its initial layout.

The corrected headless baseline reproduces a 20-DIP retained offset in all six
light/dark and window-size cases, plus the nested-scroller case. Seven cases fail
and four preservation cases pass before changing production code. An initial
test-fixture failure attempted to reparent inputs without first removing them
from their original panel; that test-authoring issue is corrected separately.

## Design self-check

- App's shared `AppSettingsDialog` owns the body viewport and its open/close
  lifecycle. The existing styled open property and shared template suffice;
  no port, dependency or alternative dialog implementation is introduced.
- A new open session resets only the template-owned body before visibility and
  autofocus. Returning to an already-open page retains its position and remembered
  field. Explicit focus chosen after opening can still scroll to a later input.
  Nested content scrollers keep their own positions.
- No authorization, approval, task execution, timeout, cancellation, output,
  persistence, HTTP or provider operation changes. Existing focus revision guards,
  availability checks, host subscriptions, Escape and close-command ownership stay
  in place. No additional event subscription or global state is introduced.
- The template continues using shared visual wrappers and appearance variants.
  Naming the owned body changes no geometry, colors, typography or footer scope.
  All changed files remain below the 500-line hard limit and keep one C# type.
- Eleven new cases cover reopen, labels, focus/drafts, retained open pages,
  explicit lower-field focus and nested-scroller ownership. Existing dialog,
  editor, focus and component-boundary cases must also pass, followed by a solution
  build and an ordinary-permission native replay.

## Evidence ownership

Artifacts stay outside Git at
`%TEMP%\MossAgent-current-production-matrix-20261012-235235`.

- The before App's managed assembly hash is
  `9F978AD9F19D31444202274AB415F388859D81698167A45FE43C13AF758B9961`.
  `runtime-files.json` records its module identities; an unchanged App-host EXE
  hash alone does not prove the managed App version.
- `dark-narrow-model-editor.png`, `dark-narrow-model-editor-scrolled.png` and
  `dark-narrow-model-editor-reopened-before.png` record the real reproduction.
- Clearing the synthetic model ID and attempting local save produces actionable
  `请选择供应商并填写模型 ID。` guidance. Cancelling retains both saved models.
  The UI value operation initially reports an observation timeout; a fresh binding
  proves it applied, so the operation is not repeated and the App is not restarted.
- `latest-initial.evidence.json`, `latest-browser-panel.evidence.json`,
  `latest-invalid-model-save.evidence.json` and `before-fix-closed.evidence.json`
  retain task A cancelled/task B completed, 15 successful audits, three verified
  artifacts and no task-owned Chrome/Node process. The synthetic database has no
  scheduled tasks or runs. `permission.json` confirms an unelevated App token.
- The before QA instance exits through its actual close button and restores
  `Theme=System` through the real appearance page. It is not killed or replaced
  because an observation timed out.

## Repair verification: 2026-10-13

The shared template names its existing body `PART_DialogBody`. Opening a new
dialog session synchronously resets that body's offset before visibility and
autofocus, without scheduling a later reset that could override an explicitly
chosen field. Individual settings pages need no reset handlers. Returning to an
already-open page and nested scroll positions retain their existing behavior.

- The eleven new cases and existing dialog/focus cases pass 34/34. The wider
  settings, provider/model, editor and shared visual-control group passes 779/779;
  architecture constraints pass 7/7. The complete serial App suite passes
  4,043/4,043 in 10 minutes 2 seconds using the retained serial settings. None are
  skipped. Results are in `dialog-scroll-focused.trx`, `dialog-scroll-wider.trx`,
  `dialog-scroll-architecture.trx` and `dialog-scroll-app-serial.trx`.
- The solution builds with zero warnings and errors; scoped whitespace
  verification and repository `git diff --check` pass. The complete visual test
  assembly passes 12/12, with no skipped cases. Source review verifies method
  limits, the architecture test verifies one type per C# file and file limits,
  and the changed shared template parses as valid XML.
  Other functional test assemblies are built, not fully rerun in this repair.
- After the full App run, a final solution build again has zero warnings/errors
  and all eleven new cases pass in `dialog-scroll-final-replay.trx`. The final
  managed App hash equals the native replay's recorded hash. The staged set stays
  empty; no commit, push or publication is performed.
- The repaired production App DLL hash is
  `D60261459E5BE56095641242305D55A464DED6866AD359AD96FE9E7F99E3565C`.
  `runtime-files-after.json` records the modules used for the native replay;
  `permission-after.json` confirms its ordinary, unelevated token.
- At 980x640 and 96 DPI, both light and dark model A editors pass the actual
  open/scroll/cancel/reopen sequence. The first `模型 ID` label is fully visible,
  the first input shows its focus outline, and the saved model values remain
  intact. Footer actions remain accessible while the advanced JSON field scrolls.
  Native Escape also closes the editor and returns to the provider list.
- The dark sequence is repeated for model B, matching the model in the original
  failure screenshot. Compare `dark-narrow-model-editor-reopened-before.png`
  with `dark-narrow-model-b-reopened-after.png`; the retained offset no longer
  clips its first label. The light/dark model A captures use the
  `*-narrow-model-editor-after.png`, `*-scrolled-after.png` and
  `*-reopened-after.png` filenames in the same external evidence directory.
- The test instance restores System theme through the real appearance page and
  exits through its actual window close button. Before/after closed snapshots
  contain identical tasks, all 15 successful audits and all three verified
  artifact records. Live snapshots show no task-owned Node or Chrome process.
  No message, task continuation, approval-policy change or tool replay occurs;
  no saved provider/model changes are made during the repaired native replay.
  Read-only catalog counts remain one provider, two models, two tasks and zero
  schedules/runs; the synthetic `ui-settings.json` persists `Theme=System`.

This native sweep is partial. It does not establish a complete current-version
theme/state matrix, same-state Codex or PI-Desktop pixel equivalence, every visible
control's behavior, all protocols or completion of the full product objective.
