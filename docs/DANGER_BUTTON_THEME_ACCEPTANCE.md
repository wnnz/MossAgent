# Shared danger-button theme repair (2026-10-13)

## Design and scope

1. App owns this appearance-only behavior. Its existing shared `danger` button
   variant now resolves three theme resources; no dependency direction changes.
2. Existing resource dictionaries and wrappers suffice. Commands, labels, hit
   areas, sizing, disabled treatment and style order remain unchanged.
3. There are no authorization, approval, cancellation, output or persistence
   changes. Native QA uses independent read-only database backups with a paused
   plan and a loopback observer that records and refuses unexpected requests.
   No delete, model fetch, send, run, policy change or permission prompt is invoked.
4. App.axaml is 497 lines, the new resource dictionary 15, and the single-type
   DangerButtonThemeTests 138. Methods remain below the hard limit.
5. Tests cover both themes, live switching, hover/press, hit-area preservation,
   text contrast of at least 4.5:1, and disabled-command nonexecution/recovery.

## Reproduction and repair

The 980x640 native light provider page exposes maroon delete buttons because
the shared `danger` variant hard-codes dark background/border/foreground colors.
All nine markup consumers, including settings deletion and terminal Stop, use
that same variant. No page-local palette is added.

`DangerButtonResources.axaml` supplies Light and Dark theme dictionaries. Light
uses `#FEF2F2` / `#FECACA` / `#B91C1C`; Dark retains the existing
`#2E1517` / `#6B2127` / `#F87171`. Three DynamicResource setters replace the
hard-coded colors. The existing disabled style still resolves muted resources.

Evidence stays outside source at
`%TEMP%\MossAgent-narrow-fidelity-20261013-1015`.

- The three saved baseline TRX files reproduce four genuine light/switch palette
  failures. They also contain test-authoring failures: two incorrectly assumed
  muted light colors, followed by a resource lookup missing ActualThemeVariant.
  Those harness mistakes are corrected without changing production disabled
  colors or weakening its command nonexecution assertion.
- The first corrected eight-case group passes 8/8. After adding two contrast
  cases, the wider group passes 152/152, including all ten new cases, provider-row
  commands/layout, navigation state, shared-control boundaries and affected
  settings/terminal consumers. None fail or skip.
- The complete pixel assembly passes 24/24; architecture constraints pass 7/7.
  The solution builds with zero warnings/errors, and scoped whitespace formatting
  verification passes. XML parsing, size checks and scoped diff checks also pass.
- The earlier 5,667-case complete regression belongs to the sidebar-icon source
  checkpoint, not this later palette repair. Other full suites are built but not
  fully rerun here. No AOT, JSON, packaging, commit, push or release changes occur.

## Native evidence ownership

The before default-output App SHA256 is
`1D1FB84D717C9364B8CE95D8414C1445EF733E44744A44FC77C013D2A2558AAB`.
The repaired default-output App SHA256 is
`C22DE2853829104723109774F69E5F24180787A144C78D11079D9366D5006525`.
Their launch manifests, independent data and audits are in `native/` and
`native-after/`; neither is substituted for the earlier E2A392 isolated replay.

- The before build passes actual 980x640 light context/inspector navigation and
  dark settings/provider/model-editor checks. Scrolling model B, Escape and
  reopening preserve its fully visible first label and fixed footer. Those two
  selected label/footer regions are pixel-identical before and after reopening.
- DPI changes between 96 and 120 are observed during this first replay, with
  recorded 1225x800 and 1920x1020 frames. A later screen capture is uniformly black
  and is rejected: only valid=false JSON is retained, not a comparison PNG.
  Inputs stop for fresh selection/observation; subsequent 980x640 PrintWindow
  pixels agree with the refreshed target state. The cause is not established and
  this does not certify a complete high-DPI sweep or a capture-helper fix.
- The repaired build independently reaches the real light/dark provider page at
  980x640, 96 DPI and ordinary permissions. Existing expanded rows update on the
  real theme command. Light deletion hover is exercised without clicking it.
- Equal-size, unscaled native crops show the light button's dominant RGB changes
  from 46,21,23 to 254,242,242. Model metadata pixels remain identical. The selected
  dark button region is pixel-identical across builds. The comparison image is
  `danger-button-native-comparison.png`; detailed regions are recorded in JSON.
- Both copies retain provider/model/proxy/project rows, six tasks, four terminal
  histories, original audits and three hash-verified artifacts. Each observer
  retains only the four historical requests; no new execution is observed.
- Both Apps restore Light and exit via their real close button with code zero.
  Both observers stop normally. All four QA/observer PIDs and port 51792 are absent
  at final cleanup. No forced termination or original-data mutation occurs.

## Remaining gates

This repairs a shared appearance defect and adds bounded current-build native
evidence. It does not prove all consumers' native states, whole-window Codex or
PI-Desktop fidelity, complete high-DPI/theme/size coverage, or product completion.
The complete product goal remains active.
