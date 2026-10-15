# Subagent dialogue projection optimization

## Design self-check

- App owns presentation through the existing ChatMessageViewModel and reusable
  SubagentDialogueView. Remove the derived row getter from the raw tool payload
  instead of putting mutable cache fields into a record copied with `with`.
- Each message lazily projects the latest published raw dialogue snapshot. Warm
  reads return the same rows. Only changed role/content rows are recreated;
  retained answers keep their parsed Markdown blocks. Rows parse answers only
  when Blocks is read; literal tool output is still never parsed as Markdown.
- New snapshots do not mutate earlier rows or arrays. Metadata-only updates do
  not notify the row binding. Equal rows may retain presentation when IsPartial
  changes because that flag is raw execution metadata, not row presentation.
- No cross-message/task or static cache, new port, filesystem/network operation,
  approval, timeout, output-budget, audit, persistence or JSON-shape change.
  Standard JIT debug and Windows x64 single-file publication remain unchanged.
- The three production C# files each retain one type and stay below 250 lines;
  new methods remain below 40 lines. Existing shared controls, styles, dimensions
  and icon geometry are unchanged.
- Tests cover retained and changed rows, immutable snapshots, independent messages,
  current-value notifications, null/empty histories, role changes, partial flags,
  81 duplicate/reorder/grow/shrink transitions, rendering, serialization and the
  existing cancellation/reopen lifecycle regressions.

## Evidence

- Four new regressions fail against the original getter: warm reads and metadata
  updates rebuild rows, and progress reparses unchanged answers. With one answer
  containing a heading and C# code block, 1,000 warmed reads allocate 1,624,000
  bytes in the original implementation and zero after this refactor.
- The refactored implementation passes 192 focused cases, including bound-view
  updates, light/dark narrow/wide rendering, raw JSON/legacy loading and live
  chunked/close-delimited child Stop and optional reopen. The bound view now
  inherits its message context instead of rebinding to each raw tool payload.
- Measurement is Windows x64 .NET 10 Debug/JIT with current-thread allocation
  counters after 100 warm-up reads. It measures repeated property reads, not
  complete application memory, rendering cost or general latency.
- TRX, baseline source snapshots and logs remain outside Git under
  `%TEMP%\MossAgent-dialogue-projection-20261012`.

## Final verification

- Complete App regressions: 3,652/3,652 passed, with no failures or skips.
- Serial solution build after the test host exits: zero warnings/errors.
- Architecture constraints: 7/7 passed. Post-build projection/bound-view replay:
  13/13 passed. Other complete test assemblies were not rerun in this slice.
- Scoped whitespace verification and repository-wide git diff --check pass. One
  initial formatting run identifies nested-loop indentation in the new test;
  it is corrected with apply_patch before the successful final verification.
- The 13-file Roslyn audit passes, with exactly one type per handwritten file,
  maximum 185 lines and maximum 48-line test methods. Changed production methods
  are at most 24 lines; WorkspaceChatView markup is 170 lines. PowerShell's first
  Roslyn load conflicts with its loaded assembly, so the audit uses an isolated
  repository-external .NET 10 executable, not a heuristic type counter.
- Unobserved construction of an answer row allocates 40 bytes in the focused
  measurement; its 1,000-heading answer is parsed on the first Blocks read only.

## Post-refactor native smoke

- Rebuild the external host with the current App only after test-host exit.
  It runs without elevation on PID 5104, in a new synthetic data/project root.
  Actual stable client geometry is 1280 by 820 DIP, scale 1.25, light theme.
- Actual input/send invokes Gemini parent and Responses child with the configured
  model, high reasoning, prompt and sharing disabled. Held chunked progress shows
  the partial answer in the real WorkspaceChatView after scrolling its detail pane.
- Releasing the response updates the same native dialogue to the complete answer.
  The tool succeeds with no error, parent/child requests are 2/2, child disconnects
  zero, parent replies one and both child/inner-file tool audits finish successfully.
- Passive Python screenshots are retained in the external evidence root's native
  directory: live.png, completed.png and completed-answer.png. The Python image
  shows clear rendered text at physical 1600 by 1025 pixels; the Sky preview at
  this DPI loses some glyph pixels and is not used to infer a production font bug.
- Normal close confirms QA_FINAL_REQUESTS=2,2 and QA_UI_EXIT=0. The broader sharing
  toggle, Stop, persistence and same-process recovery evidence is documented in
  SUBAGENT_LIFECYCLE_NATIVE_ACCEPTANCE and predates the projection refactor.
  This smoke proves the new binding's native live/completed update, not every
  provider, dark theme, DPI, checkbox hit area or long composer wrapping case.

This completes only a presentation optimization slice, not full-product visual
or functional acceptance.
