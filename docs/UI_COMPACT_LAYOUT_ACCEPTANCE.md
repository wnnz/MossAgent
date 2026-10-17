# Compact interface polish

## Scope and design review

- App owns these layout changes. No application ports, persistence or platform
  dependencies change; this is presentation of existing data and commands.
- Preserve the Codex-style palette, shared surface shadows, 28px send button,
  12px composer icons and 8px rectangular dropdown corners.
- Keep appearance in shared styles and reuse the existing control wrappers.
  No handwritten file or method exceeds the repository's size limits.
- No new I/O, tool execution, approvals or authorization is introduced. Existing
  projectless restrictions, draft handling, commands and model identity remain.

## Behavior

- The composer model selector previously capped its entire content at 180px.
  An unbounded reasoning label could consume that space and hide the model name.
  Its shared style now allows up to 320px, bounds the reasoning label and keeps
  the full description in the existing tooltip and accessible name.
- Below 360px of actual toolbar width, project actions and model/send actions
  occupy separate rows. Projectless chat does not reserve an empty action row.
  Resize and project switching restore the appropriate layout without sending.
- Below 480px of settings row width, metadata and status move below the title.
  Actions retain their shared alignment and size. Empty metadata rows collapse.
- Focus inside the composer uses the theme's focus border without changing
  border thickness, bounds or the shared shadow. Blur restores its idle border.

## Verification

- `CompactSurfaceRenderingTests` renders production components in both themes,
  320/360/460/640/800px cases and 100/125/200% scaling. It checks readable text,
  contained actions, unchanged send geometry and focus/blur layout stability.
- `ComposerResponsiveLayoutTests` uses the production workspace composition and
  a local synthetic provider to verify repeated resizing, project switching,
  retained drafts/policy and zero unintended provider requests.
- `SettingsItemRowLayoutTests` covers long labels, alignment, live resizing and
  removal of metadata/status without empty layout slots. Existing command and
  projectless keyboard tests exercise the unchanged interaction boundaries.
- Set `MOSSAGENT_VISUAL_EVIDENCE_DIR` to save rendered PNGs outside the repository.
  These are deterministic offscreen renders, not live native-window captures or
  evidence of complete pixel equivalence to Codex Desktop.

## Current validation checkpoint (2026-10-15)

- All six complete non-UI suites pass: Agent 123, Infrastructure 204, Providers
  542, Tools 494, Browser 76 and Platform.Windows 142. Complete pixels pass 136.
  These seven tested assembly hashes remain unchanged after the solution build.
- The first complete App run finishes normally in 13m15s: 4,611/4,615 pass, with
  four failures and no skips/aborts. Its longest case takes 3.654s; this is a long
  complete run, not evidence of a stalled test. The earlier five-minute cutoff
  was not sufficient to establish a complete result.
- The failures are old assertions in `ShellGeometryTests`,
  `SharedControlStyleTests` and the two `ModelSelectorComponentTests` theme cases.
  They still expect the per-view alignment, SurfaceVariants ownership or 180px
  cap replaced by the shared model-selector style. The corrected three classes
  pass all 33 cases without removing cases or weakening containment checks.
- The corrected complete App rerun finishes normally in 13m01s: 4,614/4,615 pass,
  with no skips/aborts. All four shared-style failures above pass in this run.
  The sole failure is the untouched
  `WorkspaceCatalogLookupTests.WarmCatalogLookups_DoNotAllocate(missing: true)`:
  it observes 5,104 allocated bytes instead of zero. Neither that test nor its
  lookup implementation is changed by this UI follow-up; the cause is unproven.
  Its complete five-case class passes three independent no-build reruns, but
  those 15 results do not replace the complete run's failure.
- Across all eight complete suites, 6,331/6,332 pass with no skips/aborts. This
  is not an all-green checkpoint. Focused and isolated reruns are not counted
  again. All eight tested assembly hashes match the checked build outputs.
- Solution build has zero warnings/errors. Scoped whitespace formatting and
  diff checks pass. No user data, running windows, permissions, publishing options,
  commits or remote branches are changed. Native captures remain postponed.

Reports are outside the repository in `%TEMP%/MossAgent-current-goal-tests`:
`app-complete.trx` is the initial complete run, `updated-guards.trx` is the focused
rerun, and `app-final.trx` records the corrected complete rerun.
`catalog-isolated-1.trx` through `catalog-isolated-3.trx` retain the independent
catalog-class reruns. The complete console log is
`%TEMP%/MossAgent-current-goal-app-final.log`. Full-product and current same-state
Codex/native interaction acceptance remain open.

## Model list follow-up (2026-10-15)

- App owns the changes in `AppModelSettingsRow` and the shared provider-row
  styles. There are no new ports, I/O, permissions, persistence, publishing or
  tool-execution changes. Existing model commands and ownership remain intact.
- Below 480px of actual row width, model name, model ID, context and reasoning
  use separate full-width lines instead of competing for two half-width columns.
  Wider rows retain the existing two-column layout. Live resizing restores both
  layouts without replacing the model or changing command parameters.
- Both detail grids share their column spacing. Provider/model text uses an
  18px minimum alignment slot, while row actions retain their centered 24px
  geometry. No per-view dimensions or colors are introduced.
- Essential model details use the existing muted text brush rather than the
  lower-contrast subtle brush. The rendered control properties are checked for
  at least 4.5:1 foreground/background contrast in both themes. Context and
  reasoning gain full-value tooltips; the existing keyboard-accessible edit
  action still opens their complete configuration.
- Before implementation, the new visual checks reproduce a 101px model-name
  slot at 320px and missing reasoning tooltips. Ten model-row cases fail; eight
  navigation-focus cases already pass, so existing focus styling is retained.
- Regression coverage includes 320/480/640px, 100/125/200% rendering, both themes,
  repeated breakpoint crossings, current model command identity, and visible
  keyboard focus without geometry changes on settings/history/disclosure controls.
- Before/after PNGs and test reports are written outside the repository to
  `%TEMP%/MossAgent-ui-polish-20261015`. These are offscreen renders, not captures
  of the user's desktop or evidence of complete Codex pixel equivalence.
- This follow-up passes 111 focused App cases, all 154 pixel-rendering cases and
  seven architecture cases (272 unique cases, no skips). The solution build has
  zero warnings/errors; scoped whitespace verification and diff checks pass.
  This is not a new complete eight-assembly run and does not erase the earlier
  complete run's allocation-test failure documented above.

## Shared alignment follow-up (2026-10-15)

### Design self-check

1. App owns this presentation-only correction; module dependencies are unchanged.
2. Existing shared wrappers, provider commands and task failure hints are reused.
   No new port or service is needed.
3. There is no new I/O, authorization, approval, timeout, cancellation, persistence
   or output-limiting behavior. Evidence uses synthetic values and offscreen windows.
4. The added test file declares one type and stays within the file/method limits.
   Appearance remains in shared styles rather than business views or view models.
5. Rendering tests cover initial geometry, both themes, multiple widths/scales,
   live resizing and shorter hints. Unrelated icons and panels are negative cases
   proving that the corrected selectors do not apply globally.

### Reproduction and correction

- Fourteen new rendered cases reproduce two inactive derived-type selectors.
  `AppUserControl` retains the native UserControl style key; the provider header's
  text stack therefore remains stretched, and failure notices lose their intended
  4px side/16px bottom margins and first-line warning alignment.
- Before/after renders confirm the warning is vertically centered against the
  whole multiline error before the fix. It now aligns with the first line;
  provider text and its 24px actions share a vertical center.
- Both controls gain stable root classes used by the scoped shared variants.
  Native base style keys, shared icon dimensions and command ownership remain
  unchanged. Other static selector candidates are not treated as confirmed bugs
  or modified without their own rendered evidence.
- The first candidate passes all six provider cases and the failure notices'
  initial checks, but eight resize assertions observe queued headless layout.
  The fixture now drains dispatcher jobs before checking the resized short hint;
  there is no sleep, production workaround or weakened geometry assertion.
- Reports and before/after PNGs stay outside the repository at
  `%TEMP%/MossAgent-shared-alignment-20261015`. These are deterministic offscreen
  renders, not native desktop screenshots or a Codex pixel-equivalence claim.
  Native captures remain postponed; no existing application window is operated.
- Fresh validation passes all 180 pixel-rendering cases and 93 related App cases
  (273 distinct cases, no skips). This includes all fourteen new alignment cases.
  The solution build has zero warnings/errors; scoped whitespace formatting,
  diff checks and production/test-copy App assembly identity checks pass.
- This is a targeted UI checkpoint after the complete 6,433/6,435 header run in
  `SETTINGS_PAGE_HEADER_ACCEPTANCE.md`. Its untouched allocation and restart
  cleanup failures remain open; passing this subset does not replace them.
