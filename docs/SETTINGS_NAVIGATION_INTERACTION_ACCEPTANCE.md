# Settings navigation interaction acceptance

## Scope and design checks

This slice follows `SETTINGS_SHELL_ALIGNMENT_PROGRESS.md`. It changes
product UI/behavior and obtains deterministic evidence; it is progress,
not a no-progress or wait turn. The full product objective remains active.

1. App's MainWindowViewModel owns top-level page navigation. Reuse the catalog's
   explicit ConversationRequested event instead of wrapping every task/history
   command or changing Domain/Application contracts.
2. Existing task commands retain lookup/locked-state validation. Search retains
   the existing matching rules and only exposes group visibility to the view.
3. No new tool, model call, approval policy, authorized root or persistence schema
   is introduced. Tests use independent storage and deterministic loopback
   providers; keyboard tests must prove settings does not submit the hidden draft.
4. Keep one type per C# file; split new interaction tests from existing shell
   geometry tests. MainWindowViewModel must stay below 500 physical lines.
5. Prove successful shortcut/menu/history navigation, rejected/null/foreign/locked
   targets, disposed subscription cleanup, background updates without page theft,
   real search bindings and unsaved editor/conversation state preservation.

## Implemented behavior

- Ctrl+N, the File menu's new-task action, explicit task selection and history
  navigation now leave settings through the existing successful-navigation event.
  MainWindowViewModel removes its subscription on disposal. Locked, foreign and
  null targets retain their existing rejection behavior; rename/reload does not
  take the user out of an unsaved provider editor.
- Search hides headings whose groups contain no matching navigation item. A
  query does not select another content page or clear drafts. Clearing it restores
  seven navigation items and three groups; keyboard activation still uses the
  existing page-selection command.
- Ctrl+Enter from the settings search retains its existing isolated behavior:
  it does not send the hidden composer. No unnecessary global submission guard,
  persistence migration or tool-policy change is added.

## Deterministic verification

The new nineteen-case baseline fails ten and passes nine: eight successful
navigation cases and two empty-group searches expose the missing behavior.
The finalized focused settings/navigation group passes 58/58 without skips.
An initial keyboard fixture did not wait for restored model selection; that
fixture is corrected before this baseline, not presented as a submission bug.

The first complete serial App run passes 4,212 of 4,218 cases and fails six
existing workspace-frame assertions. Their isolated recheck fails the same six:
they still require the outer shadow frame to clip itself, contrary to the
documented `WINDOW_FRAME_SHADOW_ACCEPTANCE.md` separation. The assertions now
require a non-clipping outer frame and a clipping inner AppBorder with equal
global bounds/radius. Product frame rendering is not changed or reverted.
All seventeen ConversationHeadingLayoutTests then pass. The corrected test file
remains 250 physical lines; MainWindowViewModel remains 466 and SettingsOverlay
128. Both new test files contain one type and remain below 250 lines.

The latest isolated App output is Debug/JIT
`.scratch/settings-interactions-frame-20261013/bin/MossAgent.App/debug_win-x64`,
SHA256 `1D35374501158D3B03DF81A9E47963BE4B0C29C14091F6AB746004A49BC8BFBC`.
Its solution build has zero warnings/errors; all 48 pixel cases and scoped
whitespace checks pass. Ten shell cases also export fresh compositor frames to
`%TEMP%\MossAgent-settings-interactions-rendered-20261013` using actual Headless
render-scaling notifications, not resized screenshots. External Python produces
an unscaled shell crop and four matching light-color samples against the retained
native Codex General reference. It explicitly rejects native/whole-window
equivalence: the Moss frame is Headless Appearance and the reference is General.
The Python helper and image artifacts remain outside the repository.

The corrected final serial App run passes all 4,218 cases. Together with the
final 48-case pixel run and the six other complete assemblies (Agent 119,
Tools 491, Platform.Windows 137, Browser 76, Infrastructure 199, Providers 516),
the final eight-assembly results pass 5,804/5,804 with zero failures/skips.
Seven architecture cases are included in Agent's 119, not counted twice.
The earlier six-failure App TRX and isolated red recheck remain retained.
App/pixel final replays use `settings-interactions-frame-20261013`; the other
six use `settings-interactions-20261013` against the same checkout, without
promoting either output directory to native production evidence. A final
solution build and scoped whitespace/diff verification also pass. The recorded
latest App assembly remains unchanged during the final App/pixel runs and is
not substituted for the default executable or launched as a native preview.

## Native observation limit at the original checkpoint

The old preview's launch monitor is still live in session79013. A new passive
PrintWindow attempt again reports 1920x1020 native bounds but only 1536x816 content
and black padding, so the capture is rejected. This is not a settings comparison
or a native renderer root-cause conclusion. No duplicate preview is started and
no native input uses the unsafe prior window binding.

A fresh window-list/get-window/state check again supplies Codex pixels for the
MossAgent window object, so no input follows it. `before/interaction-gate.json`
records the rejected passive frame; no PNG is accepted. `codex-current.png/json`
is a valid 1920x1032, 96-DPI passive capture, but shows the workspace rather than
Appearance. It is not silently substituted for a settings reference.

The old preview still owns its independent data directory and old App SHA256
`6E10E54B78BE6C11D036ED5D5B8B39EE6FF920AD15FA6A062B05141F2D8C2CD3`.
Read-only comparison again verifies all nineteen table schemas/rows, SQLite
integrity, unchanged UI settings, three original artifact hashes/audits and the
four-request history. The legacy automation audit retains its expected false
fresh-cancellation flag because no cancellation is executed. Its bounded local
observer is no longer listening; neither its original PID nor port 51792 is
present. The still-open App is not reported as a successful shutdown or a new
scheduler replay. The user is asked to close only that old MossAgent preview
before a trustworthy new-version replay is attempted.

Normal-size/maximized light/dark native interactions, equivalent Codex Appearance
and PI-Desktop references, restore clipping and the full UI/state/DPI matrix
remain required. Deterministic interaction tests do not replace those gates.
The default executable, JIT/single-file packaging, JSON, AOT and execution policies
remain unchanged. No commit, push or release occurs.

`NATIVE_SETTINGS_INTERACTION_PROGRESS.md` supersedes the open-preview/binding
status above: ownership and actual foreground pixels are reestablished, the
old App closes normally, and the recorded candidate receives a fresh independent
native search/navigation/draft/theme/size replay without model requests. This
does not repair the older resize defect or supply an equivalent Appearance
reference. It also records a later shared-title correction found in native crops.
