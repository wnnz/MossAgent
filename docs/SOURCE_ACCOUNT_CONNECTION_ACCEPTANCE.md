# Source-account connection checkpoint

## Scope and entry point

Settings → 源代码账号 now creates, edits, enables/disables and deletes independently
named GitHub personal-token connections. The shared dialog selects an explicitly
configured proxy or direct access. Each row's 刷新身份 action reauthenticates with
the stored configuration; 刷新列表 only reloads local metadata and does not claim
to refresh authentication. This is not a PR inbox, repository grant, OAuth login,
Git credential manager integration or a claim of Codex connector compatibility.

## Design self-check

1. Domain owns immutable account/identity values and endpoint validation;
   Application owns the repository/identity/HTTP ports and revision-checked
   connection refresh. Infrastructure implements SQLite and GitHub HTTP. App
   calls these ports and uses the shared settings controls, not SQL or HTTP.
2. Reuse proxy profiles and the existing settings lifetime/shared dialog; do not
   fabricate an AiProvider or reuse its authenticated headers for source access.
3. Saving credentials and explicitly testing their identity are user settings
   operations, not Agent tools. They create no task roots or tool grants.
   Subsequent PR reads/writes must implement source/repository authorization and
   use the shared execution pipeline; account verification is not that grant.
4. Every new handwritten C# file has one type. Account editing, catalog lifetime,
   safe row metadata, migration, transport, parsing and ports are separate files;
   no partial-class workaround or per-page visual style is introduced.
5. Controlled HTTP, real SQLite, raw loopback sockets and production-composed
   Headless UI tests cover persistence to actual requests and displayed identity,
   with rejection, cancellation, stale results and credential-leak negatives.

## Runtime and safety boundary

- Schema 18 adds source_accounts without deleting or reinterpreting existing
  projects, tasks, drafts or providers. Migration failure rolls back creation;
  retry and repeated initialization preserve existing data. Revision-checked
  saves/deletes reject stale editors and cannot resurrect a deleted saved row.
- Proxy references prevent deleting an in-use profile. Enabled connections reject
  missing/disabled proxies. Disabling an account can preserve its existing
  disabled proxy; revocation does not depend on a working network configuration.
  Missing references and reenabling with a disabled proxy still fail. Runtime
  creation rechecks the selected profile and never
  falls back to direct or another account. Proxy-list refresh preserves the
  selected unavailable identity and the current editor draft.
- API addresses require HTTPS, no credentials/query/fragment/ambiguous escaped
  paths, and an API root or /api/v3/ prefix. GitHub.com uses api.github.com, not
  its website. An explicitly configured Enterprise server keeps its authority,
  port and prefix. This validation does not establish that a custom server is
  trustworthy; configuring it explicitly chooses the credential recipient.
- GET user is built from that pinned base, not response URLs. Authorization is
  added to this request only. Production handlers disable redirects, cookies and
  inherited system proxies; direct access and explicit proxy access are distinct.
  Ordinary platform HTTPS certificate validation remains enabled.
- Identity requests have a 30-second deadline and linked cancellation; headers
  are bounded, decompressed bodies stop at 64 KiB, and JSON depth is limited to
  16. Non-success bodies and Location URLs are not exported. The displayed
  numeric ID/login shape is validated and direct credential reflection rejected.
  Authentication, forbidden/rate-limit, redirect, timeout and network errors use
  fixed localized messages, without the original response or exception chain.
- Credentials follow the existing plaintext-SQLite product decision. Account
  and editor JSON serialization excludes the credential field, ToString does
  not print it, and list rows contain metadata only. The dialog masks the token
  and clears it on closing/disposal. No logging/export path is added.
- Refresh reads the account before and after the request and requires the
  captured revision to match. A changed/deleted/disabled connection cannot
  publish the old successful identity. This is a consistency check, not an atomic
  remote permission guarantee or the still-missing live PR grant revocation.
- Late reads/saves cannot replace a newer form. A save may commit before
  cancellation; the UI warns to reload and does not replay it on startup.
  Cancellation awaits request/body cleanup before releasing the operation.

## Validation candidate

Candidate: artifacts/source-accounts-20261018. Earlier Git push and tool-scope
candidates remain separate, unchanged historical evidence. Initial diagnostics
retain a stale restore-assets build failure and a test-only false-positive where
the body string [] matched Byte[] in a stack trace; neither is hidden as a pass.
Restoring the existing declared dependencies fixes the former; the latter checks
the bounded public message while separate credential tests inspect ToString.

This first candidate has five Completed, zero-failure/skip reports: 490
Infrastructure, 624 Tools, 145 Agent, 99 related App and 300 Visual checks,
totaling 1,658/1,658, plus a complete zero-warning/error solution build. The 2,220
source/configuration and 2,259 runtime file hashes/path sets agree before/after
the run; no source ZIP or recoverable runtime snapshot is claimed. The shape
audit passes 360 C# files. Twelve Headless renders cover both themes, compact and
ordinary viewports and 1/1.25 scaling; selected renders were visually inspected.
Focused passes overlap broader reports and are not added again.

A separate source-account-disable-20261018 candidate follows this first version.
Its two new SQLite/UI regressions first fail because an unavailable proxy also
blocks disabling the account. The fix separates revocation from enabled-proxy
validation, preserves the selected proxy and retains the missing-reference and
reenable denials. Earlier broad results do not validate this later code; its
current build and regression have separate terminal evidence.

The follow-up now terminates with a clean complete solution build and two
Completed reports: 493 Infrastructure and 100 related App checks, totaling
593/593 without failures/skips. All 2,221 selected source/configuration and 2,259
runtime file hashes/path sets match before/after testing. The 361-file changed/
new C# type/size/syntax audit and git diff whitespace check pass. The five focused
green cases overlap these reports. Neither the earlier 1,658 cases nor earlier
focused UI runs are added to this later total. Native/live-account and complete
solution-test acceptance remain separate.

## Still incomplete

Repository-specific grants, PR search/inbox/URL/details/diff/comments/checks,
review chat, revision-bound feedback and create-PR tools remain to implement.
OAuth, GitLab, cloud review and plugin compatibility are not substituted with
token settings. No real account, paid model, production GitHub authentication,
native user window or EXE restart has been used for this checkpoint.
Controlled loopback forwarding deliberately replaces only the HTTP factory in
UI tests; it proves requests/identity presentation, not production TLS trust or
a real GitHub account's permissions. Raw production-handler tests separately
exercise redirects/cookies/direct access and the selected HTTPS CONNECT proxy.
