# Window caption fidelity and native evidence (2026-10-13)

This is a bounded caption-style correction and an evidence audit, not complete
Codex visual equivalence. The full-product objective remains active.

## Design and safety self-check

1. App owns caption presentation. Existing shared `AppIconButton` and style
   variants remain authoritative; no module dependency or native port is added.
2. Existing minimize, maximize/restore and close commands are unchanged. Only
   close-icon size and the existing shared close-hover color are corrected.
3. Authorization, approval, cancellation, persistence and output limits are
   unchanged. Native replay does not send messages or enable schedules. Python
   performs passive capture and read-only metadata checks, without activating,
   moving, resizing or sending input to either reference or target windows.
4. App.axaml remains 496 physical lines, WindowVariants.axaml 58 and the new
   single-type regression file 74. No per-view appearance override is added.
5. Four headless cases cover both themes at 980x640 and 1280x820, real headless
   pointer enter/leave, white hover glyph, Arrow cursor and unchanged hit areas.
   Native pixels remain a separate acceptance gate.

## Reference and correction

The preceding native comparison uses equal, unscaled 1920x1032 images in
`%TEMP%\MossAgent-window-state-20261013-080834`. Its
`native-region-comparison.json` records matching sampled titlebar/navigation-rail,
sidebar and workspace colors. Minimize ink is 10x1 and restore ink 10x10 in both
images. Close ink is 10x10 in Codex but 8x8 in that MossAgent binary. Different
history-button states and conversation contents do not establish pixel equality.

The passive Codex hover sample in
`%TEMP%\MossAgent-caption-fidelity-20261013-085556\codex-passive-current.png`
contains the close-hover background RGB 232,17,35 (`#E81123`).

- WindowVariants gives only the window close icon Size=10; the other caption
  icons retain Size=8 and StrokeThickness=1. Button hit areas remain 46x36.
- The existing App.axaml close-hover rule now uses `#E81123`, replacing
  `#C42B1B`. White glyph and Arrow cursor behavior are preserved.
- An attempted additional rule in WindowVariants was overridden by the later
  existing shared rule. That ineffective rule is removed; include ordering and
  unrelated styles are unchanged.

## Deterministic verification

- `baseline/caption-baseline.trx`: all four cases reproduce Size=8 instead of 10.
- `hover-baseline/caption-hover-baseline.trx`: all four reproduce the old hover
  color. The intermediate `focused-final/` run retains these four failures and
  is not a successful final result despite its directory name.
- `green/caption-green.trx`: final caption/window group passes 44/44.
- Final solution build: zero warnings and errors. Completed `full/*.trx` files
  pass 5,655/5,655 across eight assemblies, with no failed or skipped cases:
  App 4,105; visual 12; Agent 119; Tools 491; Providers 516; Infrastructure 199;
  Browser 76; Platform.Windows 137.

Logs and TRX files are in the external caption-fidelity directory above. No
commit, push, release, AOT, JSON or packaging change is part of this slice.

## Earlier native window-state sweep

The preceding production-App sweep belongs to assembly SHA256
`319A951FBB3562556F23F378B0EB6648059445D29B914560D107217D0E5D4FEC`,
not the caption-corrected assembly. Ordinary-permission, DPI-96 captures and
read-only bounds records in the window-state directory verify:

- Light/dark 980x640 and dark 1280x820 restoration; maximized 1920x1032 inside
  the monitor work area. Cold reopen retains 1280x820 normal bounds while maximized.
- Dark inspector and automation/history surfaces at narrow and maximized sizes;
  completed history opens without a model request. Minimize retains 980x640
  normal bounds and restoring returns to 980x640.
- Persisted replay retains the paused plan, four terminal runs, six tasks,
  original 15 tool audits and three hash-verified artifacts. Recorded requests
  remain the four historical whitelisted loopback requests.
- The earlier PID 3156 disappears after the actual caption close action. Its
  exit code was not observed; this is not evidence of exit code zero.

## Caption-build checkpoint: native gate remained open

The production Debug/JIT assembly SHA256 at this checkpoint is
`38B1E1992B6280581F6C7634A69DFC1AAC1FCC17436E6B93C66A3733601A2074`.
PID 7884 starts with ordinary privileges and the isolated window-state data.
Cold-start and after-observation audits pass all checks, including no descendant
processes, unchanged tasks/audits/artifacts and no additional model request.
That read-only restoration is not a new automation execution.

Native capture cannot yet validate the corrected pixels:

- Sky returns MossAgent metadata and its accessibility tree, but the displayed
  screenshot shows Codex. A fresh list/get-window retry reproduces the mismatch.
  No coordinate or element input is issued after the mismatch is identified.
- Passive PrintWindow reports 1920x1020 bounds, but the non-black content covers
  only 1536x816; the remaining right and bottom strips are black. The independent
  read-only bounds sample reports a 1920x1032 work area and 1536x816 normal bounds.
  These observations do not establish whether capture, native DPI or startup
  window state owns the discrepancy.
- Explicit screenshot-process/thread DPI initialization does not remove the
  padding. The external passive script now rejects such frames instead of
  accepting them solely because luminance variance is nonzero. The rejected
  sample writes diagnostic JSON with `valid=false`, not an accepted PNG.
- The optional `--method screen` requires the selected window already be in
  foreground and checks ownership/bounds again afterwards; it performs no input
  or activation. A manual foreground handoff is requested for further diagnosis.
  The non-foreground negative case rejects capture without saving a PNG; a fresh
  passive Codex PrintWindow sample passes full 1920x1032 content-extent validation.

`caption-final-readonly.automation-audit.json` in the window-state directory
passes all restoration checks again after this capture diagnosis. The QA App is
still running for the manual foreground handoff at this checkpoint; current-build
normal close/exit and observer shutdown have not been verified. The loopback
observer has its existing 20-minute lifetime bound, not a recurring automation.

No resized/cropped padded image is used to disguise native dimension inequality.
Current native close-hover pixels, minimize/restore, both-theme size matrix,
inspector/context-card interaction, history navigation and normal shutdown still
need reliable current-version observation. Headless success and the earlier
version's native matrix must not be promoted to current-version visual sign-off.

The later sidebar-glyph correction and fresh isolated production replay are in
`WINDOW_SIDEBAR_ICON_ACCEPTANCE.md`. That separately identified build obtains valid
native frames, actual hover/inspector/minimize/history behavior and normal exit.
Both checkpoint QA instances and observers are then cleaned up with preserved
data audits. The failed frames above remain failures, not retroactive successes.
