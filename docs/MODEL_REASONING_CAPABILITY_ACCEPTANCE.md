# Model reasoning capabilities and native acceptance

Date: 2026-10-08. This continues the Responses settings acceptance without
replacing `MODEL_CONFIGURATION_NATIVE_ACCEPTANCE.md`.

## Scope and design self-check

1. Application owns the immutable protocol/model effort policy. App owns editor
   and composer presentation; Providers owns request mapping and preflight.
   Dependency direction remains unchanged.
2. Existing configuration/model ports and provider streams are reused. No new
   tool, database schema or automatic migration changes existing settings.
3. Unsupported Gemini efforts fail before HTTP-client creation with a fixed safe
   code, classified into a localized task hint. Cancellation is preserved.
   Native acceptance uses synthetic credentials, an isolated SQLite directory,
   loopback SSE and read-only tasks; no tool approval is bypassed.
4. Shared semantic combo/slider wrappers own native selection synchronization.
   Per-editor option snapshots remain stable during keyboard selection, and
   detached slider handlers are removed. One type per handwritten C# file and
   file/method limits are audited; ModelSettingsViewModel is near its hard limit.
5. Focused tests cover unsupported/supported requests, no outbound HTTP on
   rejection, normalization, retained legacy settings, keyboard correction,
   dynamic range shrinkage, draft/send protection, detach/reattach, disabled input
   and light/dark endpoint geometry.

## Capability policy

- Gemini 3.8/3.7 Flash and 3.1 Pro: low/medium/high; Gemini 3 Pro: low/high.
  Gemini 3.1 Flash Lite Image: minimal/high. Other Gemini 3 identifiers retain
  the four-level compatible mapping; future families still need remote validation.
- Gemini 2.5 Pro keeps the existing six budget levels and cannot disable thinking;
  Flash additionally accepts none. Unrecognized older/custom Gemini aliases accept
  none only rather than silently omit an explicitly requested thinking parameter.
- Efforts are trimmed and normalized case-insensitively; off aliases none, not
  Gemini 3 minimal. Non-Gemini protocols retain the general eight-level policy;
  this does not establish every remote compatible model's capabilities.
- Newly discovered models get a supported default. Saved unsupported values are
  retained and shown with a warning; saving/sending requires explicit correction.
  A model switch never clamps a previous slider choice into the user's draft.
- Google references used for the family mapping:
  https://ai.google.dev/gemini-api/docs/thinking and
  https://ai.google.dev/gemini-api/docs/gemini-3.

## Native evidence

All helper scripts, QA hosts, databases, logs and images remain outside repository
source under `%TEMP%\MossAgent-protocol-configuration-20261008`.
Passive Python PrintWindow captures do not activate, resize or click either app.
Actual MossAgent input uses Computer Use, not a custom UI-automation helper.

- Initial request regression: 15 capability cases fail before the implementation.
  Native iteration also exposes blank legacy selection, a medium value silently
  replacing minimal, and an exception when shrinking options during selection.
  Shared semantic controls fix these causes rather than hide the editor warning.
- Light 1280x820, ordinary permissions, scale 1: saved minimal is visible,
  rejected on save, explicitly changed to high, saved through the production UI,
  then sent through the production Gemini provider and loopback SSE response.
  Captured whitelist: model gemini-3.8-flash, effort HIGH, output 4096,
  temperature 0.4, top_p 0.7.
- The same native session selects the compatible model's eighth slider position
  and sends a real loopback request: local-compatible, effort ultra, output 4096,
  temperature 0.4, top_p 0.7. The expected response appears in the conversation.
- Rebuilt final host, Dark 980x640, ordinary permissions, scale 1: the same data
  restores saved Gemini high, the edit/save footer is visible, previous responses
  restore, and the picker displays three aligned ticks and high selection.
- Files include `light/legacy-preserved-final.png`, `light/gemini-high-response.png`,
  `light/gemini-high-parameters.json`, `light/compatible-ultra-response.png`,
  `light/compatible-ultra-parameters.json` and `light/dark-restored-editor.png`.
  Main-window PrintWindow does not necessarily include separate popup HWNDs;
  popup assertions also rely on direct Computer Use observations.
- A separate checkpoint opens the dark automation empty page and new-plan editor,
  closes the unsaved form, and inspects the docked no-conversation empty state.
  No plan is saved or executed. Images are `light/dark-automation-editor-checkpoint.png`
  and `light/dark-inspector-empty-checkpoint.png`; they are baselines, not proof of
  same-state Codex fidelity. Both final native hosts close normally with exit 0.

## Verification and limits

- Final behavior build: solution zero warnings/errors; relevant App filter
  passes 117 tests. Providers full suite passes all 283 tests.
- Seven-project full run: 3,152 executed, 3,149 passed, three sidebar-heading
  cases fail during temporary-directory deletion with file-sharing violations,
  not layout assertions. All 12 sidebar-heading cases pass on isolated rerun.
  This is not a clean full-suite result; unrelated cleanup code is unchanged.
  TRX evidence is in `verified-results` beneath the evidence root.
- Scoped whitespace verification and 35-file AST/XML scale audit pass.
- No paid/public provider, authentication proxy, all future/custom model families,
  complete token-budget edge cases, all DPI/hover states or full Codex pixel
  equivalence is claimed. The current passive Codex capture shows conversation
  and file preview, not a matching automation/settings state.
- No staging, commit, push or publish. Debugging stays managed/JIT and the existing
  Windows x64 single-file release configuration remains unchanged. Overall product
  goal remains in progress.
