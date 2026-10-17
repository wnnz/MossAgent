# Shared field focus and disabled primary states

## Design and scope

This slice follows the UI/UX skill's desktop-relevant guidance: keyboard focus
must remain visible, hover must not displace it, and disabled controls need native
disabled behavior and theme-appropriate visual treatment. It does not replace the
existing Codex-inspired visual system or apply mobile hit-target requirements.

Required design self-check:

1. App owns the shared appearance. Dependency directions stay unchanged.
2. Existing AppInput/AppEditableChoice and AppButton/AppIconButton wrappers,
   native focus states and command availability are reused; no new service port.
3. No tool execution, file/process/network access, authorization or approval
   behavior changes. Test screenshots render isolated Headless windows only.
4. Production changes are shared AXAML selectors and semantic theme resources;
   no handwritten production C# or page-local appearance overrides are added.
   Source files remain under the repository limits.
5. Pixel/state tests cover focus plus hover, editing, blur, theme changes,
   unavailable-command pointer/keyboard rejection and re-enabled execution.

## Reproduced baseline

The unchanged production UI fails all 20 new pixel/state cases:

- Twelve text/password/multiline/editable-choice cases lose their focused border
  as soon as the pointer enters. The later pointerover template selector changes
  the focus brush from #8E8E8E to the idle #E5E5E5 (Light) or #424242 (Dark).
- Eight primary/send-button cases use #D9D9D9 in Dark, whether starting in Dark
  or switching from Light. Native template presentation is verified, not merely
  the wrapper's property values.

An initial test compile required the Avalonia 12 physical-key parameters and
ContentPresenter namespace; that was a fixture correction, not a product defect.
The final baseline TRX records 20 executed failures, not a compile-only baseline.

## Change

- Idle input hover no longer matches a field with keyboard focus within it.
  The existing focused border, radius, thickness and editing behavior remain.
- Disabled primary buttons and the composite ghost/primary send variant share
  theme-aware semantic brushes. Light retains its original #D9D9D9/#7A7A7A;
  Dark uses #3A3A3A/#8B8B8B instead of a bright Light-mode surface.
- Command availability, enabled appearance, dimensions, caption controls,
  composer borderless editing and custom color-editor surfaces remain owned by
  their existing components/styles. No alternate template is introduced.

## Validation

- All 20 new cases pass after the shared-style correction. They cover actual
  Light/Dark rendering, 100/125/200-percent render scales, hover/focus coexistence,
  keyboard editing, stable geometry, blur and disabled-command behavior.
- The final complete pixel suite passes 208/208 after final formatting/build,
  including those 20 cases. The earlier 20-case and 208-case runs are not added
  again to the final result. The two shared tests use the production style tree.
- The focused App run passes 437/437, covering fields/choices, composer behavior,
  checkbox interactions, custom theme-color fields, shared control styles,
  caption controls, focus/dialog behavior and visual-component enforcement.
  Together these two non-overlapping final scopes pass 645 cases, with no
  failures, skipped/not-executed or aborted cases. This is not a full App or
  eight-assembly result; the previous unexplained allocation failure is not
  investigated or claimed repaired by this appearance-only slice.
- `dotnet build MossAgent.slnx --no-restore -v:minimal -m:1 /nodeReuse:false`
  succeeds with zero warnings/errors. Scoped whitespace-format verification,
  XML parsing, source-size, one-type and whitespace/conflict checks pass.
  Two test assemblies and three matching production App copies retain their
  verified identities across their respective final validation boundaries.

Evidence is outside the repository at
`%TEMP%/MossAgent-interaction-states-20261016`: `interaction-before.trx`,
`interaction-after.trx`, `app-related.trx`, `pixels-current.trx`, build/format
logs, assembly hashes and raw state PNGs. `before-after.png` combines unchanged
100-percent crops for presentation, not a reference-window pixel comparison.
Existing desktop windows and user settings are not accessed. Native screenshots
remain user-postponed; Headless render scaling does not establish native Windows
DPI or same-state Codex parity. Nothing is staged, committed, pushed or released.
