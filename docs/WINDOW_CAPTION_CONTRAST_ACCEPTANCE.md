# Window caption contrast

## Reference and design self-check

The preceding unscaled passive comparison at 96 DPI shows the same minimize,
maximize and close ink extents, but foreground luminance is 31 in Codex versus
93 / 130 / 93 in the current App. App's caption buttons inherit the ordinary
ghost/close TextMutedBrush (`#5D5D5D` in light); vector rasterization additionally
affects the maximize sample. Treat palette inheritance and rasterization as
separate causes; a brush correction alone must not be reported as pixel equality.

1. App's shared window-caption style owns this presentation. No module dependency,
   native process integration or per-view appearance assignment is needed.
2. Reuse AppIconButton, existing caption commands, hit areas and pointer/focus
   behavior. Dedicated caption palette resources must not recolor navigation,
   ordinary close actions or other ghost buttons.
3. No tool, provider, persistence, approval, cancellation or authorization behavior
   changes. Native replay uses an independent synthetic data root and no message
   submission; Python capture remains passive and outside the repository.
4. Consolidate caption geometry and palette in a shared caption variant loaded
   after generic button styles, keeping markup under its hard limit. Each new
   test type gets its own file.
5. A red baseline must reproduce the muted foreground in both themes and sizes.
   Verify normal/hover/leave, red close hover and white glyph, Arrow cursor,
   retained 46x36 hit areas, non-focusability and unchanged navigation color.
   Then run related tests, pixel tests and a solution build, followed by native
   comparison. Dark caption readability is tested; without a matching dark
   reference, do not claim exact dark Codex equivalence.

## Shared correction and deterministic evidence

The caption-only resource uses `#1F1F1F` in light and white in dark. Shared
`WindowCaptionVariants` owns normal/hover/disabled palette and the retained button
geometry. It loads after generic ghost styles; a first rule in the earlier
WindowVariants is ineffective because later generic styles overwrite it. That
ineffective rule is removed rather than retained as a competing override.
The existing red close-hover rule and white glyph remain authoritative. Ordinary
navigation and non-caption ghost/close buttons keep their existing muted colors.
App.axaml is 494 physical lines; caption geometry stays in its dedicated variant
and palette resources rather than growing the application markup further.

Evidence lives outside the repository at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-caption-contrast-20261014`.

- All four baseline cases fail with the old muted caption foreground. The first
  style-only attempt also fails all four and reveals include-order precedence.
  A subsequent test lookup incorrectly requests a theme resource without a theme;
  the test now compares the existing documented light/dark muted colors directly.
  Final caption, icon, focus and window appearance coverage passes all 36 cases.
- Eight real Skia rendering cases exercise both themes at 1 / 1.25 / 1.5 / 2
  render scale. Low-alpha PNG round-tripping exposes over-strict unpremultiplied
  channel assertions, not a palette change. The final guard allows two eight-bit
  premultiplied channel quantization units. Eight negative cases prove the old
  muted colors cannot satisfy that guard at alpha 64 / 128 / 192 / 255. All sixteen
  new cases pass. These are render scales, not native monitor/DPI transitions.
- The first root-level driver exits one on the earlier too-strict pixel guard
  (55/56 pass); it is terminal before the repaired test assembly is built. Its
  files remain failed history, not passing final evidence. `final-gates` belongs
  to the palette-only driver and recorded palette-only assembly hashes.
- Final scoped formatting and a solution build pass with zero warnings/errors.
  All 64 pixel cases and seven architecture constraints pass. The complete App
  assembly gate subsequently finishes with 4,264 passes and two failing source
  ownership guards still looking in WindowVariants. Those guards now follow
  WindowCaptionVariants and also require its include after generic ghost rules.
  This failed historical complete run is not counted as a passing checkpoint.

Palette-only App DLL SHA256:
`F9D569C3A83DDDFE24AA04FAB750E92567B8F8B83FA14E61F7B0DEE855838A1B`.
Palette-only App test DLL SHA256:
`A6022EACC42AEF1CE8D64C674BA8DE3D9778CB80EE3F5541B309FF84EBD00BD1`.
Palette-only pixel test DLL SHA256:
`AF6C38988A92A8F206FFDF6FF0CF19AFB6762CBEF7C1AD538B7627839660855A`.

## Native replay and actual comparison

The same Debug/JIT App runs at ordinary permission and 96 DPI with an independent
synthetic data root. Passive Python capture verifies PID ownership, foreground,
window geometry, elevation and non-black content. Five settled native frames
cover light max/normal, dark normal/max settings and restored light max settings.
Immediate Computer Use input frames contain a transient blue pointer highlight;
they are not substituted for the settled Python images.

The real restore action returns 1920x1032 to 1280x820. The real minimize command
sets the owned HWND's IsIconic state true while its App process remains alive.
Reactivating that same returned window makes IsIconic false and restores its
maximized settings page. This is native command evidence, not just icon presence.
The actual close command subsequently exits the App and observer zero; both
processes disappear and loopback port 51792 is released.

Open-after-theme-restoration and closed audits both pass integrity, all nineteen
table fingerprints, three original artifacts and the unchanged four-request
history. Original Light UI-settings bytes are restored exactly. No real user
settings, model submission, approval, AOT, JSON adapter, packaging, commit, push
or release changes occur.

`caption-native-before-after.png` contains unscaled 96-DPI ordinary-window caption
crops from fresh Codex, the preceding App and this candidate. All three preserve
10x1 / 10x10 / 10x10 ink boxes at the same comparison threshold. Native darkest
caption luminance changes from 93 / 130 / 93 to 31 / 84 / 31; the reference is
31 / 31 / 31. Minimize and close now reach the reference foreground, while the
maximized-window restore and ordinary maximize rasterization remain separate
unproven gates. In particular, the ordinary maximize rectangle still has no fully
opaque reference-dark edge in this sample. Do not claim palette correction repairs
subpixel geometry or all caption pixels.

The fresh Codex reference remains a 526x601 conversation with collapsed sidebar,
not Appearance. Equal local caption ink bounds do not prove same-state page,
native high-DPI, hover/pressed/inactive or complete interface equivalence. The
separate native resize/clipping root cause, responsive surfaces, same-state
Appearance/PI-Desktop references and full product objective remain open.

## Ordinary maximize raster correction

The palette-only native comparison leaves ordinary maximize at luminance 84.
Size=9 and half-pixel positioning alone still produce an 11x10 alpha>=64 ink
box in Skia, so neither a different threshold nor a larger allowed box is used
to mask the failure. An external diagnostic renderer records the left edge
split across alpha 103/152 while the opposite edge is alpha 255. Direct geometry
replay isolates the round line cap: flat caps remove that extra column while
changing the join alone does not. These diagnostics are outside the repository.

The shared ordinary-maximize variant now uses Size=9, translation (+0.5,-0.5)
and flat caps. Minimize, close and alternate restore keep their existing geometry
and caps. AppIconButton exposes its existing alternate state as a pseudo-class,
retaining the same button and icon instances. No generic icon renderer changes.
Odd/even-width light/dark layout cases retain 46x36 hit areas and centered icons.
Two raster cases require the unchanged 10x10 ink box, opaque middle pixels on
all four edges and a transparent center, then exercise maximize/restore sizing.
All 18 caption-palette pixel cases and 53 focused App/window cases pass.

The newer candidate App SHA256 is
`4200603DCE4B46855B346BECE3B44FE75CDF0980410D44C6E4CFC56142DB6386`.
`native-raster` uses a fresh independent data root, not the already-closed
palette fixture. Settled passive Python frames cover light normal/max conversation,
light/dark normal/max Appearance and restoration to light. Actual native maximize,
restore and minimize/reactivation are verified at 96 DPI; IsIconic changes true
then false on the same owned HWND while the process remains alive. Open-data
audits retain all nineteen tables, original artifacts and four-request history,
and restore the exact initial Light preference bytes.

`caption-raster-native-comparison.png` preserves unscaled ordinary-window crops.
The reference, palette-only and corrected candidates all measure 10x1 / 10x10 /
10x10 at the existing native threshold. Corrected light caption luminance is now
31/31/31, compared with palette-only 31/84/31 and reference 31/31/31. This proves
the sampled ink extents and peak contrast, not equality of every caption pixel,
restore glyph, dark reference, interaction state or complete page. A new
foreground-only passive Codex capture is correctly refused while another app is
in front. Passive PrintWindow capture subsequently returns a verified non-black
526x601 conversation at 96 DPI from the same installed 26.1007.2314.0 version;
the comparison uses this fresh reference. It is not the requested Appearance
page. No Codex UI input is issued.

The corrected App and observer both exit zero after the actual close action.
The launcher also disappears and port 51792 is released. A further post-exit
audit passes the same table/artifact/request/UI checks. Neither fixture is reused
after closure. No real data, approval, AOT, JSON adapter or packaging changes occur.

The first `raster-final-gates` driver passes formatting/build but exits before
tests because Windows PowerShell cannot resolve Get-FileHash in that inherited
environment. It is terminal failed history. `raster-final-gates-v2` runs under
the verified PowerShell 7 host, records the candidate assembly identity, and
passes formatting, a zero-warning/error solution build, all 66 pixel cases and
seven architecture constraints. Its complete App gate finishes with all 4,271
cases passing, zero failures/skips/aborts, and driver exit zero. Test/pixel DLL
SHA256 values are respectively
`DB78C9AD31CA1ECE9CBC17B639E913AC5F0F180001ACD56931726545D25736D2` and
`E36C9085451CD5F460AD172AC76BDA123D334CC2A5222D82FAE680332D8B3AE8`.

`verified-checkpoint.json` independently parses terminal TRX counters and verifies
current App/test/pixel hashes. The preceding complete six non-UI groups total
1,538 passes (119 Agent, 491 Tools, 516 Providers, 199 Infrastructure, 76 Browser,
137 Platform.Windows); all six test DLL hashes remain unchanged after the final
solution build. Together with the 4,271 App and 66 pixel cases this establishes
a current eight-assembly 5,875-case regression checkpoint, not complete product
acceptance. The seven architecture cases are a subset, not additional totals.
Native post-exit data audits and process/port cleanup also pass. No commit,
push or release occurs. Same-state Appearance/PI-Desktop comparison, original
native clipping/high-DPI investigation, broader narrow-window adaptation and
the remaining product state/function matrix stay open.
