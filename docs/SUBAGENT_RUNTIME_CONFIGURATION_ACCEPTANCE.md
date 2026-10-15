# Subagent runtime configuration acceptance

## Design self-check

1. Agent owns child configuration resolution through its existing repository
   ports. App settings and shared model capabilities already validate new drafts,
   but a later provider/model edit can invalidate a previously saved child effort.
   The execution boundary must revalidate the actual saved target, without silently
   choosing another provider, model or reasoning effort.
   App owns audit presentation from existing audit records, with exact localized
   error-code categories rather than raw errors or permission-escalation advice.
2. Reuse `IConfigurationRepository`, `ISubagentConfigurationRepository` and
   `ModelReasoningPolicy`; no new execution path, concrete infrastructure reference
   or UI dependency. Match both model identity and provider ownership.
3. Run through the existing schema/capability/approval/timeout/cancellation/output/
   audit pipeline. Reject invalid configuration before a child request or progress
   event. Localize the actionable rejection; never echo keys or exception text.
4. Keep one handwritten type per file and existing file/method hard limits. Do not
   alter JIT, Windows x64 packaging, JSON, persistence schema or tool permissions.
5. Add failing focused pipeline tests for foreign model ownership, unsupported
   saved effort and supported choices/legacy `off`. Exercise actual settings saves,
   SQLite, local adapters, failure audit/presentation/restore and explicit repair.
   Existing successful native child lifecycle evidence is not recreated or claimed
   as fresh negative-case acceptance. Supplement this with actual settings edits,
   native rejection/restore screenshots and audit hint allowlist/layout regressions.

## Current scope

The preceding model-status continuation is verified progress, not full-product
completion. Current source and acceptance records show that the saved child
configuration, four protocols, live dialogue, sharing toggle, Stop and same-process
recovery already exist. This slice hardens configuration changes between editing
and execution, rather than creating another child runner.

## Failing evidence and correction

- The first pipeline run has 7 failures and 6 passes. Unsupported efforts reach
  the stub model, and a matching row identity with foreign provider ownership is
  incorrectly accepted. The foreign-row case is a negative port-contract probe,
  not a claim that the production SQLite query returns foreign rows.
- The real App/settings/SQLite baseline has 4 failures and 1 pass. Editing a model
  family or provider protocol leaves the saved child effort intact, but execution
  reports the generic `subagent_model_failed` instead of naming the incompatible
  configuration. Gemini's adapter already prevents a network request in these
  baseline cases; this is not reported as an existing remote-request leak.
- `SubagentTool` now matches both row and provider identity, and checks the current
  shared capability policy before constructing a child run. Supported settings and
  legacy `off` retain their current request semantics. Unsupported settings return
  `subagent_reasoning_unavailable` with an actionable localized explanation, no
  child request/progress, and no fallback or rewrite of the user's configuration.

## Verification

- Focused Agent checks initially passed 68/68; the subsequent catalog/gateway
  optimization expanded this to 84/84. The final Agent assembly passed 112/112.
- The App `Subagent|ConfigurationMutation|ModelReasoning` run passed 152/152,
  covering actual settings, SQLite, local protocol adapters, explicit repair,
  failure audits/cards, saved-effort retention and restore without replay.
  The final-build full App assembly subsequently passed 3,739/3,739.
- The signed Gemini child fixture now uses the valid `gemini-3-flash-preview`
  model identity, retaining its signed-continuation and parent/child isolation
  assertions. Restored configuration is read from the repository port rather
  than constructing a fresh UI-bound MainWindowViewModel off its dispatcher.
- Scoped whitespace checks, Roslyn type/size checks and the final solution build
  passed with no warnings/errors. No persistence schema or packaging changes.
- Native Gemini model-family invalidation and rendering are now verified below.
  Provider-protocol invalidation retains deterministic coverage rather than native
  negative-case coverage. Gemini model-family repair now also has the native
  success/restoration evidence below.
  Full-product and pixel-parity acceptance remain ongoing.

## Native negative-case acceptance (2026-10-12)

The previous goal turn changed code and produced completed tests, so it was
progress. This continuation closes the missing native rejection evidence, then
fixes an actually observed audit-presentation gap rather than adding another
child executor.

- Rebuilt the existing external `GeminiParentQa` host against current App code;
  build warnings/errors were zero. Both native starts reported `QA_ELEVATED=False`.
  Only a new synthetic TEMP database/project and loopback protocol servers were
  used. No real provider credentials, paid requests or production task data.
- In the real child settings page, explicitly selected and saved `high` for
  `gemini-3-pro-preview`. In the real expanded provider/model editor, changed that
  same non-default model identity to `gemini-2.0-flash` and explicitly set the
  model's own effort to `none`. The default parent remained
  `gemini-3-flash-preview` / `medium`; the saved child remained `high`, with sharing
  disabled and no automatic fallback.
- The settings page preserved the now-incompatible child effort and displayed
  its existing explicit-choice guidance. A conversation send produced a visible
  failure row with the new actionable reasoning explanation. Normal close logged
  `QA_REQUEST_COUNTS=2,0`. Read-only whitelist inspection of the synthetic SQLite
  database confirmed one approved, completed, failed `subagent.run` audit with
  `subagent_reasoning_unavailable`, a persisted matching failure payload, no child
  dialogue and unchanged authorized evidence file. The parent's synthetic summary
  is not evidence of successful child execution.
- Native inspection also found that the right execution-record card still mapped
  that error to generic permission/configuration advice. `AuditExecutionViewModel`
  now maps the exact known code to localized child-settings guidance. Unknown or
  near-matching codes remain sanitized; success/pending/denied precedence stays
  unchanged. No raw error text, permission changes, new UI controls or local styles.
  The failing presentation baseline was 1 failure / 17 passes; five presentation
  regressions and two actual shared-row wrapping/bounds cases cover the correction.
- Reopened the same saved database normally with the repaired App, without sending
  anything. Deep-dark failure history and the corrected audit hint were visible.
  The restore host initially had an effective 1280x820 window despite requesting
  980x640; only a native border drag produced the measured 980x640 capture. At that
  size the audit hint wraps completely within the existing shared card. Normal
  close logged `QA_REQUEST_COUNTS=0,0`; the read-only verifier confirmed the same
  five messages and unchanged message/audit hashes, preserved child `high`, and
  no replay or history mutation.

External evidence root (no files from it are added to the repository):
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-reasoning-negative-native-20261012-101831`.
Screenshots use the user's existing Python helper in `--passive` mode with the
exact native PID; input and resizing use Sky. Its cursor glow is not App shadow.

- `01-valid-high.png`, `02-model-family-changed.png`, `03-invalid-effort-preserved.png`:
  actual saved settings and retained invalid child choice.
- `04-runtime-rejection-light-wide.png`, `05-audit-before-specific-guidance.png`:
  1920x1032 light native rejection and the observed generic audit defect.
- `06-restored-dark-wide.png`, `07-restored-dark-narrow.png`,
  `08-restored-dark-narrow-specific-audit.png`: actual 1280x820 and 980x640 dark
  restoration and corrected audit guidance. The compact conversation summary
  retains its existing ellipsis; the audit card shows the complete instruction.
- `metadata-negative-before.json`, `metadata-negative-restored.json`, native
  stdout/stderr logs: bounded whitelist evidence, normal exits and zero restore
  requests. Screenshot scripts, verifier and synthetic data remain outside Git.

This is native acceptance of one concrete failure/recovery scenario at 100%
scale, not proof of all protocols, all DPI settings or full Codex visual parity.

The final post-audit-change App assembly passed 3,746/3,746, with zero failures or
skips, in the completed `tests/audit-runtime-final.trx` under the external evidence
root. This includes the seven new presentation/shared-row cases; the preceding
94/94 focused run and zero-warning/error solution build cover the same production
change. No native protocol or DPI acceptance beyond the limits above is implied.

## Native explicit repair and restoration (2026-10-12)

The preceding automation projection turn changed source and completed regression
checks, so it was progress. This continuation closes an actual native acceptance
gap without adding another execution engine or changing production behavior.

- Created a separate synthetic fixture by SQLite read-only backup of the preserved
  failing history. The external host retargets loopback endpoints and the synthetic
  authorized project through production repository ports. A dedicated Gemini child
  profile/model identity isolates child requests from the Gemini parent; the invalid
  saved `high`, `gemini-2.0-flash` family and prior failed history remain intact before
  UI repair. This is not a same-provider-identity replay of the original fixture.
  The synthetic child's legacy display name still says Responses; actual protocol
  and request checks establish Gemini behavior, rather than relying on that label.
  Original fixture data, real user tasks, accounts and credentials are not modified.
- External setup initially used a nonexistent `BaseUrl` property, then attempted
  to change a model's provider ownership in place. Compilation and the existing
  repository guard rejected those harness mistakes before UI acceptance. Corrected
  only the external host: use `BaseUri` and create the separate child model through
  supported repository operations. Do not weaken production ownership validation.
- Normal-privilege startup reported `QA_ELEVATED=False`. In the actual settings
  page, observed the retained incompatible `high` and its guidance, explicitly
  selected `none` and clicked Save. Sharing remained disabled and the parent model
  remained `gemini-3-flash-preview` / `medium`. A single manual conversation send,
  not automatic replay, requested another authorized synthetic-file inspection.
- The actual Gemini child made two requests targeting `gemini-2.0-flash`, without
  `thinkingConfig`; the Gemini parent made two requests and retained medium. Child
  context excluded the parent marker, its file result reached the second child
  request, and a successful child result reached the parent's continuation. Native
  dialogue displayed the authorized read evidence, child reply and parent summary;
  the inspector displayed the old failure beside two new successful audits.
- Normal close logged `QA_REQUEST_COUNTS=2,2`. A bounded, fixed-root, read-only
  whitelist verifier confirmed the explicitly saved `none`, unchanged selected
  identities from the pre-UI snapshot, one preserved configuration-failure audit,
  new successful `subagent.run` and `filesystem.read_file` audits, persisted child
  dialogue and unchanged evidence bytes. The original five message keys/content
  hashes and original audit tuple remain unchanged; no failure is overwritten.
- Reopened the same repaired fixture without sending. Its actual initial window
  was 1280x820 despite the host requesting 980x640; a native border drag produced
  the measured 980x640 capture. Verified recovered dialogue and the actual collapse
  action after scrolling the completed header into view. An earlier click on a
  clipped header did not collapse it and is not counted as successful interaction.
  Normal close logged `QA_REQUEST_COUNTS=0,0`; message and audit hashes match the
  completed repair snapshot, `none` remains saved and no model/tool replay occurs.

External evidence root:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-reasoning-repair-native-20261012-105830`.
Python captures use only the exact PID in passive mode; Sky owns all app input.

- `01-invalid-high-before-repair.png`, `02-none-explicitly-saved.png`: actual settings.
- `03-native-repair-success-dialogue.png`, `04-prior-failure-and-new-success-audits.png`:
  1280x820 dark successful workflow and retained error guidance.
- `05-restored-dark-narrow-expanded-before-visible-action.png`: the uncollapsed
  observation, not proof of the earlier attempted action.
- `06-restored-dark-narrow-actual-collapsed.png`: measured 980x640, old failure and
  new completed result after the actual collapse.
- `metadata-native-repair.json`, `metadata-repair-before.json`,
  `metadata-repair-after.json`, `metadata-repair-restored.json`, normal-close stdout
  and empty stderr: whitelist evidence, identity/history retention and request counts.
  Harness, verifier, captures and synthetic SQLite data remain outside Git.

This adds native Gemini effort-repair acceptance at 100% scale, not all-provider
negative/repair coverage, real remote-model compliance, all themes/DPI or Codex
pixel parity. The complete product goal remains active.

After both native hosts exited, the solution build again passed with zero
warnings/errors and the runtime-configuration/audit-presentation/shared-row focused
run passed 29/29 in `tests/native-repair-focused.trx`. Scoped whitespace checks
passed; no paths were staged, committed or published. This slice changes only this
repository acceptance record and external synthetic QA assets, not production
source or tests. The preceding goal turn's completed App full run was 3,757/3,757;
it is not represented as a new full-suite command in this native-only slice.
