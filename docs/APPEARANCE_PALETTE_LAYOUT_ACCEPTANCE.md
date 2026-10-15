# Appearance palette layout acceptance

## Design self-check

1. App owns this appearance-only layout. Shared controls and style variants own
   rows, selectors and disclosure presentation; no module dependency changes.
2. Keep both existing palette/font editors and their real save/reset/selection
   commands. The basic card follows the effective theme through MainWindow's
   existing theme notification, without a new subscription or cache.
3. This page changes only appearance preferences. It does not change tool
   permissions, approvals, model requests, user databases, JIT/JSON or publishing.
4. New handwritten types have their own small files. Shared disclosure markup
   retains its content on collapse, rather than rebuilding editor state.
5. Rendered tests cover both themes and widths, default-collapsed content,
   keyboard disclosure, preserved drafts, actual theme routing and save errors.
   Existing settings focus, palette/font persistence and visual gates remain.

## Reference and scope

Use the Python passive 1920x1032, 96-DPI Codex Appearance reference recorded in
`APPEARANCE_MODE_PICKER_ACCEPTANCE.md`. Its basic card contains five rows below
the mode picker; advanced settings are initially collapsed. MossAgent currently
shows a Diff preview followed by both full palette editors, including extended
fonts. This change targets that hierarchy, not a new theme service or fake
preset/import/export controls. Codex's theme-preset actions and thumbnails, full
page positioning and high-DPI equivalence remain separate acceptance work.

## Correction and regression record

The page now has one five-row basic card bound to `ActivePalette`. Its computed
selection uses the existing effective-theme state and notification, so System
follows the rendered light/dark theme. Both original palette/font editor instances
remain available through the mode choices; switching does not discard either
draft. Save/reset and immediate font/accent selection still use the existing
commands and persistence services. No fake theme-preset/import/export buttons are
introduced. Extended fonts and the read-only Diff move into one shared initially
collapsed `AppExpander`; subsequent collapse retains the realized controls.

Four initial rendered cases fail against the old two-card page. The first shared
template build catches missing explicit template/setter target types. Later tests
expose inherited Expander alignment restricting expanded content to its 577-DIP
natural width: the shared wrapper/template now stretch both the host and content.
The expected full-width Diff assertions remain in place at narrow and wide sizes.
Keyboard tests send both Space down and up. Existing focus scenarios explicitly
expand advanced content, retain font selection and color drafts, and establish
that a remembered editor is visible at 1032-DIP height before compact resize;
their no-scroll, dismissal and lifetime assertions remain intact.

The initial native disclosure reports its StackPanel as the accessible name.
Explicit binding to Header repairs the shared template; the final real tree
reports `button 高级 ID: PART_toggle`. Rendered tests additionally cover disabled
click rejection/re-enable, arrow state, retained controls, effective System mode,
independent theme drafts and unreadable-color rejection/recovery through the real
save button. An obsolete source guard requiring extended fonts inside each basic
card is updated to enforce the new shared advanced ownership instead.

The final template's focused group passes 79 cases. Both complete runs made
before the notification-guard correction pass 4,407 cases and fail one: the old
exact MainWindow notification list omits `ActivePalette`. Their failed reports
are retained separately and are not passing checkpoints. The guard is extended,
with a selected-editor identity assertion; all original notification and disposal
checks remain. The rebuilt focused group, including all three lifetime cases,
passes 82 cases. This is a test expectation correction, not a suppressed failure
or change to the theme service/lifetime policy.

## Final checkpoint

After rebuilding the corrected guard, the serial complete App run passes 4,408
cases with zero failures or skips. The other seven complete assemblies pass
1,622: pixels 84, Tools 491, Providers 516, Agent 119, Infrastructure 199, Browser
76 and Platform.Windows 137. The total is 6,030 across eight assemblies; the
82-case focused group is a verified subset, not an additional test count.

The final solution build has zero warnings/errors. Scoped whitespace formatting,
explicit checks of all twenty-one tracked/untracked paths and workspace diff
whitespace pass; the index stays empty. External Python verification checks the
TRX outcomes, assembly paths/hashes, native audit and sampled row measurements.
An independent PowerShell check agrees with the counters, confirms the running
test assemblies stayed unchanged, and verifies test/driver process exit. The
seven other test DLLs retain their recorded tested identities after the guard
rebuild, and the App retains the native-captured production hash below. Both
failed pre-correction complete runs remain separate from this green checkpoint.

## Native evidence and limits

The final production App is built independently under
`.scratch/appearance-palette-final-20261014`. Its SHA-256 is
`BC55A95649D65362885E17EE37B57BD2156158748AC1835C781373A01D37E13E`.
Ordinary-permission native replay verifies System/effective light at 1920x1032,
pointer expansion and keyboard collapse, then Dark at the same viewport. A real
native grip drag reaches 526x601; both theme modes and scrolled advanced Diff/font
controls remain accessible. Light is restored and the App closes normally, exit
zero. No approvals or sharing settings are automated. The synthetic database is
a separate copy, not the user's data directory; all nineteen table hashes remain
unchanged, including the previously cancelled AskEveryTime task and tool audit.

External scripts, data and screenshots stay outside Git under
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-appearance-palette-20261014`.
`native-audit.json` checks identity, viewport/DPI/ordinary-permission metadata,
normal exit, restored preferences and unchanged tables. The opened content area
changes pixels; keyboard collapse restores the closed area's pixels exactly.
`palette-comparison.png` uses the same unscaled source crop coordinates for the
reference, preceding production version and final candidate. Display scaling of
that composed image is not a source-image size change.

Center-line pixel samples independently confirm a 226-pixel basic-card height and
matching row intervals of 48/44/44/44/45 in both 96-DPI images. Their absolute row
positions differ by 21 pixels. Dividers remain RGB 237 in the reference versus
241 in the candidate. These samples do not measure card width or prove complete
pixel equality. Column/title positioning, color-input pills, thumbnail contents,
theme-preset actions and the remaining full-product/state/DPI gates stay open.
The default launch output, JSON/JIT and packaging are not changed. This slice
does not verify a fresh successful child run or complete the overall goal.
