# Shared selector keyboard-focus appearance

## Reproduced defect and correction (2026-10-16)

Headless rendering of the actual production styles confirms square black/white
focus frames inside ordinary and compact dropdown menus. The selected row already
has six-DIP corners, so the native rectangular focus adorner breaks its shape.
Pill selectors already own a rounded background highlight and must retain it.

The UI/UX skill's cross-platform visible-focus guidance and Avalonia styling
guidance inform this correction. Web-only cursor rules and mobile target sizes
are not imposed on this Windows desktop UI. Existing Codex-oriented geometry,
palette and interaction density remain unchanged; no new design system is added.

`Styles/ComboBoxVariants.axaml` assigns a shared native-based item-container theme
to `AppComboBox`. Its noninteractive `AppBorder` focus adorner uses the existing
`BorderFocusBrush`, a two-DIP stroke and the adorned element's current radius.
It changes no dimensions, padding, content position or command behavior. Theme
and radius changes update the existing adorner without rebuilding the row.

Crucially, this is an item-container **theme default**, not a global visual style
that overrides explicit component ownership. Existing pill-selector themes and
local focus owners can still opt out. The pill selector's production file is
unchanged, and its original background highlight remains intact.

## Ownership and safety self-check

- App owns appearance through its existing shared styles and native Avalonia
  focus lifecycle. No domain, provider, persistence or dependency direction changes.
- No new service, global state, tool, file operation, network call, approval policy
  or asynchronous lifetime is introduced in the application.
- Production changes are twelve AXAML lines in one shared style file. The two
  new handwritten C# test files contain one type each, 90/174 physical lines,
  with maximum method sizes of 26/45 lines. No new application C# is needed.
- Pixel and routed-input checks cover the successful paths and the boundaries:
  explicit opt-out, existing pill appearance, disabled input, Escape without
  selection, hit-tested pointer selection and exact selection-change counts.

## Final verification

- Final 24-case regression baseline uses an identity-verified original App DLL:
  all twelve ordinary/compact rounded-corner cases fail at the corner pixels;
  four theme/input cases fail at the absent shared adorner. The eight existing
  pill/local-owner preservation cases already pass. Baseline: 8/24, not all green.
- Final complete pixel assembly: **260/260**, including all 24 new cases, across
  Light/Dark and 100%/125%/200% render scaling where specified.
- Related App tests: **198/198**, matching independent discovery. This includes
  existing theme choices, selector native behavior, settings editors and connected
  provider/automation workflows. Architecture constraints: **7/7**.
- The three final disjoint scopes total **465** passing cases, with matching
  individual TRX rows/unique IDs and no failures, skipped or aborted cases. Neither
  baseline failures nor preliminary candidate runs are added to this total.
- Solution build: zero warnings/errors. Scoped formatting and source-size checks
  pass. Final post-test build and assembly-identity checks are recorded alongside
  the raw results; generated evidence and probe scripts stay outside the repository.

## Preserved failed attempts

The first candidate applies the adorner with a global `ComboBoxItem` style. Its
pixel tests pass, but two existing `ThemePaletteChoiceInteractionTests` fail
because the style overrides the pill theme's explicit null adorner. Those old
assertions are not weakened or changed. Moving the default into the native-based
item-container theme repairs that regression. The final new tests explicitly
preserve both pill and local ownership; all original related tests pass.

The first baseline fixture also tried changing the navigation method while
refocusing the same already-focused item. That did not remove its old focus
state. The final fixture first moves focus to another item and then back; the
identity-verified final baseline above is the acceptance baseline. The initial
formatting check reports one initializer line break; the corrected check passes.

## Evidence and limits

Evidence: `%TEMP%\MossAgent-dropdown-review-20261016`.

- `selector-baseline-final.trx`, `baseline-final/`: final unchanged-style baseline.
- `pixels-final.trx`, `final/`: complete final pixel results and raw rendered frames.
- `app-related-final.trx`, `architecture-final.trx`, `final-test-summary.json`:
  final related results; `app-related-discovery.txt` verifies the 198-case scope.
- `app-related.trx`: the preserved two-failure first candidate. Earlier baseline,
  candidate and pixel reports remain separate, not successful final evidence.
- `before-after.png`: labeled, enlarged off-screen Light/Dark comparison.
- `format-final.log`, `source-audit.json`, build logs and hash CSVs: hygiene,
  compilation and tested-assembly identity evidence.

The separately completed 4,794-case App run starts before this correction and
retains the earlier App DLL throughout; it is not a full run of this new UI
revision. Its allocation investigation is recorded in
`SIDEBAR_REFRESH_OPTIMIZATION_ACCEPTANCE.md`. No causal repair of those historical
intermittent allocation failures is claimed.

Existing desktop windows, user databases and Codex are not operated, captured or
restarted. These synthetic Headless checks do not replace the user-postponed
native captures, same-state Codex comparison, native DPI transitions or complete
product acceptance. No staging, commit, push or publish is performed. Windows x64,
JIT, single-file publishing and existing JSON behavior remain unchanged.
