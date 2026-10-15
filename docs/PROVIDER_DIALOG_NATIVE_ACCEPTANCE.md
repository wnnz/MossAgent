# Provider and model dialog native acceptance

## Plan and design self-check

- The preceding TLS turn made verified progress; the complete product objective
  remains active. This continuation returns to native provider/model settings.
- Use production App/MainWindow, composition, shared controls/styles, existing
  settings commands and SQLite ports. Repository-external QA code only seeds
  isolated synthetic data and exposes a bounded loopback discovery/text service.
- Start with fresh screenshots and real expansion/editing/focus input. Check
  wide/narrow layout, long labels, fixed dialog footers, theme switching, saving,
  discover/manual model identity and restoration, without deleting configurations.
- Ordinary privilege, a private temporary data/project root and synthetic keys
  only; no real account, paid request, user configuration, approval change,
  authentication/security UI or system setting is touched. GUI input uses Sky;
  the external Python helper only captures passive pixels for comparison.
- Any observed product defect needs a failing focused regression and a fix in
  App's owning shared control/style or settings model. Preserve dependency,
  lifetime/cancellation, localization, one-type and file/method constraints.
- No production change is assumed before fresh runtime evidence. Run the relevant
  tests and solution build after a correction, and report native coverage exactly.

## Evidence

Repository-external root: `%TEMP%\MossAgent-provider-dialog-20261012`.
Before/after passive Python screenshots, bounded observations, original failing
regressions, successful checks and normal-shutdown logs are retained there.

## Confirmed native defect and correction design

- The preceding catalog cleanup turn made verified progress (App 3,694/3,694,
  solution build). This continuation revalidated the existing ordinary-privilege
  980x700 host, PID 17696, instead of restarting it for an observation timeout.
- Actual input opens the existing model editor, selects Appearance, then returns
  to AI providers. The dialog and draft remain open, but focus stays on the
  navigation button. A real Escape key leaves the model editor open. Independent
  UIA focus and the bounded in-process observation agree after the transition.
- The correction belongs to App's shared `AppSettingsDialog`: observe effective
  ancestor visibility, restore an available dialog-owned focus target, and reject
  queued transfers after hiding, closing or detaching. Preserve page navigation
  and drafts rather than changing the modal's scope or disabling the sidebar.
- No new I/O, port, tool, approval, timeout or persistence behavior is needed.
  Native confirmation uses only synthetic configuration. Focus regressions cover
  light/dark, last-field selection, unavailable children, reattachment, late
  callbacks and Escape; code must retain one type per file and size limits.

## Implemented correction

- The shared dialog observes visibility/enabled changes on its attached visual
  ancestors and itself. Those subscriptions are released on detachment; no global
  focus hook or configuration cache is introduced.
- Returning to a retained page restores the last available dialog-owned field,
  otherwise an available input/action. A newer focus already inside the dialog
  is not overridden. Hiding, closing and detaching invalidate deferred transfers.
- TextBox's configured clear/retain-selection behavior stays unchanged. The final
  regression covers both policies instead of requiring every input to retain its
  selection after losing focus. Templates, dimensions, footer layout and colors
  are unchanged.

## Verified results

- The pre-correction focus baseline has 8 failures and 5 passes, including the
  native page-return failure. Two additional selection-policy cases bring the
  final focus cases to 15. Focus/dialog/picker/page-switch/draft/edit-menu regressions
  pass 68/68; App full suite passes 3,709/3,709.
- Architecture constraints pass 7/7. Solution and native-host builds have zero
  warnings/errors. Scoped whitespace and diff checks pass. Roslyn audit reports
  one type per file, 169/161/81 file lines, and maximum method sizes 26/23/21 for
  the shared control, focus tests and fixture respectively.
- The old host closes normally with exit 0 and request counts 0/0. The rebuilt
  ordinary-privilege host, PID 17424, runs the production window/composition and
  independent synthetic SQLite root. It starts at 980x700 DIP, scale 1.
- In light/narrow mode, real navigation away and back restores model-ID focus;
  a real Escape closes the editor and restores the edit-row trigger. In
  dark/narrow mode, an edited display-name draft survives navigation and focus
  returns to that last field. Passive screenshots and bounded observations prove
  both outcomes.
- Real title-bar maximization checks dark/wide 1920x1032 DIP, scale 1, with the
  existing fixed dialog footer visible. Saving the synthetic display name updates
  the current model row. The native discovery action calls the bounded loopback
  service, adds `discovered-local-extra`, and preserves that edited model.
- Real manual addition creates `gemini-2.5-flash` only under the secondary Gemini
  provider. Reopening restores its ID, fallback display name, 128000 context,
  8192 output and medium effort. Real Tab reaches the next field; Escape closes
  that editor. No Gemini request is sent to the Responses-only QA service.
- The UIA set-value helper times out once; initial observation still shows the
  old text, and a later refreshed screenshot shows the intended new value. No
  second set-value call is made. Page/role trees can lag rendered transitions;
  subsequent inputs use independently refreshed text or current screenshot IDs.
- The rebuilt host closes normally with exit 0 and request counts 1/0 (discovery/
  response). No configuration is deleted, no real secret is entered, and no
  approval/security/privacy setting is changed. All GUI input uses Sky; Python
  performs passive captures only. No commit, push or publication is performed.

## Remaining scope

- This proves the model editor's page-return behavior, not every settings dialog
  or full-product visual parity. Native provider/proxy/MCP editor transitions,
  additional DPI scales and every theme/window combination remain unverified.
- Caption-button focus behavior is outside the page-return correction. At that
  checkpoint, the manual editor displayed the previous discovery status until
  saving; the status-ownership follow-up below corrects that separate issue.
- Real external-provider streaming, full-product workflows and a fresh Codex/PI
  comparison are not claimed here. The complete product objective stays active.

## Model status ownership follow-up design

- The confirmed stale footer is caused by the page and dialog sharing one status.
  Regression additionally reproduces a pending discovery or another row's delete
  replacing the current editor's validation error. The original focused baseline
  has 20 failures and 4 passes across light/dark production-pane bindings.
- App owns the correction: keep page operation results available while the model
  footer binds only to its draft's validation/save error. Editor entry/reset/close
  clears that draft status; retained-page navigation does not. Existing revision
  checks continue rejecting late save/delete errors from an older draft.
- Extract discovery orchestration/error mapping from the nearly 500-line settings
  model. Reuse `IModelDiscoveryService`, configuration ports and the catalog's
  mutation/import pipeline, without new I/O, global state or persistence changes.
- Preserve provider ownership, settings lifetime cancellation and the 30-second
  default discovery deadline. Check cancellation after awaited ports and never
  expose exception text, API keys or proxy credentials. This changes no tool,
  authorization, approval or output policy; synthetic native QA needs no secrets.
- Each handwritten file retains one type, file/method hard limits and shared
  visual variants. Focused regressions cover fresh/reopened editors, pending
  discovery success/failure, unrelated deletion, late writes and actionable retry;
  solution build and native screenshots follow before handing off this slice.

## Model status follow-up verification

- The 24 new production-pane cases all pass; the initial combined focused suite
  passes 102/102. The initial App full suite passes 3,733/3,733. The extracted
  operation retains the provider/import ownership and existing lifetime guards;
  success also restores the page's non-error appearance without clearing the
  current editor's validation error.
- A further disposal regression catches direct command execution reading an
  already disposed token source. Guarding the dispatch before obtaining that
  token prevents discovery, persistence and late status publication; command
  availability alone is not treated as a safety boundary.
- Final verification after that guard passes the combined focused suite 103/103
  and App full suite 3,734/3,734 (`app-final.trx` in the external evidence root).
  The solution build again has zero warnings/errors; whitespace and size audits
  also pass. The final safety test file is 117 lines, one type, max method 65.
- Both solution and external native-host builds pass with zero warnings/errors;
  architecture constraints pass 7/7, scoped whitespace and diff checks pass.
  Roslyn audit reports one type per file: model settings 462 lines/max method 40,
  discovery operation 78/28, new visual regressions 169/28 and fixture 69/16.
- Native evidence lives in `%TEMP%\MossAgent-model-status-20261012-092036`.
  The new synthetic production-composition host runs without elevation at
  980x700 DIP, scale 1, and never accesses real user configuration or secrets.
  A real discovery action imports the extra model only for the requested primary
  provider. Opening the secondary provider's new editor keeps that page success
  visible behind the dialog but leaves its own status empty.
- Real blank-ID save shows the current localized error in the fixed footer.
  Navigating to Appearance, switching to dark, and returning retains that error
  and draft. Typing `gemini-2.5-flash` and retrying saves only under the secondary
  provider; reopening restores its parameters with no previous save-success
  footer. Maximizing to 1920x1032 DIP preserves that clean footer and layout.
- Passive Python screenshots are `light-new-after-discovery.png`,
  `light-validation-error.png`, `dark-error-after-page-return.png`,
  `dark-reopened-after-save.png` and `dark-wide-reopened.png`; bounded observations
  separately confirm the page/editor status. Comparing the preceding native
  `dark-manual-reopened.png` confirms its old "model added" footer is gone.
  `status-footer-comparison.png` retains the two passive native footer crops.
- The host exits normally with code 0 and request counts 1/0 (discovery/response).
  All GUI input uses Sky; Python only captures pixels. The initial UIA click has
  unavailable geometry, so the window is freshly rebound before a screenshot-
  backed retry. Stale UIA trees are not used to repeat completed actions.
- Pending discovery/delete and late-save isolation are deterministic headless
  regressions, not claimed as native concurrent-operation coverage. Additional
  DPI scales, real external streaming and full-product visual parity are still
  outside this slice. No commit, push or publish; the complete goal stays active.
