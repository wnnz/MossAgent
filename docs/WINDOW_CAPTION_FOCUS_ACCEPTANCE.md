# Window caption focus acceptance

## Reproduction and repair

Native production App testing at ordinary privileges, DPI 96, first verifies
1280x820 and 980x640 workspace layout, the light/dark appearance page, model
expansion, model editor scrolling and maximized/minimum-size transitions.
The earlier content-clipping problem is not reproduced in this replay; no
repair or root cause for that earlier instance is claimed.

A different real failure is reproduced: after clicking maximize and restore
while the model editor is open, focus belongs to MaximizeButton. Escape leaves
the model editor open. AppSettingsDialog handles unhandled Escape only on its
own routed input path; the sibling caption button takes input outside that path.

The shared WindowControlButtonTheme now sets Focusable=False. All three caption
actions keep native-titlebar semantics: pointer actions work without taking an
editor's keyboard focus, and they do not become Tab stops. Their accessible
labels, commands, CanExecute checks, sizes, cursor, icons and hover rules remain
unchanged. Dialog Escape/child handling is not replaced by a window-global
shortcut, and no focus trap, service locator or new subscription is introduced.

## Design self-check

1. App's existing shared caption theme owns the behavior; dependency direction
   remains unchanged.
2. Existing AppIconButton commands and AppSettingsDialog routing are sufficient;
   no new port or per-view workaround is required.
3. No provider, tool, authorization, approval, cancellation, timeout or output
   policy changes. The native sweep uses an independent database backup and
   never submits a message, starts a browser/terminal or changes approval policy.
4. The shared resource dictionary remains below 300 lines. The new 107-line test
   file has one type; methods/lambdas remain below the hard size limit.
5. Successful pointer commands, disabled commands, field focus/draft/selection,
   Escape restoration and production caption keyboard-stop rejection are tested.

## Test and build evidence

Evidence is under
`%TEMP%\MossAgent-native-resize-20261013-115953`.

- The new sixteen-case baseline passes six disabled-command preservation cases
  and fails ten: six enabled pointer-focus cases and four actual MainWindow
  keyboard-stop cases. This is an observed before/after regression, not a
  test-fixture scheduling error.
- After repair the related caption/icon/dialog/focus/scroll group passes 54/54,
  including all sixteen new cases and existing handled-Escape/disabled-command
  checks. The complete serial App suite passes 4,174/4,174 without failures/skips.
- The final eight-assembly regression passes 5,736/5,736 without failures/skips:
  App 4,174, visual 24, Agent 119, Browser 76, Infrastructure 199,
  Platform.Windows 137, Providers 516 and Tools 491. All eight final TRX files are
  retained separately from the failing baseline.
- Solution build passes with zero warnings/errors. Scoped whitespace formatting,
  shared-theme XML, one-type/file-size and diff whitespace checks pass. The final
  App assembly still matches the repaired native replay hash.

## Native evidence ownership

Baseline App SHA256:
`D384764AC1C1A3AC60011EC51099249DE3B6B2FDBB6412F14541E523BD0E539B`.
Repaired App SHA256:
`7CFC5F00FBF9A30413BF670D940DAC80FB09486931A610BE0292C6DFBFDCCF6B`.
The repaired App uses ignored `.scratch/caption-focus-20261013` Debug/JIT output;
default output is not silently replaced. Repaired frames/audits are in `fixed/`.

- Both light and dark native model editors open at 980x640, maximize to
  1920x1032 and restore to 980x640 without disappearing or clipping the footer.
  Fresh UIA observations confirm Model ID retains focus; Escape now closes the
  editor. The same model/catalog expansion remains available afterwards.
- The actual repaired close caption button exits the App zero. Both baseline
  and repaired observers exit zero, their original App/observer instances are
  gone, and port 51792 has no listener. Repaired UI preference returns to Light.
- Read-only audits before/after closing prove all nineteen database table schemas
  and rows match the source backup: six tasks, 26 messages, four terminal schedule
  runs, catalogs, saved configuration and audits are unchanged. Three original
  artifacts pass hashes. Only four historical model requests exist; the log hash
  is unchanged and no App descendant process is observed.
- Passive same-state baseline/repaired 1920x1032 cold light frames are exactly
  pixel-identical. This proves unchanged appearance for that one fixture state,
  not full state/DPI equivalence. `caption-focus-before-after.png` places two
  unscaled 980x640 dark frames side by side: baseline Escape leaves the modal
  open; repaired Escape closes it. Python scripts stay outside the repository.

## Codex comparison and remaining gates

Fresh passive Codex and repaired MossAgent frames are both 1920x1032, DPI 96 and
ordinary privilege. Sampled titlebar, sidebar, main and selected-row colors match.
The rail sample at (40,400) differs by one RGB unit: Codex (237,243,248) versus
MossAgent (238,244,249). This may involve the adjacent frame shadow; no flat
background correction is applied from a single sample without verifying the
surrounding gradient. Comparison points and actual results are retained in
`caption-comparison.json`; no full-window pixel-equivalence claim is made.

Codex remains on its conversation page rather than a matching settings state.
The current native check does not cover all seven repaired settings pages,
all inspector tabs, higher DPI or PI-Desktop equivalence. Previous evidence
belongs to its recorded version; it is not promoted to this repaired assembly.
No AOT, JSON, packaging, commit, push or release change occurs. The full product
objective and complete visual/control/reference acceptance remain active.
