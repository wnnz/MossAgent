# Proxy editor selection acceptance

## Design self-check

- App owns the editor target and list publication. Saving through the unchanged
  Application configuration port must never publish a record before it belongs to
  the visible catalog: the real two-way ListBox rejects out-of-catalog selections.
- The existing save/read/notification sequence, operation deadline, cancellation,
  serialized mutation ownership and draft revision guards remain authoritative.
- No proxy transport, authentication, credential storage, tool approval or schema
  changes. Test fixtures use only synthetic credentials, with boolean assertions
  rather than printing secrets. Native diagnostics read only the isolated QA data.
- Keep the fix in the existing small save method. Every handwritten C# file has
  one type, and all new methods/files remain within repository size limits.
- Reuse one ID-based catalog lookup for editor availability, reload, save/delete
  validation and row commands. Null IDs return immediately; indexed reads avoid
  closure and enumerator allocations without adding a cache or mutable index.
- Regressions exercise the actual shared settings page in both themes, create and
  edit flows, and a draft changed while storage is pending. Run existing proxy,
  editor lifetime/notification and authenticated routing tests plus a solution build;
  then repeat native failure/recovery with the real settings and conversation.

## Native finding

Changing the synthetic HTTP proxy password saves it, but the form stays open without
success guidance and its Delete action becomes disabled. A read-only check of the
isolated QA database confirms the changed password using a boolean only. The native
host and server are closed normally before modifying or building any App files.
The save path publishes its request record while the ListBox still owns the older
catalog. List selection feedback can clear the editor identity and invalidate the
revision before the post-save read publishes the updated list.

## Verification

- The real-page baseline fails all 5 cases: light/dark create/edit flows and a
  newer draft entered during a pending write. After the ordering fix, all 4
  normal saves close their dialogs. The first broader replay passes 47/48; its
  remaining failure is a test assertion expecting a word absent from the actual
  localized previous-draft notice. The assertion now uses that resource directly.
- Save reloads the catalog before publishing its actual saved member. ID and
  revision guards still preserve newer/reopened drafts, await notifications and
  reject removed targets. No binding mode or persistence contract is changed.
- The lookup regression measures 7,840,000 allocated bytes for 10,000 warmed
  checks before consolidation, and zero afterward. It also verifies stale row
  records resolve by ID, disabled/missing targets cannot be tested and checks do
  not modify another draft. The final proxy/settings selection passes 150/150.
- The complete App suite passes 3,600/3,600 in 6 minutes 52 seconds. Its 60-second
  per-test watchdog does not fire and no memory dump is generated. The solution
  build has 0 warnings and 0 errors. Whitespace checks pass; Roslyn audit confirms
  one type per touched C# file, at most 384 lines and at most 47 lines per method.
- Evidence is `C:\Users\wzheng\AppData\Local\Temp\MossAgent-native-proxy-recovery-20261011`:
  failing `tests/editor-before.trx`, `tests/editor-after.trx` and
  `tests/lookup-before.trx` are retained alongside `tests/proxy-final.trx`,
  `full-suite/app-full.trx`, `build.log` and `source-audit.json`.

## Native replay

- The isolated host's App DLL hash matches the solution build. A host-only compile
  first rejects access to an internal localized resource; the external observer
  is corrected without changing production visibility. Both host processes run
  without elevation and use only synthetic SQLite data and loopback endpoints.
- With the saved wrong HTTP password, Gemini discovery reports actionable HTTP
  407 guidance. A unique conversation send finishes Failed with the same guidance.
  Both requests are rejected after authentication retry; direct requests stay at
  zero, no authenticated request succeeds, and the 8 existing replies are unchanged.
- Restoring the synthetic password through the real editor closes its dialog and
  shows the update notice. The observer confirms no catalog-external selection,
  no proxy error and no credential-leak flags.
- Without restarting App or provider, discovery synchronizes one model and the
  same conversation completes successfully. The reply count becomes 9; the only
  authenticated requests are model discovery and the Gemini streaming endpoint.
  Direct requests remain zero. The first unauthenticated 407 challenge on each
  successful operation is expected and not misclassified as a bad password.
- Passive Python captures are `wrong-conversation.png`, `restored-save.png`,
  `restored-discovery.png` and `restored-conversation.png`; each stage also retains
  safe JSON observations. UI and server close normally with exit 0 and empty stderr.
  The Python script and the QA host remain outside the repository.

Native coverage here is Gemini over HTTP, light theme, scale 1 at 1920 by 1032.
Missing credentials, other protocols and SOCKS5 failure/recovery retain their
deterministic coverage, not new native coverage. No paid endpoint, real user
configuration, AOT/JSON migration, commit or release is involved. The wider product
and Codex visual-fidelity goal remains active.
