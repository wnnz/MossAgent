# Authenticated provider proxy acceptance

## Plan and design self-check

- Previous goal work made concrete progress through sidebar reconciliation,
  runtime interactions and real external-Chrome concurrency evidence. The wider
  product goal remains active; anonymous proxy evidence does not prove authentication.
- Authentication remains owned by Infrastructure's existing HTTP/SOCKS5 client
  factory. App uses real settings commands and SQLite; Providers owns discovery,
  stream parsing and safe public error mapping. No cross-module shortcut is added.
- Extend deterministic App-composition tests with authenticated loopback proxies,
  covering Responses, compatible Chat and Gemini, valid/missing/wrong credentials,
  explicit direct routing and prevention of credential leakage in failures.
- Fixture framing is bounded, cancellable and loopback-only; it validates targets
  without forwarding to or resolving remote hosts. Its observation records contain
  request route and boolean flags only, never raw credentials, headers or messages.
- Existing approvals, task-authorized paths, production timeouts/output limits,
  database schema and JIT single-file packaging remain unchanged. Handwritten helper
  types are separate files and methods stay within the engineering size limits.
- Preserve a failing security baseline if authenticated errors expose secrets;
  fix the owning public-error boundary, add no-read/cancellation regressions, then
  verify the real settings/discovery/conversation path in an isolated native host.

## Evidence and limits

- HTTP 407 bodies can contain proxy credentials that Providers cannot redact from
  an AiProvider's ProxyId. Discovery and the common streaming error reader now
  return the same localized authentication guidance without opening or reading
  the body. Cancellation remains authoritative and responses are disposed.
  Other statuses retain their existing sanitization, byte limits and decoding.
- The four protocol adapters share error-event sanitization instead of repeating
  six type-check branches. Non-failure events retain their identity without copies;
  sanitizing failures does not mutate the original event. Provider test setup is
  shared rather than repeating protocol factories and request construction.
- Providers regressions cover all four protocols, declared/unknown lengths, empty,
  credential-bearing, malformed UTF-8, oversized and unfinished 407 bodies. All
  80 body cases initially failed, while the pre-cancelled reader test passed.
  Eight additional cases check cancellation after response headers and disposal.
  The complete Providers suite now passes 469/469, including event identity tests.
- App tests use real settings commands, isolated SQLite and production clients.
  The authenticated matrix passes 30/30 across Responses, compatible Chat and
  Gemini with HTTP Basic and SOCKS5 username/password authentication. It covers
  valid/missing/wrong credentials, no direct fallback, explicit direct routing,
  and correct -> wrong -> restored credentials without restarting the provider.
  The broader focused App proxy/routing selection passes 81/81; Infrastructure's
  client-factory and connection-tester selection passes 9/9.
- The first credential-edit test run exposed a fixture-only assumption: SOCKS5
  always expected the synthetic hostname/port, while the new no-fallback case
  intentionally targets a second loopback server. Its three failures are retained
  in `app-proxy-before-fixture-correction.trx`. The fixture now accepts an explicit
  expected endpoint without weakening target checks or forwarding any traffic.
- Evidence: `C:\Users\wzheng\AppData\Local\Temp\MossAgent-provider-cleanup-20261011`.
  `tests/proxy-error-before.trx` retains the 80/81 failing baseline; post-fix results
  are `providers-after.trx`, `app-proxy-final.trx` and `infrastructure-proxy-after.trx`.
  Assertions and observation records never print credential values or headers.

- Solution build: 0 warnings, 0 errors. Whitespace verification passes for all 16
  touched C# files; each contains one handwritten type and stays under 250 lines.
  Tracked diff checks and new-file trailing-whitespace checks also pass.
- The first solution-wide test command was interrupted during the long App visual
  group after 3,051 passing App cases. That command is not recorded as successful.
  A standalone App replay with a 60-second per-test watchdog and no memory dumps
  passes 3,531/3,531 in 4 minutes 36 seconds; the watchdog does not fire. This was
  a long-running rendering group, not a reproduced production/test deadlock.
  Other projects pass 1,427/1,427, including Windows 137/137, so all test projects
  are verified at 4,958/4,958 across the completed project runs. Evidence is in
  `full-suite/`, `app-hang-diagnostic/app-hang-diagnostic.trx` and `build.log`.

## Native success-path follow-up

- An isolated, non-elevated native host uses the real App composition, settings,
  model picker and conversation with synthetic SQLite data and loopback endpoints.
  The existing Python window-capture script stays outside the repository. No real
  user configuration or credentials are read.
- Actual UI interaction saves the HTTP proxy configuration and discovers one
  `fixture-model` for each of Responses, Gemini and compatible Chat. Manual models
  remain present; Gemini's non-generation discovery entry is not imported.
- Responses explicitly selects the direct route in the provider editor and is
  saved through the UI. Its conversation produces only a direct
  `POST /v1/responses`, despite the configured default HTTP proxy.
- Gemini discovery and conversation use authenticated HTTP; the conversation
  target is `/v1/models/fixture-model:streamGenerateContent?alt=sse`. A first 407
  challenge followed by an authenticated retry is expected, not a wrong-password
  failure. The inherited unsupported reasoning override blocks sending until the
  real reset action restores the model default.
- Compatible Chat discovery and conversation use authenticated SOCKS5, with
  `POST /v1/chat/completions`. Neither proxied conversation adds a direct request.
  All three sends finish as Completed and add their expected reply. White-listed
  UI and route observations contain no credential-leak flags.
- The host and server both close normally with exit 0 and no stderr. Evidence is
  `C:\Users\wzheng\AppData\Local\Temp\MossAgent-authenticated-acceptance-20261011-2202`,
  including proxy-save/discovery, direct/HTTP/SOCKS5 observations and passive
  screenshots. The window renders at 1920 by 1032, scale 1, in the light theme.
- This follow-up verifies native success and explicit-direct routing only. It does
  not claim a new native wrong/missing-credential or credential-restoration run;
  those negative paths remain supported by the deterministic tests described above.

## Native HTTP failure and recovery follow-up

- A subsequent ordinary-permission run covers Gemini model discovery and streaming
  with a wrong synthetic HTTP password, then recovery through the actual settings
  editor. Failed discovery and conversation show safe HTTP 407 guidance; no direct
  fallback, successful authentication, new reply or credential leak is observed.
- The edit exposed an out-of-catalog ListBox selection during save. App now reloads
  the catalog before publishing its saved member, retaining draft/lifetime guards.
  The fixed native save closes its dialog and displays the update notice.
- Without restarting the App or provider, discovery and conversation then succeed
  through authenticated HTTP, adding one expected reply. Direct requests remain
  zero. All observation records and messages have false credential-leak flags;
  both processes close normally with exit 0 and empty stderr.
- Evidence and the 150-case focused / 3,600-case complete App regression are in
  `PROXY_EDITOR_SELECTION_ACCEPTANCE.md`. Native missing-credential, SOCKS5 and
  other-protocol failure/recovery are not claimed by this follow-up.

No paid endpoint, real user configuration or daily browser profile is used. No
full-product, TLS/public-network or Codex pixel-equivalence completion is claimed.
JIT debugging and Windows x64 single-file publishing are unchanged. No commit,
push or release is performed.
