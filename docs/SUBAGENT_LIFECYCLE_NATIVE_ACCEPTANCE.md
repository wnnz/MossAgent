# Subagent native lifecycle acceptance

## Plan and design self-check

- The preceding proxy turn made verified progress. The product goal remains
  active; earlier subagent runs do not prove native shared-context toggling or
  stopping while a child stream is open.
- Use the existing App composition, settings commands, workspace run ownership,
  provider adapters, subagent tool pipeline and persistence. The only new runtime
  code is a repository-external QA host with isolated synthetic data.
- Two bounded loopback fixtures return the established Gemini parent and Responses
  child protocol. A cancellable child-frame gate permits observing live progress
  and network disconnect without modifying production timeouts or bypassing tools.
- The synthetic project alone is authorized. Its existing FullAccess fixture
  policy is seeded, never changed or approved through UI automation. No real
  configuration, credentials, public endpoint or daily browser is accessed.
- Each external handwritten C# file contains one type and stays within file/method
  limits. Observe only allowlisted parameters, booleans and task/tool states;
  never write full provider requests, headers, keys or arbitrary dialogue text.
- Verify actual sharing-on save/request, live child progress, sharing-off save,
  Stop/cancellation/persistence and a successful subsequent send without restarting
  the App. Reuse focused subagent/lifetime tests and build the full solution.
- Any observed defect requires a failing focused regression and a fix in its owning
  module. Screenshots use the existing repository-external passive Python helper.

## Verification

### Transport investigation, 2026-10-12

- The preceding sidebar turn made verified progress, including a successful
  3,638-case App run. The product goal remains active; that run is not native
  subagent lifecycle evidence.
- A held fixed-length response reproduces the missing partial text in two new
  workspace cases. A standalone `HttpClient` headers-only probe also blocks, even
  with one-byte reads. A bare TCP probe, without any MossAgent provider, agent or
  UI, receives the 92-byte complete response header but not the already-written
  body. `NoDelay` and coalescing header/body writes do not change that result.
- This places the observed withholding below MossAgent's SSE parser, not in its
  Markdown, dialogue projection or preview threshold. The specific transport or
  environment component responsible is not identified; neither runtime source
  assumptions nor this result prove a general .NET/Windows defect.
- Probe source snapshots and failing TRX remain outside the repository under
  `%TEMP%\MossAgent-subagent-lifecycle-recheck-20261012`. Temporary byte/TCP probes
  are not retained as failing production regression tests or silently counted as
  successes. No provider parser, execution timeout or system setting is changed.
- Actual indefinitely-held SSE now uses valid HTTP chunk framing in the external
  QA server. Ordinary completed fixture responses keep their fixed lengths. The
  repository lifetime regression also covers chunked and close-delimited child
  streams, each with live partial text, Stop, audit completion and optional restart.
  This does not establish fixed-length partial delivery on this environment.

### Native lifecycle, 2026-10-12

- The ordinary-permission QA host stays on PID 6716 for all three sends, with
  actual client geometry 1920 by 1032 DIP and scale 1 in the light theme. Native
  screenshots use the repository-external passive Python helper.
- Sharing-on is saved through the actual editor. The child receives the configured
  model, high reasoning effort, configured prompt and parent marker on both model
  turns, and the second request includes the authorized file-read result.
- Live partial text is visible after scrolling the child detail viewport. Actual
  collapse/re-expand preserves it. Releasing the held chunked response completes
  the child successfully; the parent reply alone is not used as success evidence.
- Sharing-off is saved without changing model/reasoning. The next two child
  requests omit the parent marker. While the response is held with partial text
  visible, actual Stop closes the child connection, sets the task to Cancelled and
  completes the tool audit with `tool_cancelled`; no task remains running.
- Read-only SQLite checks verify the cancelled partial reply is persisted, all
  dialogue IsPartial flags are false and its inner filesystem.read_file audit
  succeeds. No credentials or raw provider requests are exported.
- A third actual send succeeds without restarting. Aggregate parent/child requests
  are 5/6, child disconnects 1 and parent replies 2. All six child requests use the
  selected model, high effort and configured prompt; the latter four omit shared
  parent context. All six tool audits are complete; the cancelled child remains
  cancelled rather than being retroactively marked successful.
- The host exits through its normal close signal: QA_FINAL_REQUESTS=5,6 and
  QA_UI_EXIT=0. Evidence remains under
  `%TEMP%\MossAgent-subagent-lifecycle-native-20261012`, including sharing-off
  saved/live/cancelled/rerun images, persistence booleans and final assertions.

This verifies this synthetic native lifecycle, not every provider, theme, DPI or
the unresolved fixed-length partial-delivery behavior. The checkbox's wide blank
hit area and long composer wrapping were separate observed UI gaps; their later
reproductions and corrections are documented in INPUT_HIT_TARGET_ACCEPTANCE.

The subsequent fixed-length investigation is recorded in
`FIXED_LENGTH_STREAM_ACCEPTANCE.md`. Independent Python probes reproduce only
the plaintext HTTP withholding, while private pinned-TLS provider regressions
verify fixed-length partial delivery. This does not retroactively turn the
earlier failing plaintext probes into successes or add native TLS UI coverage.
