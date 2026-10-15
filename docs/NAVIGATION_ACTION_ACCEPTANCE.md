# Navigation action states

## Shared ownership

Business pages continue to use `MossAgent.App.Controls`; this change does not
introduce another wrapper library or allow native visual controls. Existing
`MOSSUI001`, `MOSSUI002` and AXAML build gates remain mandatory. Structural
layout primitives are still allowed, but not page-local appearance overrides.

`NavigationActionVariants.axaml`, included by the shared surface styles, owns
task options, project collapse controls and project-section actions.
`ConversationTabVariants.axaml` owns the matching tab-option states. Existing
commands, parameters, selection and archive semantics are unchanged.

| State | Expected behavior |
| --- | --- |
| Hidden | Zero opacity and no pointer interception; keyboard navigation remains available |
| Row hover | Action is visible and can receive pointer input |
| Row keyboard focus | Action is visible without selecting the row |
| Focus and hover leave | Action is hidden unless its menu is open or its tab is selected |
| Selected tab | Options remain visible |
| Disabled command | Reveal does not enable or execute the command |
| Open action menu | Anchor remains visible after focus moves into the popup or outside the row |
| Menu action | Existing command executes once and shared flyout dismisses |
| Escape | Menu dismisses without invoking its action |

## Regression evidence

`NavigationActionStateTests` uses an isolated Headless dispatcher, real App
styles and shared controls in light/dark themes. Its 36 cases cover actual
pointer hit testing, hover, focus, selection, disabled commands, menu dismissal
and keyboard project toggling; they are not just XML selector assertions.

Before the shared-style changes, 20 of the initial 28 cases failed. After the
fixes and additional Escape/toggle coverage, all 36 passed. The focused
navigation/component subset passed 62 tests.

Headless results establish component behavior, not full-product pixel equality
or native DPI coverage. Native screenshots and interactions require separate
acceptance evidence.

The full serial solution run passed 1,561 tests, including 1,177 App tests.
The solution build completed with zero warnings/errors; scoped whitespace
format verification and one-type/file/method-size checks also passed.

## Native acceptance on 2026-10-06

An ordinary-permission Windows process loaded a separate synthetic data copy
and the production App/MainWindow, in dark theme at 1280×820. Supported native
input verified project collapse/expand with Tab and Space, switching to a
second conversation, keyboard focus on an unselected tab without selecting it,
opening its options with Space, Escape dismissal without closing the tab, and
restoring hidden options after focus moved into the composer. The menu anchor
remained visible while the popup was open and the pointer stayed elsewhere.

The existing external Python helper captured the confirmed process with
passive PrintWindow, validating exact bounds and rejecting uniform/black frames.
Screenshots and QA files remain outside the repository:

`C:\Users\wzheng\AppData\Local\Temp\MossAgent-navigation-action-qa-20261006-102753`

`tab-action-states.png` contains four same-size, unstretched crops in this order:
idle, unselected-tab keyboard focus, open menu anchor, focus left the tab.
Main-window PrintWindow evidence shows the anchor; popup visibility was also
checked through the native UI snapshot. No message was sent, no task was
archived, and the real application data was not modified.

This native pass does not cover light theme, alternate DPI, all window sizes,
every menu, or one-to-one comparison against Codex. Those remain separate
broader UI acceptance requirements.

## Engineering self-check

- App owns presentation; the module dependency direction is unchanged.
- Existing shared controls and commands suffice; no new port or tool is needed.
- No authorization, approval, timeout, cancellation, persistence or output
  policy changes are introduced.
- Shared styles are split instead of growing the near-limit surface stylesheet;
  handwritten files retain one type and stay below the engineering hard limits.
- Negative cases prove hidden pointer rejection and disabled-command refusal,
  with focus, menu and theme behavior exercised using real styles.
