# Theme thumbnail artwork acceptance

## Scope and ownership

This slice changes only the shared App theme-preview scene and appearance
variants. It corrects the miniature conversation artwork, not the whole settings
page, theme catalog, preset-action semantics or complete Codex fidelity.

1. App owns the artwork; no module dependency or service contract changes.
2. Existing theme selection, palette Save/Reset and preference persistence remain
   authoritative. No new tool, port, file access, SQL, HTTP or process work is
   introduced in UI code.
3. Native checks use independent synthetic data directories and ordinary
   permissions. Approval/security controls and real user data remain untouched.
4. Shared wrappers and variants remain mandatory. The scene stays 80x60 with a
   66x46 inner screen and two clipped 40x60 halves for System. No production C#,
   JSON, JIT, AOT or Windows x64 single-file packaging changes are introduced.
5. Tests cover geometry, colors, System clipping, application-theme independence,
   selection outline and existing visibility/hit-test boundaries. Compositor
   pixel cases cover Light/Dark application themes at 100%, 125% and 200% scale.

## Measured corrections

- Move the conversation inset from `3,4,3,2` to `4,4,4,3`.
- Narrow the third user line from 24 to 23 pixels; keep other line widths.
- Narrow the miniature composer to 44 pixels and raise it one pixel. Move its
  four-pixel send dot right/up one pixel while keeping its circular shape.
- Use measured artwork ink: accent `#339CFF`, light response `#E5E5E5`, dark
  response `#565656` and light backdrop `#F4F4F4`.
- Preserve the real picker commands, 80x60 hit areas, spacing, focus, disabled
  behavior and selection outline. Generic assistant branding is not replaced.

The reference is the saved 2026-10-14 Codex Appearance screenshot, 1920x1032 at
96 DPI, version 26.1007.2314. A fresh passive capture of version 26.1007.9446 is
481x601 and not Appearance; it is explicitly excluded from same-state evidence.
Unidentified download/clone-looking reference actions are not guessed or wired
to unrelated operations.

## Native and comparison evidence

Candidate: `.scratch/appearance-controls-20261015`.
App SHA-256:
`1FDB53C157E1905B2BDEF6C6CB9A196C911DC3C22AC7152DF76125BDBBBB5235`.
External evidence and Python helpers:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-appearance-actions-20261015`.
They are not repository files or release assets.

`preview-comparison.json` records exact source hashes and fixed unscaled 80x60
regions. The composite is enlarged only for inspection. Mean absolute RGB error
against that historical reference decreases for all three unscaled crops:

| Mode | Before | After | Different pixels before/after |
| --- | ---: | ---: | ---: |
| System | 3.6118 | 2.7876 | 2013 / 1586 |
| Light | 4.1519 | 3.6951 | 2351 / 1596 |
| Dark | 4.2215 | 1.8815 | 1707 / 1243 |

These metrics apply only to the recorded crops, not the whole window or a new
version sign-off. Inner-screen border/shadow, logo and remaining page typography,
catalog/actions, states and native high-DPI equivalence remain open.

Python PrintWindow validates the exact process/path/hash, rejects minimized/black
captures and records 96 DPI and ordinary permissions. Nine accepted frames cover
the baseline and candidate, System/Light/Dark, keyboard Tab/Space activation,
1920x1032, 1280x820, 526x601 and close/reopen. Candidate frames retain the corrected
miniature ink. A failed post-restart navigation frame (`08-restored-system`) shows the
workspace and is excluded; fresh geometry observation followed by one retry
opens Appearance in `09-restored-appearance-system`.

The baseline and two candidate processes exit normally with code zero and empty
stderr. Candidate data keeps all nineteen table hashes unchanged after the
mid-run snapshot, produces no artifacts, and restores the exact original System
preference bytes. Independent fresh databases differ only in schema_versions
migration timestamps; they are not falsely treated as byte-identical databases.
`audit-native.py` independently validates the accepted captures, preferences,
native pixels, process identities, normal exits and bounded crop improvements.

## Test correction history

The initial App regression run started before its DLL was ready and is excluded.
A valid rerun fails four old-artwork cases and passes eight unchanged cases.
Initial pixel diagnostics incorrectly use offscreen RenderTargetBitmap scaling
with nested rounded clips. The final cases instead set the headless window's
render scaling and capture its actual compositor frame. Interior sample points
avoid fractional-DPI antialiasing edges; geometry has separate exact assertions.

The final compositor tests reject the unchanged baseline App in all 18 cases,
then pass all 18 with the candidate App. Focused App tests pass 69 cases. These
subsets are not additional tests beyond their complete-assembly runs. A temporary
external diagnostic executable is interrupted after producing no usable output;
it is not acceptance evidence. No production workarounds are added for it.

## Final validation

The complete solution run passes 6,143/6,143 cases: App 4,498, visual 102,
Agent 119, Browser 76, Infrastructure 199, Windows 142, Providers 516 and Tools
491. There are no failures, skips or aborted cases. The focused 69/18 subsets
are already included in these totals. Initial diagnostic and expected-red runs
remain separate from this complete gate.

The final solution build has zero warnings/errors. Scoped whitespace formatting,
explicit source/type/line-limit/AXAML/whitespace checks and independent
TRX/binary/native-data verification pass. `verify-complete.py` confirms that the
native candidate and both UI test assemblies use the same production App bytes.
No default output, real user data, approval policy or packaging changes; nothing
is staged, committed, pushed, released or installed. The overall goal stays active.
