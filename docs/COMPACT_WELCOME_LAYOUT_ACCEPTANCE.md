# Compact empty-conversation heading

## Reproduced defect: 2026-10-14

Ordinary-permission native Python captures of the previous production App show
the same defect in both themes at 526x601 and 96 DPI: the selected empty task's
three-line welcome heading extends underneath the composer context strip. The
brand icon also intersects the floating project-context card. These are layout
defects, not evidence that the separate native DPI/geometry issue is repaired.

The preceding native surface matrix also exercises Appearance, provider/model,
browser, proxy, MCP, agent and subagent settings, retained/cancelled model drafts,
empty-form validation and inspector surfaces. It runs against the earlier App
hash, not this fix; it is retained as prior-version evidence only. No real model,
tool, terminal or browser execution is added by either matrix.

## Design self-check

1. App's shared layout controls own this behavior. Module references and all
   Domain/Application/infrastructure/platform contracts remain unchanged.
2. AppProjectPrompt keeps its bindings, retained project button, command, tooltip,
   accessible name and flyout. Its layout panel measures styled content: one row
   when it fits, prefix/project plus suffix on two rows otherwise, and a bounded
   three-row fallback only when even the prefix cannot share a row. Long names
   use the existing ellipsis rather than making an unnecessary third row.
3. AppEmptySessionLayout replaces the view's centering StackPanel. Shared styles
   still own the preferred 32-DIP brand offset. The panel accounts for this offset
   in arrangement and reduces the gap only when the available height requires it.
   Sufficiently tall viewports preserve the previous brand and heading positions.
   No context card is hidden, font reduced, native window size forced or DPI
   message intercepted to conceal the problem.
4. Each new handwritten C# file contains one type and remains below 250 lines;
   methods remain below the hard limit. No subscription, cache, package, service
   locator, native control bypass or persistence migration is introduced. This
   is Debug/JIT; Windows x64 single-file publishing and JSON behavior are unchanged.
5. MainWindow tests cover actual card/composer/brand bounds, two sizes, both themes
   and short/80-character names, without provider requests or selection changes.
   Component tests retain repeated resizing, matching row centers, non-overlapping
   fragments, long names, Tab/Space, disabled/missing commands, flyout and tooltip
   identity. Additional tests cover odd widths, the extreme-width fallback,
   compact brand containment, exact tall-viewport positions and invalid offsets.

Authorization, approval policies, task cancellation, timeouts, path validation,
output limiting and storage remain outside these synchronous layout components.

## Test development and limits

- The first eight red results used the stretched InspectorPane host as the card
  boundary; they are not accepted as a product regression baseline. After locating
  the visible inspector-surface AppCard, the unchanged product fails six cases and
  passes two. Four failures expose the actual composer overlap; two long-name
  cases expose the brand/context overlap.
- Old Grid-definition assertions are replaced with actual row/bounds checks,
  retaining adaptive-pane measure/arrange and command safety coverage. Intermediate
  failures remain in the external logs. A real one-pixel row overlap is fixed by
  rounding the shared row origin and child slots on the actual Avalonia layout
  scale. Tests also drain queued resize work before asserting rendered geometry;
  changing a Window property alone is not proof its actual bounds have settled.
- The initial focused run passes 40 cases with zero failed/skipped cases. Complete
  pixel regressions pass 84. Solution build has zero warnings/errors and scoped
  whitespace-format verification passes. Complete App regression is recorded in
  the final checkpoint below; focused cases must not be counted twice.
- The first complete App run passes 4,387 cases and fails one old source guard
  requiring StackPanel Spacing and an icon RenderTransform selector. The corrected
  guard requires the shared layout, style-owned brand offset, no view override,
  retained dedicated icon and unchanged active-conversation header divider.
  This first complete run is not accepted as a passing checkpoint.
- The final focused run includes the related shell source guards and passes all
  53 cases. The production App hash remains unchanged after the guard-only update.

## Native replay

The final independent App assembly SHA256 is
`A3B7F0C56C86B1E720A8D54C19A7B7D67C357A1014A7169F9E088BB77430FAFA`.
The normal-permission test instance uses only a fresh synthetic database copied
from a closed fixture; no user database or default launch output is replaced.

- Passive Python captures verify light 1920x1032, restored 1280x820, and real grip
  resizing to 980x640 and 526x601. Both themes at 526x601 show the complete two-row
  heading above the composer and the full brand below the context card.
- Clicking the retained project name opens the real picker at the minimum size;
  Escape dismisses it without changing projects. Native long-name interaction is
  not rerun here; it has deterministic real-MainWindow bounds coverage.
- Dark-theme maximize to 1920x1032 restores the single row; native restore to
  526x601 restores two rows. A transient null UIA result after maximize is handled
  by fresh window selection and observation, not a repeated maximize click.
- Restoring Light produces unchanged audits of all nineteen tables, three stored
  artifacts, the existing four-request history and UI-setting bytes. Titlebar
  close terminates App and observer normally with exit zero; both processes and
  the loopback listener disappear. The final closed-fixture audit also passes.
- Unscaled light/dark before/after comparisons retain both 526x601 frames. They
  demonstrate this MossAgent fix, not Codex pixel equivalence. Fresh passive Codex
  remains a maximized light conversation, not Appearance or an equivalent empty
  conversation; no Codex UI input is automated.
- At 1920x1032, the unscaled `(800,380)-(1400,545)` brand/heading crop has zero
  changed pixels against the preceding production assembly. This verifies the
  existing wide welcome appearance, not other areas or Codex equivalence. The
  earlier frame is named light-settings-max but visibly shows the workspace;
  it is used only as workspace evidence, never accepted as an Appearance capture.

Raw captures, scripts, fixture audits and tests remain outside Git:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-compact-welcome-20261014`.
Earlier surface captures and intermediate/red tests remain at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-ui-matrix-20261014`.
The final build is isolated under `.scratch/compact-welcome-20261014`.

## Final checkpoint

The final complete App run passes 4,388 cases, complete pixels pass 84 and the six
complete non-UI suites pass 1,538: 6,010 across eight assemblies, without counting
the 53-case focused subset twice. All final drivers exit zero; reports contain no
failed, skipped, aborted or unexecuted cases. External checkpoint verification
binds TRX and assembly hashes to the native App hash, checks all seven native
capture identities/geometries, recomputes the unchanged wide crop, and verifies
restored data, normal cleanup and clean build/format/whitespace results. The
machine-readable record is `verified-checkpoint.json` in the external evidence
directory. The initial failed complete report remains separate and is not merged
into this passing checkpoint. Full-product acceptance, same-state Codex
settings/reference coverage, actual DPI transitions and native geometry diagnosis
remain open. Nothing is staged, committed, pushed, published or installed; the
overall product goal stays active.
