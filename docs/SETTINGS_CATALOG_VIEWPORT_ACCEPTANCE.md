# Settings catalog viewport acceptance

## Confirmed failure and correction

Proxy and MCP settings measured their catalog cards in an Auto-height row. A
vertical StackPanel then gave the ListBox unbounded height until its fixed
560-DIP cap, regardless of the actual page viewport. At 601-DIP window height,
the bottom of the scrollbar and list could remain outside the visible page.

All eight original cases fail: both pages, Light/Dark, 486/1024 DIP wide. At the
compact width the list occupies `(37,202,402,560)` and extends to y=762; the wide
case occupies `(152,160,710,560)` and extends to y=720. Neither fits a 601-DIP page.
The failing TRX and original rendered frames are retained without replacement.

Both pages now use Auto header and remaining-height catalog rows. A finite Grid
inside the card constrains the existing ListBox, while a shared top-aligned card
variant preserves natural empty/single-row height. The existing 560-DIP maximum
is moved into one shared ListBox variant; it is a ceiling, not a forced height.
Native scrolling, item virtualization, selection, row commands and row appearance
are retained. No manual item truncation, forced layout or custom scroll handler
is added.

Status feedback now uses the existing shared page-header status slot, including
its 96-DIP scroll bound and complete keyboard-readable text. Proxy error styling
retains its error binding; MCP retains its existing informational presentation.
Modal editor status/notice bindings and footers remain unchanged.

## Design self-check

1. Ownership stays in App view layout and `SettingsLayoutVariants.axaml`; there
   are no new production C# types, module dependencies or platform APIs.
2. Existing AppCard/AppListBox/AppSettingsPageHeader wrappers and commands are
   reused. Page geometry remains structural; appearance variants remain shared.
3. No new production I/O, approvals, tool execution, cancellation policy, proxy
   transport, configuration schema, provider behavior or credential handling.
4. The modified AXAML files are 96/92/74 physical lines. Five new C# test files
   are 149/103/96/93/83 lines, with one type per file and methods below 80 lines.
5. Thirty-item catalogs prove the failing path; empty/single-item, repeated
   resizing, keyboard selection, pointer hit testing and actual row actions
   protect the successful path. Existing safety suites remain authoritative.

The ui-ux-pro-max Avalonia guidance is applied by retaining semantic selectable
lists and native virtualization rather than replacing them with hand-built rows
or hiding items. This is not a redesign of Codex's established visual direction.

## Production UI coverage

The focused run passes 47 cases, including all 36 new cases:

- Twenty viewport cases cover two pages/themes/widths, natural empty/single-row
  height, create/cancel and five repeated compact/wide size transitions.
- Eight mutation cases edit the last row using real text input and Save, verify
  the persisted target in probe repositories, then invoke Test/Enable and check
  target identity, unchanged unrelated items and visible status feedback.
- Eight complete MainWindow cases use production composition and isolated SQLite,
  with 30 proxies and 30 disabled MCP profiles. At 526/1280 by 601 DIP in both
  themes, the last row's actual hit target is visible; keyboard edit/save reaches
  SQLite with the same ID and unchanged non-name fields, without adding records.

The initial candidate's focus assumption and the expanded fixture's assumption
that a scrolled-off first item remains realized are corrected in test helpers.
Keyboard traversal now starts on a realized native item and uses End; all
original containment/hit/identity assertions remain. These are fixture corrections,
not additional product defects. Their original candidate logs are retained.

## Evidence boundaries

Evidence root:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-settings-catalog-20261016`

`baseline/` and `baseline.trx` retain the eight failing page states. `shell/`
contains corrected page, last-row, status and complete-window renders.
`proxy-before-after.png` and `mcp-before-after.png` compare equal-size page renders;
the labels explicitly identify them as off-screen images.

Tests use synthetic data and independent Headless sessions. The MCP profiles are
disabled in the real-composition cases; no external MCP process is started. The
probe tests use a recording proxy tester and deferred MCP manager, not real
network connectivity. Existing authenticated transport tests are separate coverage.
No existing Codex/MossAgent window is operated, captured or restarted. Native
screenshots remain user-postponed; these renders do not prove native DPI behavior,
same-state Codex parity, the separate native resize fix or full-product completion.
Windows x64/JIT single-file publishing, AOT/JSON choices and user data are unchanged.
Nothing is staged, committed, pushed or released.

## Final regression and coverage audit

The first same-process full App attempt is interrupted after the tool host closes
its output. Its session is subsequently missing and no dotnet/testhost process
remains; no final TRX is produced. `app-full.log` retains an observed untouched
`SidebarFilterAllocationTests` project/matching failure: zero bytes expected,
1,200 measured. This is not a successful full run, a proved application hang or
a causal allocation fix. The later batched pass does not erase that observation.

Fresh discovery lists 4,784 cases across 581 App test classes. Six disjoint
class-prefix partitions run every discovered class; all 4,784 cases pass with no
skips or aborts. `coverage-audit-final.json` verifies all 4,784 distinct TRX test
IDs and exact per-class discovery/result counts, with no class overlap or omission.
Fourteen display-name groups are truncated duplicates, so display-name uniqueness
is not mistaken for test identity. The 36 new cases are included, not added again.
This verifies the complete App case set across partitions, not one uninterrupted
same-process all-green run or resolution of the original allocation observation.

| Final check | Result |
| --- | --- |
| Complete App case set, six partitions | 4,784/4,784 |
| Complete pixel suite | 188/188 |
| Complete Providers suite | 542/542 |
| Infrastructure proxy/HTTP subset | 20/20 |
| Architecture subset | 7/7 |
| Solution build | Zero warnings/errors |
| Scoped whitespace verification | Passed for all six touched C# files |
| Source/document hygiene | Passed, including untracked files |
| Assembly identity after final build | All eight recorded hashes unchanged |

The three runtime App copies are identical. Baseline/focused/partitioned results
are not summed as independent new tests. The final table represents 5,541 checks
within the stated suite/subset scopes, not a new complete eight-assembly total.
The historical 6,495 eight-suite checkpoint remains historical. Other complete
non-UI suites and postponed native/Codex acceptance are not rerun by this slice.
Final logs, discovery/partition manifest, TRXs, build/format logs and before/after
hash CSVs stay in the external evidence root. The product goal remains open.
