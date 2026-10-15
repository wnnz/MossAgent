# Navigation rail component acceptance

## Scope and design self-check

- App owns `AppNavigationRail`, its resource labels and shared styles. MainWindow
  supplies existing workspace/main commands and keyboard-help content. No core,
  provider, tool, OS, persistence or dependency-direction change is introduced.
- Existing NewTask, Inspector, ToggleInspector and ToggleSettings commands remain
  authoritative. The rail does not call storage, tools or providers directly.
  Timed tasks remain explicitly disabled; audit/help are MossAgent capabilities,
  not an implementation of Codex's notification/account or automation services.
- Actions are local navigation only. Existing commands retain their approval,
  cancellation and execution behavior; this component adds no tool permission,
  filesystem access, process launch, network request or unbounded output.
- Component markup/code, shared variants and focused tests stay within 500 lines,
  each handwritten C# file has one type, and methods remain within 80 lines.
  The analyzer's geometry predicate is extracted without changing its rules.
- Focused tests cover measured geometry in both themes, actual transforms,
  workspace command routing, unchanged drafts/context and no provider requests.
  Negative cases cover missing commands/help, CanExecute rejection, owner
  replacement, hidden/disabled ancestors, detachment and forbidden composition.

## Appearance ownership

- The rail is 52 DIP wide, with 36-DIP hit areas, 10-DIP corner radii, an 8-DIP
  body inset and 8-DIP inter-button spacing. Settings is centered 26 DIP above
  the window's client bottom rather than the previous 30-DIP inset.
- Light home selection uses its own `#E2E9ED` brush; dark uses `#2C333A`.
  This does not change the global settings selection palette.
- Home uses a filled 17-DIP icon and a shared optical translation of (-1,-0.5).
  Its visual center is (25.5,70), with the button center at (26,70). Settings uses
  a 15-DIP icon translated by (+0.5,+0.5); other rail icons retain the common size.
  These are optical corrections from native pixels, not per-view icon overrides.
- `ValidateSharedNavigationRail` and `MOSSUI002` reject Content, Padding and
  geometry replacements, including native-owner property forms. HelpContent and
  semantic command bindings remain supported.
- Explicit TranslateTransform is accepted only as a non-Control value object,
  alongside RotateTransform. Reflection and negative markup tests retain the
  rejection of Button, TextBox, Path, Border, ScrollViewer and other native visuals.
  Business RenderTransform overrides remain rejected.

## Transform regression

The initial rail's render-transform setters compiled but the actual home icon had
an identity TransformGroup. Replacing the setter's text with a TranslateTransform
object still reproduced the same runtime failure. The broad task-running animation
was installing transform state on unrelated AppIcons during style initialization.
Avalonia 12.1.2 TransformAnimator initializes RenderTransform before observing the
conditional match, so a conditional global animation selector is not isolation.

The animation now belongs to SidebarActivityIconTheme, applied only to the task's
activity icon. Home alignment and the existing running-marker cases pass together.
Four theme/state regression cases also prove that ordinary icons retain their null
or styled translation when an activity marker becomes running or stops. Rotation
is required while running; initially hidden markers may initialize their theme
lazily. Binding jobs are drained before asserting the real visual state.

## Lifecycle and functional boundaries

- Missing commands or HelpContent disable the corresponding entries. More is
  disabled when both of its commands are missing; command CanExecute is preserved.
- More actions close their own menu. Replacement of a command, help content or
  DataContext, hide/disable, hidden/disabled ancestors and detach close both menus.
  Ancestor subscriptions are removed on detach.
- Help content inherits its business DataContext. No unauthenticated help-page
  navigation, model transmission, automation execution or paid API call is added.
- Ordinary JIT debugging and Windows x64 single-file release behavior remain
  unchanged; there is no AOT or JSON source-generation change.

## Validation status

Focused visual, navigation, lifecycle, negative-markup and drawing selection:
116 passed, zero failed/skipped. Full build, final native captures and serial
solution regression are recorded below. Compilation alone is not
a claim of pixel-identical UI or of the complete product goal being achieved.

## Native pixel comparison, 2026-10-08

Python captured the actual ordinary-permission App/MainWindow with isolated
synthetic SQLite data at 96 DPI. Codex was captured passively; only its (8,8)
outer frame offset was cropped. Neither images nor extracted regions were resized.
Helpers, screenshots, synthetic data and the comparison script remain outside Git
in `%TEMP%/MossAgent-sidebar-20261007/rail-pixels-final-20261008`.

| Measurement | Codex | Previous MossAgent | Final MossAgent |
| --- | --- | --- | --- |
| Home ink x/y bounds | 17..33 / 62..77 | 19..32 / 63..76 | 17..33 / 62..77 |
| Home ink dimensions | 17x16 | 14x14 | 17x16 |
| Clock ink dimensions | 16x16 | 16x16 | 16x16 |
| Settings ink dimensions | 16x16 | 14x16 | 16x16 |
| Settings center from client bottom | 26 DIP | 30 DIP | 26 DIP |
| Home selection RGB | 226,233,237 | 236,239,241 | 226,233,237 |

The final light capture is 1280x820; the dark narrow capture is 980x640, bounded
by the real window's minimum height. Footer actions stay inside the rail and the
sidebar retains its own scroll viewport. Dark geometry is validated, but there is
no corresponding native Codex dark-state reference to claim pixel equality.

Native light navigation exercised help/Escape, More's inspector action, audit,
settings/return and New Conversation. A synthetic unsent draft and project context
survived inspector/audit/settings; New Conversation opened a new conversation slot
without transmitting the draft. Send remained disabled with no model selected.
The earlier QA process closed normally with exit code zero before its final host
was rebuilt, avoiding its DLL lock. No user database was seeded or migrated.

These measurements cover the rail, not every sidebar label, icon outline, hover
state, typography or function. Audit/help remain MossAgent-specific, timed tasks
remain unavailable, and the entire product goal is still active.

## Final regression

- Validation-directory solution build passed with zero warnings/errors. The final
  QA host rebuilt successfully after the previous process released its DLL lock.
- After the analyzer's size-preserving refactor, one complete serial solution
  invocation passed 2779 tests with zero failures/skips: App 2115, Agent 58,
  Browser 76, Infrastructure 92, Platform 50, Providers 241 and Tools 147.
  TRX evidence is in `rail-validation-current/confirmed-current` under the
  temporary evidence directory. No flaky test was edited or excluded.
- Roslyn audited all twelve related handwritten C# files: one declaration per
  file and zero hard file/method-limit violations. Shared markup files remain
  under 500 lines; focused Git whitespace checks passed.
- Final dark help opened and Escape closed it; Python captured its actual popup.
  The desktop helper initially lacked input geometry for the new QA window;
  rebinding and refreshing geometry allowed the single retry to succeed. Its
  cached screenshot pixels were not used as appearance evidence. Both QA hosts
  subsequently shut down normally with exit code zero.
- The actual idle user program, verified to have no tasks and an empty unsent
  draft, closed normally without force termination. The ordinary Debug solution
  build passed with zero warnings/errors, and the same executable restarted with
  a non-elevated process token. Its actual 1920x1032 Python capture is non-black:
  home ink is x17..33/y62..77, settings is x18..33/y998..1013, and home selection
  is RGB (226,233,237), matching the measured light reference at the same size.
  Fresh accessibility retained the same MossAgent project and branch, empty
  composer with input focus, unselected model, AskEveryTime policy and disabled
  Send action. No task or synthetic project was added to the user's workspace.
- No publish, staging, commit or push is performed. Capture helpers and synthetic
  data stay outside the repository. Actual timed automation, Codex dark/hover
  parity, real provider runs and the complete product goal remain unverified.
