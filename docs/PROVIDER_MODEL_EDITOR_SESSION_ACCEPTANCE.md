# Provider and model editor sessions

## Native defect and design self-check

- Fresh ordinary-permission production App input on 2026-10-13 confirms that
  Escape/cancel followed by editing the same provider or model retains discarded
  fields. The provider also retains its invalid-URL validation footer. The saved
  SQLite configuration remains unchanged; this is an editor-session defect.
- App's provider/model view models own the correction. Route selected-item and
  row edit commands through their existing catalog-validated entry methods.
  Reload saved fields when starting a fresh session on an equal selected item;
  preserve a still-open draft and ordinary page navigation.
- Reuse current catalog identity/ownership checks, shared dialogs and revision
  guards. No new port, I/O, dependency, persistence schema, secret logging,
  authorization, approval, timeout, cancellation or output behavior is introduced.
- Each handwritten C# file has one type, remains below 500 lines and keeps methods
  below 80 lines. Focused tests cover cancel/reopen parameters and validation,
  already-open draft preservation, unavailable/disposed targets and zero writes.
- Verify the failing baseline, focused/wider regressions and solution build, then
  replay real provider/model cancellation with passive Python screenshots.

## Evidence ownership

Repository-external evidence stays at
`%TEMP%\MossAgent-provider-draft-20261013`. The original production App assembly is
`CD5FD637D11AADEF8A0379E56E3234B7B044A1E7E0CF66BBC8E50736B8642BC8`.
The independent fixture is a SQLite backup of a normally closed synthetic
fixture, not user configuration. All input uses Sky; Python only captures passive
pixels, prepares synthetic data and performs read-only bounded audits.

`provider-reopened-after-cancel-before.png` and
`model-reopened-after-cancel-before.png` show both retained discarded drafts.
`provider-return-draft.png` independently shows successful page-return draft/focus
preservation. Invalid save leaves all 19 table schemas and rows, three artifacts,
request history and light UI settings unchanged. No model request or deletion is
performed. One model set-value observation times out; fresh window selection
confirms the intended value, so input is not repeated or claimed unsuccessful.

## Verification

The shared entry points now reload saved fields only when opening a fresh session
on the same selected catalog item. An already-open editor retains its draft.
Selected-item commands reuse the row entry points' availability and disposal
checks instead of opening an unchecked session.

- Baseline `session-baseline.trx`: eight failures and four passing preservation
  cases reproduce both cancellation defects and the unchecked selected entry.
- Repaired `session-fixed.trx`: all 12 cases pass. `session-wider.trx` passes
  311 related provider/model/dialog/configuration/page-switch cases.
- `session-architecture.trx`: seven architecture constraints pass;
  `session-pixels.trx`: all 48 App.Visual.Tests pass.
- The solution build has zero warnings/errors; scoped whitespace verification
  and source diff checks pass. This slice does not re-run the complete App or
  eight-assembly suite and does not reuse the earlier 5,804-case result as fresh
  validation of this binary.

TRX files are external under the evidence root's `tests` directory. The repaired
Debug/JIT App is isolated under
`.scratch/settings-editor-session-20261013/bin/MossAgent.App/debug_win-x64`;
its assembly SHA256 is
`04DE522716B323CCBCEB3BB8B7BA0907014EC53F1C5C2B89EA8736DE14CD141F`, unchanged after
the tests, solution build and native replay. Default output is not updated.

## Repaired native replay

Independent `after` data and ordinary-permission production-App input confirm:

- Light/maximized provider editing: invalid save shows the actionable URL
  validation footer without a stored-row change. Navigating to Appearance,
  switching dark and returning retains the unsaved fields, validation and
  save-button focus.
- Dark/maximized Escape/reopen of the same provider restores the saved name and
  loopback endpoint and replaces the stale error with the ordinary empty-key
  hint. Returning to light keeps those restored values.
- Light/maximized model editing: page return retains the unsaved display name
  and input focus. Cancel/reopen restores the saved display name, model ID,
  context length, output limit and reasoning effort.
- Restore to 1280x820 at 96 DPI keeps the model dialog and fixed footer visible
  without black padding. Escape closes the editor and returns focus to its edit
  entry; the actual caption close exits the App and observer normally with code
  zero. Fresh enumeration and process/port checks confirm cleanup. The immediate
  post-close window enumeration raced shutdown; it is not counted as exit proof.

`after/native-observations.json` records the observed checks.
`after/final-closed.audit.json` passes SQLite integrity and exact comparison of
all 19 table schemas/rows, all three original artifact hashes, request history
and restored light UI settings. No model/discovery request, deletion or real-user
configuration write occurs. One model set-value observation times out; a fresh
window observation confirms the value without repeating input. Immediate UIA
trees can lag dismissal, and the provider validation footer is checked in pixels
rather than inferred from its absence in the tree.

The provider/model reopening frames and `model-light-normal-clean.png` have
passive Python metadata. `native-session-comparison.png` places equal-size light
1920x1032, 96-DPI native before/after crops side by side without rescaling. Its
canvas height follows crop dimensions to avoid generated trailing black padding.
This compares editor-session behavior, not Codex appearance.

## Remaining scope

The fresh passive `codex-reference-final-pass.png` is still the conversation page,
not Appearance, and is not used as an equivalent settings reference. Codex is
not automatically navigated. Full native Appearance/PI-Desktop comparisons,
high-DPI and complete interaction/state coverage remain open. This healthy
resize replay does not repair the separately reproduced native clipping defect.
No shared dialog appearance, persistence schema, packaging, AOT, JSON or approval
behavior changes. No commit, push or release occurs. The full goal remains active.
