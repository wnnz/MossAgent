# Integrated production workflow acceptance

## Scope

The actual Debug/JIT `MossAgent.App.exe` starts through its production composition
root against fresh synthetic SQLite data and local project roots. Loopback
Responses, Gemini, MCP and HTML fixtures replace remote services, not App views,
repositories, orchestration or tool execution. No paid endpoint, real credential,
daily browser profile or user database participates.

The positive run uses App assembly SHA-256
`5FAAF93F347F3B9B10CB712900AD10360D77EE87EF051911BAFDDB2880B921DB`
and Agent assembly SHA-256
`47B2D1364F886E25EAC9D6DD1FA24F936266F7D5F2AC574CE5DE9FF9EF5001D0`.
Both native starts independently report an unelevated token. Native input uses
Sky only; Python reads state and passively captures stable, non-black pixels.

## Successful combined flow

The real conversation input and send button start this sequence:

1. File write, read and replace produce `workflow.txt` with `status=edited`.
2. Parent shell execution reads the authorized file and returns a success marker.
3. Browser navigate, type, click, snapshot and screenshot operate on the attached
   managed local page. Its embedded WebView2 shows the actual input and result.
4. Configured Gemini child reads the edited file, executes its own project-local
   shell check and returns Markdown with a code block through `subagent.run`.
5. MCP echo runs through the registered standard tool pipeline.
6. The parent receives child/MCP results and renders the final response. SQLite
   records the task as Completed; the send control returns to its idle state.

The parent makes twelve Responses requests using its selected model, medium
effort, temperature 0.6 and Top-P 0.85. The child makes three Gemini requests;
each carries the configured system prompt, shared parent marker and HIGH thinking.
The parent's subsequent request contains the actual child answer and shell result,
not merely a synthetic final-summary assertion. Signed child tool continuations
remain in the real adapter path.

Native screenshots show child-running status, both child tool results, third
model turn, successful completion and the rendered child Markdown/code block.
The raw persisted tool payload independently contains successful child dialogue.
The file card opens the task-owned immutable snapshot patch through the actual
`filesystem.review_change` tool and shows the two added lines.

After review there are sixteen completed audits: fifteen successful and one
`tool_cancelled` old `filesystem.change_statistics` request superseded by the
later edit. Its replacement succeeds and the final card displays +2/-0. This
cancelled auxiliary request is not reclassified as success or a child failure.
The browser PNG and review patch both pass recorded length and SHA-256 checks.

## Restore and shutdown

Normal close removes the first App process and task WebView2 processes. Restarting
the same production executable/data directory, then opening the saved conversation,
restores the completed parent response and child dialogue, including Markdown.
The timeline disclosure, inner dialogue scrolling and project-context toggle work
through native input. Models, proxies and child settings retain their recorded
table hashes; the foreign project's synthetic file is unchanged.

Model request counts remain exactly twelve parent/three child after restart:
no incomplete operation or model call is replayed. A new read-only file-statistics
request brings the restored audit count to seventeen, with the same single earlier
cancelled record. The task remains Completed and has fourteen saved messages.
The restored App and loopback service then close normally, leaving no matching
task WebView2 processes; service counters finish at 12 parent, 3 child, 1 page,
and 1 MCP call. Stderr is empty.

## Separate negative branch

The preceding production-App run requested a child read outside the task root.
The inner read and outer `subagent.run` fail with `path_denied`; the denied inner
read has no granted approval. The child stops after two requests, not three.
The parent continues through MCP and completes. This is isolation evidence,
not a successful child run. No foreign content appears in whitelisted request
observations. Its completed snapshot review and artifact hashes are also verified.
That run predates the subsequent context-building optimization; the positive
combined run above uses the optimized Agent assembly.

## Passive Codex comparison and limits

A passive current Codex capture and the production App capture agree on sampled
light-theme RGB values: title/rail (238,244,249), sidebar (249,251,253), main
(255,255,255). This establishes those color samples, not whole-window equality.
Codex is 1920x1032 and MossAgent is 1280x820; their task data and panel states
also differ. No Codex UI input or same-state pixel-parity claim is made.

The run does not replace provider/proxy editor acceptance, multi-task cancellation,
all protocol/transport checks or the full light/dark/window-state visual matrix.
Settings are deliberately seeded before startup, not edited during this scenario.
No production code, permissions, persistence schema or publishing options change.

Scripts, databases, requests and screenshots remain outside the repository:

- `%TEMP%/MossAgent-integrated-success-20261012-192953`
- `%TEMP%/MossAgent-integrated-workflow-20261012-185830`

The preceding same-source gate has 5,511 passing tests across eight assemblies;
one initial parallel Windows process-test timeout passes in the isolated rerun.
The current continuation also passes a fresh zero-warning/error solution build,
119/119 Agent tests and 134/134 focused App child/payload/snapshot-review tests;
documentation whitespace checks pass. The App assembly hash and its copied Agent
assembly still match the native-run identities after that build. This is fresh
composition evidence and focused regression, not another full-suite run.
The complete product objective remains active.
