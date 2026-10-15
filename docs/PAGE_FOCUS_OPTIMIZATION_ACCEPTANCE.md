# Page focus selection optimization acceptance

## Scope and ownership

This change removes eager whole-page focus-candidate materialization from
`AppPageSwitcher.SelectFocusTarget`. One lazy visual-order traversal retains
the first usable fallback and returns immediately at the first usable input,
combo box or numeric field. An available remembered target still takes priority.
Focusable pages, hidden/disabled ancestors, stale focus callbacks and page-owned
restoration retain their existing behavior. No visual markup changes.

The required design self-check is:

1. App owns page focus; dependency direction remains unchanged.
2. The existing shared page-switcher contract expresses the behavior; no new port.
3. This is local presentation selection, not tool execution. Authorization,
   approval, timeout, cancellation and output policies remain unchanged.
4. The changed control and two new test files each declare one type and remain
   below the repository file/method hard limits.
5. Focus priority, hidden/disabled rejection and allocation tests protect the
   selector; real MainWindow resize tests protect page/client bounds and drafts.

No AOT, JSON, packaging, commit, push or release change occurs. Existing dirty
work is preserved. Debug/JIT output is isolated in the ignored repository path
`.scratch/page-focus-20261013`, not substituted into the default build output.

## Deterministic evidence

TRX files, scoped validation and final build evidence are under
`%TEMP%\MossAgent-native-surfaces-20261013`.

- Five new selector cases cover the three priority-field types, remembered
  targets, hidden/disabled ancestors, visual order and focusable-page fallback.
  The correct before baseline passes four and fails the allocation case at
  58,096 bytes per selection. After optimization all five pass with the fixture
  budget at or below 1,024 bytes per selection; this is not a zero-allocation or
  whole-page performance claim.
- Eighteen new MainWindow cases cover light/dark, all seven settings tabs and
  the workspace with inspector open/closed. Each window transitions through
  1920x1032, 1280x820, 980x640 and back to 1920x1032. Active page, root, caption
  actions, composer/rail/inspector stay within client bounds. Drafts remain and
  no synthetic model request is sent. These are Headless checks, not native
  Windows resize or high-DPI acceptance.
- Initial test-fixture errors are retained in the original TRX files: an
  unattached UserControl content does not become a visual child, and resize
  assertions need the queued UI dispatcher work to run first. Corrected Grid
  children and dispatcher scheduling pass; neither is a product repair.
- The related group passes 66/66. The first default-parallel complete App run
  passes 4,157/4,158; the existing SidebarFilterAllocationTests project case
  records 104 bytes where zero is expected. Its implementation/assertion is
  unchanged, and the failing TRX is retained. The final serial App run passes
  all 4,158 without failures or skips; no cause for the first failure is asserted.
- The final eight-assembly regression passes 5,720/5,720: App 4,158, visual 24,
  Agent 119, Browser 76, Infrastructure 199, Platform.Windows 137, Providers 516
  and Tools 491. All final runs have zero failures/skips.
- The final solution build has zero warnings/errors. Scoped whitespace
  formatting, type/file-size checks and diff whitespace checks pass.

## Native evidence ownership

The fresh Debug/JIT App assembly SHA256 is
`D384764AC1C1A3AC60011EC51099249DE3B6B2FDBB6412F14541E523BD0E539B`.
Launch metadata, passive PNG/JSON captures, independent fixture data and audits
are under `%TEMP%\MossAgent-narrow-fidelity-20261013-1015\native-focus`.

- Cold startup is maximized at 1920x1032, DPI 96 and ordinary privileges.
  Subsequent accepted frames are native 1280x820; no image scaling is used to
  normalize layout. An initial input is refused because Codex is foreground;
  fresh MossAgent activation/observation precedes retry. Codex is not automated.
- All seven settings pages are captured in light/dark at 1280x820. Provider
  expansion retains two models across page/theme changes. The same model editor
  is opened, cancelled with Escape and reopened without saving configuration.
  Search autofocus, a no-match query and clearing are observed. The long subagent
  page reaches its save region, preserves scroll across switching, then returns
  to its top. Empty subagent/MCP catalogs do not prove configured execution.
- The workspace and all five inspector tabs are captured in dark. The file
  `inspector-home-dark-normal.png` shows Sources, not a separate Home page.
  Diff is empty, Terminal is not started and Browser is unconnected. Audit shows
  restored execution/artifact history, not new tool execution. A final light
  Audit frame verifies restoration of Light theme.
- Before/after closing, read-only comparison confirms all nineteen SQLite table
  schemas and rows match the source backup, including 26 messages, six tasks,
  four terminal automation runs, browser configuration, catalogs and audits.
  Three original artifacts pass hash checks. Only the four historical model
  requests exist; no App descendant process is observed. No tool, terminal,
  browser-start or approval-setting action is invoked during this sweep.
- The QA App closes normally with Alt+F4 and exits zero. Its observer exits zero;
  both original processes are gone and port 51792 has no listener. Python
  screenshot and extended audit scripts remain outside the repository.

## Limits and remaining gates

An earlier App instance had a 1280x820 native client while its content retained
the maximized layout; passive screen and PrintWindow captures both showed
clipping. The fresh instance reflows correctly at normal size, but no root cause
for the earlier instance is established. A restart or Headless dispatcher test
is not presented as a fix for that observed native issue.

Current Codex settings references, PI-Desktop provider references, native
980x640 on this binary, higher DPI, equivalent-content reference comparisons and
the complete interactive-control/state matrix remain open. These screenshots
are bounded native acceptance evidence, not one-to-one whole-window fidelity.
The complete product objective remains active.
