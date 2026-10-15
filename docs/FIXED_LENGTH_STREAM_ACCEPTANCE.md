# Fixed-length streaming transport acceptance

## Plan and design self-check

1. The preceding proxy option turn made verified progress. This slice audits the
   open fixed-length transport observation, not whole-product completion.
2. Providers own protocol streaming; deterministic tests use the existing
   IModelProvider and IProviderHttpClientFactory ports, without changing runtime
   parser, HTTP factory, application composition or tool execution policies.
3. Use bounded loopback fixtures and synthetic payloads only. A private ephemeral
   TLS certificate is pinned by exact certificate bytes, with hostname errors
   still rejected. It never enters an OS trust store or production configuration.
   Retain cancellation, stream disposal and existing framing/output budgets.
4. Keep each handwritten C# file to one type and below 250 lines where practical,
   with methods below 40 lines where practical and all hard limits respected.
5. Prove partial text before response completion, release and successful finish,
   cancellation/disconnect, early enumeration disposal and wrong-certificate or
   hostname rejection across the actual provider adapters. Never replace the
   failing plaintext observation with a green chunked/TLS result.

## Independent transport observation

- The original .NET/raw TCP probe evidence remains under
  `%TEMP%\MossAgent-subagent-lifecycle-recheck-20261012`.
- A new repository-external Python socket probe independently reproduces the
  delay, without MossAgent, HttpClient, .NET, UI or SSE parsing. The listener writes
  HTTP headers and a complete first SSE frame in one send, then holds the tail.
- Both plaintext fixed-length cases (event-stream and octet-stream) return only
  headers during the two-second hold. The body arrives after releasing the tail.
  Raw non-HTTP, close-delimited HTTP and chunked HTTP deliver the first frame.
- The same Python fixed-length exchange over TLS delivers the first frame during
  the hold. TLS validation trusts only the generated certificate in a private
  client context; no system certificate, firewall, proxy or security setting changes.
- This narrows the observation to the local plaintext HTTP transport path. It
  does not identify the intercepting component or prove a general Windows/.NET
  defect, and it does not make plaintext fixed-length streaming verified.
- Evidence: `python-transport-probe.json` and `python-transport-tls-probe.json`
  under `%TEMP%\MossAgent-fixed-sse-20261012`. Scripts stay outside Git.

## Verification

- The real SslStream/SocketsHttpHandler fixture sends a declared complete body
  length but holds its tail. All four actual adapters deliver the first text
  delta while the server remains open; release yields the remaining text without
  a failure. Cancellation and early disposal disconnect without releasing the
  tail. A different certificate is rejected before any response frame is sent.
- Fixture certificate validation pins exactly the generated certificate and
  rejects missing certificates or hostname errors. Its test-only factory does
  not replace production validation, configure proxies or establish public CA,
  remote-account or real-provider compatibility.
- The first fixture activation fails because Windows Schannel cannot use its
  generated ephemeral key directly. Re-importing the synthetic PFX with
  UserKeySet, without PersistKeySet, gives the disposable fixture ownership of
  the key; no OS trust-store installation or production factory change occurs.
  The original 12-failure/one-pass setup TRX is retained, followed by 13 passes.
- The added wrong-pin cases initially fail only in the fixture's server-side
  rejection observation: a TLS alert can surface during the first request read,
  after server authentication returns. The client already rejects the connection.
  The fixture now observes both stages before sending a frame, without weakening
  rejection or swallowing unexpected failures in positive cases. Original
  four-failure focused/full TRX files remain alongside the corrected replay.
- Final focused TLS replay passes 17/17; the full Providers suite passes 516/516.
  Post-build architecture checks pass 7/7 and the existing App subagent/lifecycle/
  settings/read-lifetime slice passes 45/45. The solution builds with zero
  warnings/errors. Evidence: tls-stream-focused-pin-fixed.trx,
  tls-stream-providers-pin-fixed.trx, tls-stream-architecture.trx,
  tls-stream-app-lifecycle.trx and solution-build-final.log in the evidence root.
- Both new C# files have 161 physical lines and one type declaration. Structural
  review keeps methods below 40 lines; scoped whitespace verification and the
  repository-default diff check pass. Unrelated changes remain un-staged.

No fresh native UI, DPI/theme comparison or full App rerun belongs to this slice.
The previous native child lifecycle uses chunked HTTP, not TLS. Plaintext
fixed-length incremental delivery and its responsible local component remain
unresolved; green TLS regressions are not a workaround claim or product completion.

## Native TLS follow-up, 2026-10-12

- Evidence is retained outside Git at
  `%TEMP%\MossAgent-fixed-tls-native-20261012-154033`, including screenshots,
  native observations, fixture source, transport logs and persistence fingerprints.
  This follow-up changes no production logic, user configuration or certificate
  validation. The six external C# files each contain one type.
- The ordinary-permission host uses the real App resources, MainWindow, view models
  and production composition registrations with an isolated synthetic database and
  authorized temporary project. Its custom Application owns startup, so this is
  not verification of the complete production App startup. Only the provider HTTP
  factory is substituted, allowing two exact loopback endpoints with exact-byte
  certificate pinning and hostname-error rejection; no certificate is installed.
  The real adapters, Agent, tool pipeline, audit storage and close coordinator run.
- Native settings interaction saves trigger guidance, child prompt, the Responses
  child model, high reasoning, sharing disabled and enabled state. A Gemini parent
  uses complete HTTP responses; the Responses child uses fixed-length HTTPS with
  its final SSE bytes held back. Partial text is visible before releasing the tail.
  Three native sends in one process prove successful completion, Stop while held
  with one disconnect and a cancelled child audit, then another successful send.
  Five parent and six child requests retain the chosen child model, reasoning and
  prompt without the synthetic parent-context marker. Three file reads use the
  standard execution pipeline and leave the authorized file unchanged.
- The final database contains 11 messages and six approved terminal audits: three
  successful file reads, two successful child runs and one `tool_cancelled` child
  run. The cancelled partial answer is persisted; no dialogue remains partial.
  A new process opens the same database without sending a request. Message and
  audit fingerprints, selected identities and saved settings remain unchanged;
  the earlier cancellation remains cancelled and no side effect is replayed.
- Screenshots cover 1280-by-820 light and actual dark views, then a 980-by-640
  light restored conversation at 96 DPI. The fixture explicitly selects Light
  on startup: dark-theme preference restoration is not verified. This is not
  high-DPI, remote-provider or same-state Codex pixel-parity acceptance. Synthetic
  SSE text joins a Markdown heading directly to preview text, so the inline
  heading is not evidence of a production Markdown parsing defect.
- Fixture setup failures are preserved: resource reassignment first fails because
  the dictionary already has a parent; a later first send fails because the
  external HTTP factory lacks BaseAddress, with zero provider requests. Only the
  external fixture is repaired. Delayed native observations are checked before
  repeating input. Both successful run and restore hosts close normally with
  exit zero and are confirmed absent. Host and then-current Debug App DLL hashes
  match in `app-hashes.json`; this identifies the verified binary, not later builds.
- This follow-up passes Providers TLS 17/17 and App child-lifecycle/configuration
  30/30. The solution builds with zero warnings/errors; external fixture whitespace
  verification passes. Logs and TRX files are retained in the evidence root.
  The preceding 5,433-case full run is not a new full run for this follow-up.
  The original plaintext fixed-length delay and its responsible component remain
  unresolved; native TLS success does not reclassify that failure.
