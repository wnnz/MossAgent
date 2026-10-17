# Product interaction audit and complete regression checkpoint

## Scope (2026-10-16)

This follow-up validates the existing uncommitted UI revision, including shared
tool-output keyboard focus and subagent manual-approval coverage. It does not
introduce another production change or replace native/Codex visual acceptance.

The UI/UX skill's cross-platform focus-state guidance informs the review: keep
visible keyboard feedback, usable escape routes and native disabled semantics.
Web-only WCAG examples and mobile target sizes are not asserted as desktop
implementation requirements or compliance results.

## Complete regression results

All eight existing test assemblies finish normally with every result Passed:

| Assembly | Passed / total |
| --- | --- |
| App | 4,726 / 4,726 |
| App.Visual | 188 / 188 |
| Agent | 123 / 123 |
| Infrastructure | 204 / 204 |
| Providers | 542 / 542 |
| Tools | 494 / 494 |
| Browser | 76 / 76 |
| Platform.Windows | 142 / 142 |
| Total | 6,495 / 6,495 |

- Each TRX reports zero failures, skipped/not-executed or aborted cases; its
  individual result count matches its total. Related subset runs are not added.
- The complete App run includes all eight manual-approval flow cases, eight
  approval boundary cases and four tool-output keyboard cases. Complete pixels
  include all eight new tool-output focus-rendering cases.
- All test processes finish before the final solution build. The build succeeds
  with zero warnings/errors using `dotnet build MossAgent.slnx --no-restore
  -v:minimal -m:1 /nodeReuse:false`.
- SHA-256 identities of all eight tested assemblies stay unchanged across the
  tests and build. Production, App.Tests, App.Visual.Tests and the external
  driver's four App DLL copies match and remain unchanged after the build.
- The earlier 6,467-case complete checkpoint remains historical evidence. Its
  earlier allocation/cleanup failures do not recur here; this is not proof of
  their original causes or a new causal fix.

## Static and rendered entry review

The external driver uses the production MainWindow and composition through an
existing test fixture, isolated synthetic storage and a local model server. It
references existing output DLLs rather than rebuilding the repository. No user
database, credentials, existing window or desktop capture is accessed.

- A markup inventory finds 258 selected button declarations: 233 command, three
  Click event, 21 flyout and one toggle entry. This is syntax, not behavior proof.
- The final Headless review records 4,038 wrapper-button occurrences across 160
  scanned page/state combinations, including 40 new-editor combinations. These
  are repeated appearances, not 4,038 distinct controls or xUnit tests.
- Combinations cover project/plain-chat scope, requested Light/Dark themes and
  526x601/1280x820 geometry. Pages include conversation, six inspector states,
  automations, seven settings pages and five kinds of new-item editor.
- No enabled button is unresolved by the driver's wiring checks. Those checks
  recognize commands, flyouts, toggles and known event-backed controls; they do
  not invoke most commands. Command-disabled controls are not visually enabled.
  There are 376 disabled occurrences, not 376 distinct unavailable features.
- Twenty-eight search paths exercise Space opening and routed Escape dismissal
  from the search input. All forty new-editor paths use Space to open and Escape
  to dismiss provider, proxy, MCP, subagent or custom-agent dialogs.
- Sixteen native numeric increment/decrement paths use actual pointer hit-tests
  and down/up input, verify the value change, then restore the draft value.
- Navigation retains the original composer draft and project selection. The
  fixture's model server records zero requests throughout these UI-only paths.

## Evidence and interpretation

Evidence is outside the repository at
`%TEMP%/MossAgent-product-audit-20261016`. The directory label is not an execution
timestamp; the checks span the local 2026-10-15/16 midnight boundary.

- `app.trx` and the other seven assembly-named TRX files are complete-run results;
  `complete-suite-summary.json` records their independently parsed counters.
- `markup-actions.json` is the static declaration inventory.
- `rendered-actions-editors-verified.json` is the final driver result;
  `interaction-summary.json` records its occurrence/path counts.
- `driver/` holds the external audit source and output, not repository code.
- `hashes-before.csv`, `hashes-after-tests.csv`, `hashes-after-build.csv` and
  `app-copy-hashes-after-*.csv` record tested/build identity checks;
  `solution-build.log` records the final build.
- Earlier `rendered-actions*.json` files retain audit-development observations.
  SearchTrigger opens through a component Click handler after taking ownership
  of its flyout; native numeric spinner buttons use event wiring, not commands.
  These initial wiring warnings are not confirmed product defects. Snapshotting
  the visual collection fixes the driver's enumeration failure, not production UI.

## Remaining boundaries

- Most occurrences receive binding/enabled checks only. Visibility flags do not
  establish unobscured pixels or actual pointer hit areas; only the explicit
  numeric paths above receive pointer hit-test verification in this audit.
- The audit does not execute save/delete, model discovery, tools, terminal or
  browser launch, every menu, arbitrary user data, or all loading/error states.
- Requested theme settings do not prove the complete preference/theme workflow
  or pixel contrast. These are Headless checks, not native-window screenshots.
- Native screenshots and existing-window operations remain postponed by the
  user. Same-state Codex comparison, native DPI/resize diagnosis and whole-product
  interactive/visual sign-off remain open; no goal completion is claimed.

No production/test source, user settings, JIT/JSON behavior or Windows x64
single-file packaging is changed by this audit. Existing uncommitted work is
preserved; nothing is staged, committed, pushed or released.
