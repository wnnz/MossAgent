# CDP task-page ownership acceptance

## Design and reproduced defect

Browser owns task page identity, popup discovery and disposal through the existing
IBrowserSession contract. Application, tool execution, settings persistence and
approval policies do not change. Each handwritten type stays in its own file;
the nine affected C# files pass the 500-line/80-line-method limits.

The original factory attaches every task to the first pre-existing CDP page.
Session tab synchronization then imports every page in the borrowed context.
Two tasks can consequently navigate the same page and enumerate unrelated pages.
All six initial isolation cases fail against that unchanged implementation.
Cancellation of NewTabAsync can also abandon a late page, and repeated disposal
can release the same runtime more than once.

## Change and safety boundary

- Each explicit loopback CDP attachment creates a new task-owned page. The
  pre-existing page, context and attached browser are not owned by that task.
- PlaywrightBrowserTabSet tracks stable IDs for the initial attached page,
  task-created pages and popups descended from those pages, including nested
  popups. Borrowed contexts never import the context's entire page collection.
  Managed contexts retain their existing full-context discovery behavior.
- An explicitly supplied borrowed initial page remains borrowed by default.
  Factory-created CDP pages opt into initial-page ownership.
- Creation and disposal share a session-owned gate. Cancellation waits for the
  SDK page handoff, closes an unpublished late page and retains the caller's
  cancellation token. Disposal waits for creation and rejects new work.
- Disposal closes owned pages only, detaches popup subscriptions and releases
  runtime state once. A failed close does not prevent attempts on other owned
  pages; the first failure is still reported. Popups arriving during cleanup are
  included without republishing state after shutdown.
- This is page-operation and lifetime isolation, **not storage isolation**.
  Tasks attached to the same configured CDP context still share its cookies,
  login state and other context-level storage. No arbitrary debugging-port scan,
  new incognito context or daily-browser attachment is introduced.

## Deterministic verification

Final, non-overlapping runs have no failures, skipped or aborted cases:

- Browser: 88/88, including the 12 new ownership/isolation boundary cases.
- Tools: 494/494, retaining the standard policy, approval and audit pipeline.
- Agent: 123/123, including architecture constraints.
- Related App: 195/195; discovered and executed counts match.

The solution builds with zero warnings/errors. Scoped whitespace verification
and the nine-file Roslyn source audit pass. Nine assembly hashes remain unchanged
through these runs. The real-driver Browser assembly matches the production and
test Browser assembly hash. No test-owned Chrome process remains afterward.

## Real settings-to-tool execution

An external driver hosts the production BrowserSettingsPane in Headless Avalonia
and uses real keyboard selection, endpoint entry and Save. The isolated SQLite
configuration is checked before creating real project/task records. Subsequent
operations use the production task session manager and standard IToolExecutor,
not direct page calls that bypass approval or auditing.

The driver starts its own headless Chrome with a unique temporary profile and
loopback random-port endpoint read only from that profile's DevToolsActivePort.
Local deterministic HTTP pages verify independent navigation, task-only popups,
foreign-tab rejection, ReadOnly navigation rejection, a real PNG screenshot and
continued operation of task B after releasing task A. All 12 audits finish:
10 successes and two expected foreign-ID/policy refusals. After releasing B,
only the pre-existing synthetic sentinel page remains unchanged; Chrome is still
alive until the driver closes its own process tree in finally.

Evidence is under `%TEMP%\MossAgent-cdp-isolation-20261016`, including TRX reports,
`live-final.log`, `source-audit.json` and the final `live-result.json`/PNG in
`run-a0522f1703af4d54a896969b68ad18b6`. The PNG was visually checked as nonblank.
An earlier external-driver attempt fails with audit_unavailable because its
synthetic tasks were missing; the corrected fixture creates them rather than
weakening production auditing.

## Remaining limits

`CDP_AGENT_WORKFLOW_ACCEPTANCE.md` adds a subsequent production Agent/Responses
conversation workflow driven through actual Headless settings/send/stop controls,
with task cancellation, foreign-tab rejection and fresh-composition history.
Its scripted local provider and Headless UI do not replace native/paid-model or
whole-product acceptance. That separate checkpoint must not be summed into this
slice's regression counts.

These 900 deterministic cases and the real driver are not a current complete App
or all-suite checkpoint. No paid model initiated this workflow. Existing native
MossAgent/Codex windows were not operated, restarted or captured. Native embedded
browser concurrency, same-state Codex visual comparison and whole-product
acceptance remain open. Earlier full-App and pixel runs are not added to these
counts or presented as current end-to-end coverage.
