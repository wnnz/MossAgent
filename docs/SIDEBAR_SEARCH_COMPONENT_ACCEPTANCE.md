# Sidebar search component acceptance — 2026-10-08

## Reproduced baseline

- The previous ordinary-permission real App host accepted a query with no matches,
  leaving a project heading and an otherwise blank sidebar. Its popup supplied no
  empty-result explanation, and a closed popup did not clearly identify an active
  filter. Baseline pixels are in `%TEMP%/MossAgent-sidebar-20261007/search-baseline`.
- The new focused layout test caught a 14-DIP clear icon. The compact button class
  only controls the hit area, not its child's icon size. The owning shared search
  variant now sets that icon to 12 DIP instead of changing every compact button.

## Required design self-check

1. App owns catalog-search presentation and the shared visual component. It introduces
   no concrete Infrastructure or platform dependency into a view or view model.
2. Filtering and clearing reuse `WorkspaceTaskSearchViewModel` and its command.
   No new repository, tool or persistence port is necessary.
3. Search changes only local presentation, never sends a model request, authorizes
   tools or performs file/SQL/process operations. Existing task policy and limits
   remain unchanged; the search query is not newly persisted or logged.
4. The adapter and control have one type per handwritten C# file and remain below
   250 physical lines. Layout and appearance stay in Controls and shared Styles.
5. Focused tests cover live catalog updates, real view bindings, disabled/missing
   commands, lifetime rejection, popup focus and encapsulation in C# and AXAML.

## Behavior and boundaries

- The adapter forwards the existing query and considers visible projects, pinned
  tasks and recent tasks together. A match outside the selected project is not an
  empty result. Collection replacement and live result updates refresh the state;
  disposal removes subscriptions and rejects direct late writes/clear invocation.
- The component owns its trigger, popup, two-way field, clear action and localized
  notice. Business views only bind semantic properties. The trigger highlights an
  active query and exposes it in its accessible name and tooltip.
- Ctrl+K opens and selects the query only when attached, enabled and visible,
  including ancestors. Ctrl+Shift+K is not intercepted. Escape closes without
  clearing the query or changing conversation selection/draft.
- Generation checks invalidate deferred focus after closure, context replacement,
  hidden/disabled ancestors or detach. Closing restores a still-valid previous
  editor only when doing so does not replace a newer editor's focus.
- Clearing reuses the existing command, hides the clear icon for an empty query and
  keeps the search open and focused. A missing command or false CanExecute disables
  the action. Search subscriptions and top-level key handling follow visual lifetime.
- Shared styles own the 270-DIP popup, flat input, 12-DIP clear icon, centered controls
  and active brush. `ValidateSharedSidebarSearch` and `MOSSUI002` reject business
  content/child, padding and dimension replacement, including qualified properties.

## Runtime evidence

- An ordinary-permission App/MainWindow host used isolated synthetic SQLite projects
  and tasks. No synthetic records were inserted into the user's application data.
- Native interaction covered Ctrl+K, selecting the previous query, unmatched text,
  closing while preserving the filter, clearing/restoring all rows, an external
  project match and a pinned-only match. The selected MossAgent conversation and
  its project context remained unchanged. A synthetic composer draft survived.
- Light 1280x820 and dark 980x640 states were inspected at 96 DPI. The first launch
  was captured at 120 DPI before the desktop automation environment changed scaling;
  that frame is not used for the same-DPI comparison or a 120-DPI search claim.
- Passive Python capture rejects black/uniform frames. The popup captures use
  passive desktop capture with owned-popup bounds, since PrintWindow of the parent
  alone does not capture separate native popup windows. No input or resize is used
  by either capture command. Scripts and evidence remain outside the repository.
- `search-verified-20261008/sidebar-search-comparison.png` uses current Codex and
  restored MossAgent native crops at 96 DPI, without resizing. Their final project
  text band is at y=750; sampled project text-band centers differ by at most half
  a pixel. This confirms the search wrapper retained the corrected row rhythm,
  not identical font rasterization, folder paths or complete icon fidelity.
- Popup input focus is absent from the main window's accessibility tree; native
  input/clear/select-all and draft preservation have separate screenshot evidence.
  The updated ordinary main-program Escape smoke check reported the composer edit
  as focused again. More complex restoration and late-focus rejection remain
  covered by headless tests, not independent native race/detach interaction.
- A Codex opened-search screenshot was not available. The search popup is not claimed
  to be a pixel-identical Codex search surface. Rail workflows, remaining icon/font
  details and whole-product acceptance are incomplete; the overall goal stays active.

## Validation

- Focused search tests including the actual MainWindow bindings passed 31 cases.
- Solution artifacts builds passed with zero warnings and errors.
- Focused whitespace verification passed. Roslyn audited eight related C# files:
  one type per file and zero file/method hard-limit violations.
- The first full run exposed two obsolete source-contract assertions after moving
  search markup into Controls. Header-order verification now locates the shared
  component; multi-step flyout verification includes its owned popup and retains
  the original exact count and dismissal checks. The corrected focused run passed
  all 37 search/header/flyout cases.
- One final serial solution test invocation passed App 2070, Agent 58, Browser 76,
  Infrastructure 92, Platform 50, Providers 241 and Tools 147: 2734 passed, zero
  failed and zero skipped. TRX files are under
  `%TEMP%/MossAgent-sidebar-20261007/search-tests/confirmed-final`.
- The ordinary Debug solution build also passed with zero warnings and errors.
  The updated main program was started from its ordinary Debug path; its actual
  process token was not elevated. Python captured a non-black 1920x1032 frame and
  its opened search popup. Ctrl+K/Escape smoke checks left no query or draft behind.
  The isolated QA host closed normally with exit code zero. No AOT, publish,
  staging, commit, push, paid model call or synthetic user-data seeding occurred.

## Header icon fidelity — 2026-10-10

Fresh passive native screenshots at 96 DPI reproduce oversized header glyphs:
Codex's bell and search ink bounds are both 12x12 pixels, while MossAgent's are
12x15 and 15x15. Their ink centers are also 2.5 pixels higher in MossAgent.
Only Codex's eight-pixel external window frame is cropped; neither app is scaled.

Design self-check:

1. App's existing shared navigation appearance owns the header variant; views
   retain semantic classes rather than local colors, dimensions or transforms.
2. Search retains its existing catalog, shortcut, clear and focus contracts.
   The bell still opens the audit inspector, not a fabricated notification feed.
3. No model request, tool execution, approval/policy change, filesystem access or
   persistence migration is introduced by these appearance changes.
4. Related handwritten files retain one type each and stay within size limits.
   Bell geometry is header-specific; the general bell/search resources are unchanged.
5. Focused tests cover the real MainWindow in both themes at three widths, action
   bindings, retained hit areas and isolation from general icon-button appearance.

Final header evidence is under
`%TEMP%/MossAgent-header-fidelity-20261010-0800`:

- `moss-light-final.png` retains native 1920x1032 dimensions. Bell ink bounds are
  `(251,64)-(263,76)` and search bounds are `(283,64)-(295,76)`, matching the
  current light Codex reference at the measured threshold. Stroke curves and
  antialiasing are not claimed to be pixel-identical.
- Dark native 1920x1032, 1280x820 and 980x700 screenshots retain the same 12x12
  window-relative bounds. No current dark Codex reference was captured, so these
  prove MossAgent's geometry consistency, not dark-theme Codex color fidelity.
- Header-specific geometry leaves the general icon resources unchanged. Styles
  target only direct child glyphs; a descendant selector initially affected the
  search popup's clear icon and was rejected by the existing tests.
- Native clicks opened the actual audit inspector and search popup. Ctrl+K from
  the composer and Escape restored its focus and preserved the synthetic draft.
  Clicking the search trigger instead retains trigger focus on close; that path
  is not reported as composer restoration.
- The ordinary-permission, isolated QA host closed normally with `QA_EXIT=0` and
  `QA_REQUEST_COUNTS=0,0`. No model request or tool execution was performed.
- The icon-markup contract now recognizes unconditional, explicitly typed shared
  variants only when their referenced geometry exists. Negative cases reject
  hover-only defaults, incorrect target types, missing resources and bindings;
  the label and manual-child restrictions remain enforced.

Validation: the final header/search/command/markup subset passed 84 cases; the
same solution run passed 3,888/3,888 before the subsequent model-picker lifetime
refactor. The final source's broader focused run passed 147 cases, its solution
build had zero warnings/errors, and whitespace plus one-type/size audits passed.
The subsequent full run passed 3,889/3,890 with no skipped tests: one unchanged
`ProviderProxyRuntimeTests` case reached its five-second fixture cancellation at
SQLite connection opening, before discovery/streaming. Its unchanged 12-case
isolated run passed, but this is not a full-suite pass or a root-cause fix. Logs
and TRX are in `final-optimized-full` and `provider-proxy-isolated.trx` under the
evidence directory. Overall visual/product acceptance remains incomplete.
