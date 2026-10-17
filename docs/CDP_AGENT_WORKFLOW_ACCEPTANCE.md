# CDP settings-to-Agent conversation acceptance

## Scope and design

This closes the previous CDP slice's direct-tool-only evidence gap. A test-owned
headless Chrome and loopback scripted Responses service exercise production App
composition, real Agent orchestration, the provider adapter, task session manager,
tool execution/audit pipeline, SQLite and rendered conversation controls.

The driver and its synthetic data stay outside the repository. No production
implementation changes are required by this acceptance slice. Browser owns page
lifetimes; App owns conversation presentation and commands; the existing ports
remain unchanged. No authorization, approval, timeout, persistence schema,
publication target, JIT or JSON behavior is changed. Driver helpers have one type
per file and pass the 500-line/80-line-method limits.

Existing native MossAgent/Codex windows are not opened, operated, restarted or
captured. Headless Avalonia windows and the browser's own page screenshot are not
desktop captures. Chrome has a unique test profile; its exact loopback endpoint
comes from that profile's DevToolsActivePort, never from scanning debugging ports.
The driver closes only its own process tree afterward.

## Configuration and actual UI input

- The production BrowserSettingsPane receives keyboard selection of Chrome and
  Existing CDP, endpoint text entry and Space activation of Save. The real SQLite
  repository is checked for all three saved values before starting conversations.
- A project, authorized directory and enabled default provider/model are seeded
  through their production repository ports. Provider/model editor creation and
  discovery are not repeated by this scenario.
- The production MainWindow runs at Light 1280x820. Actual composer typing and
  send-button keyboard activation create two project tasks. Real new-task/task-
  selection navigation commands switch between them without replacing runners.
- FullAccess applies only to this isolated synthetic project's roots and attached
  test browser. No existing user's policy or approval dialog is changed.
- Every recorded model request reaches the real Responses adapter with model
  fixture-cdp, medium effort, temperature 0.3 and Top-P 0.8. The local service
  responds using the actually advertised encoded tool names. This is scripted
  protocol validation, not a paid model or proof of autonomous reasoning quality.

## Concurrent workflow

1. Task A navigates to /a, clicks the local popup button, enumerates its tabs and
   reads a real snapshot. It has an initial page and a popup, neither of which is
   the pre-existing synthetic sentinel page. Its next model request is held.
2. Task B navigates to /b. Its model requests switching to A's real tab ID; the
   standard pipeline returns a failure, the UI retains tool_failed and B's next
   model request receives IsSuccess=false. A read-only observer supplies that ID
   to generate the negative request; no browser mutation bypasses the pipeline.
3. B reads its own /b snapshot and creates a real browser screenshot. Its next
   model request is held while both tasks remain active.
4. Selecting A and activating its visible Stop button cancels only A. The task
   persists as Cancelled and its two pages close. B remains Running on /b; the
   sentinel and attached Chrome survive. Page count moves from four to two.
5. B's held request is released. The Agent navigates to /b-after-stop and reads
   the actual page snapshot before receiving its final Markdown response. B
   persists as Completed and its own page closes, leaving only the sentinel.

One complete scenario makes 12 actual model HTTP requests and completes ten
browser audits: four successful A tools, five successful B tools and one expected
foreign-tab refusal. All audit records have completion timestamps. These are
workflow observations, not ten additional unit-test cases.

## Presentation, artifacts and restoration

- The completed turn keeps its normal collapsed default. Keyboard activation of
  the rendered turn summary reveals B's six tool records. The actual snapshot
  row button opens the stored /b-after-stop details; the Markdown reply renders
  through MarkdownSelectableTextBlock rather than a placeholder text control.
- Settings/save/send/stop/disclosure controls and the final reply are brought into
  view and input-hit-tested, not merely found in the visual tree. The snapshot
  details have visible selectable text after real button activation.
- Screenshot execution has one image/png artifact belonging to B. Its stored
  path, 3,943-byte length and SHA-256 match the real PNG bytes and signature.
  The final /b screenshot was visually inspected as a nonblank local page.
- A fresh production service provider reopens the same database in a Dark 526x601
  MainWindow. A remains Cancelled and B Completed. B's Markdown, expected failed
  tool and snapshot details restore and remain operable, with no new model
  request or browser page. The unchanged sentinel title/body and live attached
  browser are verified before the driver terminates its own Chrome.

## Final verification and evidence

Two complete final driver runs pass independently with the same 12-request,
ten-audit result. The repeated runs are not summed into a larger product test
count. The latest fresh regression scopes also pass without skips or aborts:

- Browser: 88/88.
- Agent: 123/123, including architecture constraints.
- Related App browser/embedded/shutdown scope: 195/195, matching discovery.

These disjoint regression scopes total 406. Solution build has zero warnings and
errors; scoped driver whitespace/source checks and documentation hygiene pass.
Nine production/test/driver assembly identities remain equal through the runs and
final build. No test-owned Chrome remains afterward.

Evidence is under `%TEMP%\MossAgent-cdp-agent-20261016`: final driver sources,
`live-verified-one.log`, `live-verified-two.log`, current TRX reports,
`source-audit.json`, build/format/identity reports and per-run `result.json`/PNG.
The final runs are `run-31dcf226fb224973b8fcc13b8fbeae03` and
`run-f938e96b606940d9b39b780aa53f99b2`.

Earlier driver attempts are retained. Their assumptions about model error-code
serialization, collapsed turn visibility and Markdown control type are corrected
to match production contracts. Input-hit-test attempts initially omit the normal
Headless dispatcher drain; processing queued jobs fixes the fixture's stale hit
testing without changing production scrolling or weakening the hit assertions.
An oversized driver method is split before final source-limit verification.

## Remaining scope

This is not a native desktop/Codex same-state visual comparison, an all-protocol
or full current App/all-suite checkpoint, or a resolution of the unrelated
historical allocation assertion. Native embedded-browser concurrency and the
complete responsive/DPI/state sweep remain separate. Borrowed CDP contexts still
share cookies, login and other storage; independent page ownership does not mean
storage isolation. The complete product objective remains active.
