# Shared settings page headers

## Design self-check

1. App owns the presentation. `AppSettingsPageHeader` composes existing wrappers
   and shared appearance variants without adding dependencies to other modules.
2. Pages pass their existing create, refresh and reconnect commands through the
   dedicated action slot. Provider status/error bindings retain their owner.
3. No new I/O, task capabilities, approvals, cancellation rules or data writes
   are introduced. Tests use independent synthetic repositories and tool managers.
4. Each new C# file declares one type. The component, styles and tests stay below
   the repository's file and method limits; no large class or partial split is used.
5. Production-page keyboard tests cover real commands. Component rendering,
   live resizing, status removal/replacement and negative appearance-policy tests
   protect the shared presentation and its business-code boundary.

## Reproduced issue and correction

- At 486px page width, the old child-agent description needs 299px but receives
  215px; the custom-agent description needs 312px but receives 228px. Proxy
  settings also clips its 247px description into 244px. Six baseline cases fail
  across both themes; the other fourteen page cases already pass.
- Providers, proxies, MCP, custom agents and child agents now share one header.
  Below 480px actual header width, actions move below the title/description/status.
  Wider headers retain right-aligned actions. Text wraps rather than being clipped.
- Header spacing and action spacing live in shared styles. The three grid-owned
  headers retain their 24px list separation; scroll-stack pages retain their
  existing stack spacing rather than adding a second margin.
- Pixel review catches a candidate style-key mismatch: AppUserControl and
  AppContentControl retain native style keys, so the new variants use stable
  wrapper classes rather than derived-type selectors. Page tests explicitly
  check the separated margin. No base wrapper style keys are changed globally.
- Provider discovery failures still pass through the existing sanitizer into
  the shared error status. A production-view test checks visibility, readable
  wrapping, error color and absence of synthetic secret text in both themes.
- The old model-discovery markup guard checks the page-to-header status/error
  bindings and the component's actual status visibility/error classes. Its
  independent editor-error assertions remain intact.

## Evidence boundaries

- `SettingsHeaderPageLayoutTests` uses production view models and keyboard input
  on all five pages in both themes at 486/1024px. It opens and closes real editor
  dialogs and awaits the real refresh/reconnect command; it does not call a paid
  provider or modify user data.
- `SettingsPageHeaderRenderingTests` covers 320/480/728px, 100/125/200% scaling,
  error status, cleared status/description/actions, repeated breakpoint crossings
  and action replacement. Empty-slot checks assert cleared content and a
  heading-only extent, not stale retained bounds on an empty presenter.
- Negative C# and AXAML cases reject business overrides of component content,
  padding and dimensions while allowing data and the dedicated action slot.
- Baseline reports, candidate failures and before/after PNGs stay outside the
  repository in `%TEMP%/MossAgent-settings-header-20261015`. They are deterministic
  offscreen renders, not desktop captures or same-state Codex parity evidence.
- Native screenshots remain user-postponed. This slice alone does not complete
  the full product objective or the complete responsive/theme/interaction matrix.

## Complete validation checkpoint (2026-10-15)

- The first complete binary checkpoint passes 6,431/6,433 across eight assemblies.
  Its two failures are the old `ModelDiscoverySafetyTests` inline-status guard
  and `SharedControlStyleTests` inline-heading guard. Both now inspect the shared
  component without removing their error-visibility or typography assertions.
- The final complete App run finishes normally in 13m30s at 4,686/4,688. All 36
  new App cases pass. All six non-UI suites pass 1,581 cases and complete pixels
  pass 166, including all twelve new header-rendering cases. The eight complete
  suites therefore pass 6,433/6,435, with no skips/aborts. Focused runs are not
  counted again. This is not an all-green checkpoint.
- The untouched `SidebarFilterAllocationTests` recent/missing case reports
  3,288 bytes instead of zero. The untouched conversation-restart case reports
  an `IOException` at line 75 while deleting its owned temporary directory.
  The empty directory remains for inspection; a current locking owner is not
  identified. Cleanup failure does not prove or disprove all earlier restoration
  assertions, and neither underlying cause is claimed fixed.
- Both complete classes pass three independent no-build reruns (nine cases per
  run). These 27 results retain the strict assertions and do not replace the
  original complete-run failures. Neither production lookup/restoration behavior
  nor these two test files is changed by this UI slice.
- The final sequential solution build has zero warnings/errors. Scoped
  whitespace verification, tracked/untracked whitespace checks and eight tested
  assembly identity checks pass. An intermediate overlapping verification build
  emitted an MSB3026 copy-retry warning; it is not represented as a clean build.
  All test processes exit normally; no running user window or data is changed.
- Original complete reports are under `full/`; the final complete reports,
  build log and before/after assembly hashes are under `final/`. `isolated-1.trx`
  through `isolated-3.trx` retain the separate class reruns. `rendered-final.trx`
  and `after/` retain the final header render with fresh build dependencies.
- A separate static style-key inventory flags five other candidate selectors in
  `style-key-candidates.csv`. They require rendered verification before changes;
  this inventory is not a claim that those controls fail or have been fixed.
