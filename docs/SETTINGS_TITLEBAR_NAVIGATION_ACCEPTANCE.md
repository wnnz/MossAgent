# Settings titlebar navigation acceptance

## Scope and design checks

The wide Appearance reference has back/forward actions followed directly by
File/Edit/View/Help. MossAgent previously retained a third, redundant navigation
toggle, shifting the first menu's ink from reference x=83 to x=122 at 96 DPI.

1. App owns this presentation. MainWindow bindings and shared layout variants
   consume the existing navigation state; no module dependencies change.
2. Existing AppNavigationPanes state and ToggleNavigationCommand remain the
   behavior contract. No new view model, service, cache or state flag is added.
3. No filesystem, network, tool, approval, timeout or output policy changes.
   Native replay uses only an independent synthetic-data instance.
4. Changes remain within the source/type/method limits. All visual elements
   remain shared wrappers; per-view colors or icon dimensions are not added.
5. Eight new light/dark real-window headless cases cover visible/hidden input,
   menu access, pointer/keyboard drawer operation, resize and state isolation.

## Behavior

Hide the titlebar toggle only while settings are open, navigation is non-compact
and the settings navigation pane is already visible. Preserve it in the workspace,
in compact settings regardless of drawer visibility, and when a wide navigation
pane is closed. The existing handler still targets the active page's navigation.

At this checkpoint the permanent-settings menu variant uses a shared 4-DIP inset
rather than 9. `SETTINGS_TYPOGRAPHY_ACCEPTANCE.md` records the later 5/10-DIP insets
that compensate for updated menu padding while retaining the first ink position.
Workspace and compact states retain their existing inset, history actions,
caption buttons and independent navigation state. File-menu popup access and
Escape dismissal remain real behavior, not replacement text.

The initial source guard is updated to enforce the new bindings without removing
its geometry, command or menu-order checks. An initial test incorrectly expected
Escape to focus the titlebar button; the existing contract restores the remembered
primary editor instead. That assertion is corrected before the accepted red
baseline: all eight cases then fail at the missing new visibility behavior.
An unsupported AXAML class property-element binding initially fails compilation;
ordinary class bindings and a shared compound selector correct it.

## Native and pixel evidence

External artifacts: `%TEMP%/MossAgent-settings-titlebar-20261015`.
Independent Debug/JIT build: `.scratch/settings-titlebar-final-20261015`.
App DLL SHA256:
`75B12AD68C895089BFF41F2E2CB7BE15BBB4BAB87520906D1CE42ED99332E8D8`.

The accepted ordinary-permission replay (PID 3884) produces seventeen PrintWindow
captures at 96 DPI: 1920x1032, 1280x820 and 526x601, both themes, real menu popup,
drawer open/Escape, maximize from an open drawer, workspace collapse/reopen and
settings/workspace state independence. It restores the exact original Light/font
preference bytes, preserves nineteen database table hashes and creates no artifacts.
The App exits normally with code zero, disappears, and leaves empty stderr.
`audit.py` verifies these facts independently.

An earlier instance disappears during a runtime/session update before any accepted
capture; its receipt is retained, not described as a normal exit. A coordinate
mistake later minimizes the replay instance. The resulting 160x28 frame numbered
10 is explicitly excluded. Rebinding/activation recovers the same process, then
the observed maximize action succeeds. The external capture helper now rejects
minimized and undersized frames before saving them; no product change is needed.

`titlebar-comparison.png` uses unscaled reference/before/after crops. File ink now
starts at x=83 and Edit at x=136, matching the saved reference starts. View remains
one pixel and Help three pixels farther right; glyph widths/rasterization still
differ. This is a removed extra-action/inset correction, not exact menu-font proof.
Title ink is deliberately unchanged rather than moved by an unverified transform.

The saved full-size Codex reference remains the Appearance frame under
`MossAgent-child-settings-runtime-20261014`. A later passive capture observes a
newer Codex version in a 481x601 conversation window, not equivalent Appearance.
It is not substituted into the comparison or claimed as current full-page proof.

## Validation and remaining gates

The final focused run passes 60 cases, including the eight new cases. The complete
solution run executes 6,091 cases: 6,090 pass and one fails, with no skipped or
aborted cases. App passes 4,468/4,469; visual 84, Agent 119, Browser 76,
Infrastructure 199, Windows 137, Providers 516 and Tools 491 all pass.

The sole failure is the unmodified `WorkspaceCatalogLookupTests` missing-identity
allocation assertion: 2,552 bytes instead of zero. Three independent reruns of
its complete five-case class all pass on the same assembly. These fifteen rerun
cases and the focused sixty are not added to the complete-run total. The complete
run remains explicitly not all-green; intermittent allocation behavior is not
explained or fixed by this UI change, and no assertion is weakened.

The solution build passes with zero warnings/errors; scoped whitespace formatting,
source size/type checks and workspace diff checks pass. External verification
records report/assembly/source/native identities, the non-green complete-run
result and successful isolated reruns. No JSON, AOT or packaging behavior changes.

Current-version same-state references, residual title/menu typography, other
surfaces/states, real DPI transitions and whole-product acceptance remain open.
Nothing is staged, committed, pushed, released or installed. The full goal remains
active; this bounded UI acceptance does not redefine its scope.
