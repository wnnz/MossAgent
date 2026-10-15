# Project prompt responsive acceptance

The 2026-10-14 follow-up in `COMPACT_WELCOME_LAYOUT_ACCEPTANCE.md` supersedes the
three-row compact fallback described below. It preserves the original one-row
behavior while fitting saved empty conversations above the composer and accounting
for the brand offset inside the available height. This document's older results
remain evidence for the earlier assembly, not the follow-up build.

## Scope: 2026-10-10

`AppProjectPrompt` no longer forces three rows whenever its available width is
below 720 DIP. It measures its three styled children with unconstrained width and
compares their combined desired width with the actual available width. Content
that fits stays inline; longer content retains the existing stacked fallback.

This removes a redundant viewport assumption, not controls or functionality.
Font size, label bounds, project-name ellipsis, alignment, theme, menu appearance
and all shared styles remain unchanged. No allocation or latency improvement is
claimed; this is a content-driven layout correction.

## Design and safety self-check

1. App owns this synchronous shared-control layout. No module dependency changes.
2. Existing project commands, bindings and flyout remain the integration points.
   Reflow changes Grid definitions only when the layout mode changes; it does not
   recreate the project button, text controls, command or flyout.
3. No file, process, provider, browser or tool execution enters the component.
   Authorization, approval, task cancellation, timeouts, output limiting and
   persistence remain in their existing paths. Missing commands disable the
   action; command CanExecute remains authoritative.
4. The production file has 92 lines and one type; its longest method has 25 lines.
   The new test file has 153 lines and one type; its longest method has 32 lines.
   No extra native component, global cache, package or persistence migration is
   introduced. Debug stays JIT; publishing stays Windows x64 single-file, no AOT.
5. Ten new cases cover styled short headings at 600/664/980 DIP in both themes,
   live short/long-name changes, narrow/wide transitions, retained button identity,
   Tab/Space activation and disabled/missing-command rejection. Existing prompt
   tests retain long-name, flyout ownership and tooltip checks; real chat-view
   tests retain narrow-viewport bounds checks.

## Regression evidence

- Before the fix, the ten-case run fails six cases on inline-layout assertions;
  four pass. Failures cover the 600/664 DIP short headings and live-name cases.
- After the fix, all 17 focused prompt and empty-session-heading cases pass.
- Focused whitespace verification and the two-file Roslyn type/size audit pass.
- The solution build succeeds with zero warnings and errors.
- The subsequent full run passes 4308/4308 cases across eight assemblies, with
  zero failed or skipped cases. Repository-default `git diff --check` passes;
  the index remains empty and pre-existing worktree changes remain uncommitted.

## Native evidence

The external QA host uses the real App/MainWindow with new synthetic data and a
loopback-only synthetic provider. It sends no model prompt and never opens the
user database. All three hosts report `QA_ELEVATED: False` and close normally with
window/lifetime exit messages. Passive Python captures preserve native pixels.

- At 980x640, the short draft heading is inline in dark and light themes after
  an actual sidebar new-conversation click. The dark before capture from the
  preceding acceptance run is also 980x640, with the same heading code/styles;
  it shows the unnecessary three rows.
- The unscaled crop `(380,265)-(900,375)` changes from three foreground row bands
  to one. The comparison excludes the icon above the heading and does not resize
  either image. This measures heading reflow, not Codex pixel equivalence.
- Clicking the inline project name opens the existing picker; selecting a
  synthetic project updates both the heading and composer project binding.
- Native light-theme maximize/restore changes 980x640 to 1920x1032 and back;
  the short heading remains inline.
- An 80-character project name stays stacked and ellipsized at 980x640, becomes
  inline at 1920x1032, and returns to stacked after native restore. The retained
  stacked project action still opens the picker; choosing a short project name
  restores inline layout without a window resize.
- Read-only post-exit checks of all three synthetic databases confirm nine tasks,
  one pinned task, zero archived tasks and zero messages. No QA host remains.

Logs, TRX, QA source, Python scripts, raw captures and the unscaled comparison
stay outside Git at
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-prompt-fit-20261010`.

This finishes only this shared-control slice. Whole-page Codex fidelity, other
DPI settings and live provider/tool workflows are not accepted by this change.
No staging, commit, push or publish occurs; the overall product goal stays active.
