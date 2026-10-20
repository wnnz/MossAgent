# Explicit PR URL opening

## Scope and ownership

This adds an explicit account-and-URL dialog to the existing PR page. It opens
one authorized GitHub PR directly, including closed PRs that are absent from the
open-PR list, and attaches the existing revision-bound inspection and review-chat
flows. It is not the complete Codex PR workflow or a personal inbox.

- Domain owns `SourcePullRequestLink` and the resolved scope/number value.
- Application's `SourcePullRequestLinkResolver` reads existing account/grant
  ports and reuses `SourceRepositoryAccess`; it does not create authorizations,
  task sessions or HTTP requests.
- App's independent `PullRequestLinkViewModel` owns only form state and operation
  lifetime. `PullRequestsViewModel` performs detail reading through the same
  standard `source.pr_get` executor used by list selection. Views use shared
  controls and resources; they do not use SQL, files, processes or HTTP.
- No schema or publication-mode changes are needed. One handwritten type per
  C# file, module direction and existing size limits remain applicable.

## User path and interpretation

1. Open Pull Request, then choose **从 PR 链接打开**. Opening the dialog makes no
   source/model request, and does not change an unfinished repository grant form.
2. Explicitly select a saved source account, identified by name and API endpoint.
   A first-time dialog does not silently select an account. A subsequent opening
   retains its choice only if the metadata identity/revision still exists.
3. Paste an HTTPS web PR URL and submit. Invalid input, wrong account/platform,
   missing authorization and remote failures remain visible in the dialog, with
   unchanged editable input and explicit repair/retry instructions.
4. The returned PR becomes the single visible result and selected detail. The
   status explains that this is not an open-PR page. **获取开放 PR** explicitly
   returns to the normal open-PR list; no list query is silently added.
5. Existing file/comment/check reads, snapshot review, read-only source review
   and manual model sends retain their existing restrictions and persistence.

Recognized paths are `/owner/repo/pull/number`, optionally with a trailing slash
or `files`, `changes`, `commits`, `checks`. File/change/commit suffixes may include
one 40- or 64-hex commit. Query strings and fragments are ignored, never requested,
stored as task context, or used as instructions. These forms identify the PR;
they do not select a check, locate a comment, or pin a historical commit. The
dialog explicitly says that current PR details will be read.

`api.github.com` maps only to web `github.com` on its normal HTTPS port.
For a configured Enterprise endpoint, the web host and port must equal the
configured API authority; the existing API path prefix is retained when building
the request. No alternate web/API host mapping is guessed. Instances requiring
separate web/API authorities need a future explicit configuration extension.

## Safety and lifecycle

- Reject userinfo, non-HTTPS links, blank/control characters, oversized input,
  backslashes, encoded authority/path, raw dot segments, unsupported paths and
  invalid/nonpositive/overflowing PR numbers before any HTTP request.
- Domain matching is exact, not suffix-based. No request follows the pasted URL,
  query, fragment, redirect or provider-supplied link. Only normalized owner,
  repository and number reach the configured source API client.
- The selected account must still be enabled and at its captured revision.
  Its exact repository must already have a current explicit grant. Another
  account on the same host cannot borrow that grant. Missing grants require the
  separate existing authorization flow, not a silent expansion of scope.
- Detail reads retain standard capability/schema checks, ReadOnly policy,
  source scope, bounded output, 30-second tool execution, cancellation and audit.
  Application still revalidates access after HTTP before releasing source data.
  The form additionally has a linked 40-second operation deadline.
- Previous inspection work is invalidated and drained before requesting another
  linked PR. Closing, canceling, navigating or disposing cancels the same
  operation; cleanup is awaited and stale results cannot populate another page.
  Reopening or editing never retries automatically.
- No local roots, worktree, browser session, child agent, source write or model
  permission is added by a URL. A linked review chat remains a separate ordinary
  projectless conversation bound to its exact grant/PR/revision/mode.
- Errors use fixed localized resources, not exception text, URL parameters,
  response bodies or credential-bearing account objects.

## Validation

Verified candidate: `artifacts/pr-links-verified-20261019`. Its terminal reports
confirm the full Debug solution build with zero warnings/errors and 2,952 passing
tests, no failures or skips. This is seven unfiltered test projects plus filtered
App PR/source/shared-component/navigation tests, not all App or solution tests:

| Project | Passed |
| --- | ---: |
| Infrastructure | 660 |
| Tools | 666 |
| Agent | 155 |
| App, selected regression scope | 337 |
| Visual | 300 |
| Providers | 600 |
| Browser | 88 |
| Windows | 146 |

The new link-specific tests account for 58 Infrastructure and 26 App cases within
those totals. The selected 2,309 source/config paths and all 2,035 files across
the eight test projects' Debug directories have identical path sets and SHA-256
values before and after testing. Structural audit of the 496 changed/new C#
files, 27 AXAML files and 2,804 methods/accessors finds no violations; the largest
method is 78 lines. Eight new link-error/detail frames were visually inspected
after the terminal run, covering both themes and both window sizes.

The prior candidate `artifacts/pr-links-20261019` is retained unchanged: 2,947
passed and four grant-dialog tests failed because they counted all dialogs as
one after adding the link dialog. Source/runtime identities were stable there
too. The subsequent assertion targets the grant dialog by its actual DataContext
and close command, checks exactly one visible dialog and verifies both directions
of grant/link exclusivity; it does not relax the visible footer/control checks.
A missing-account recovery test and corresponding localized settings hint were
also added before rebuilding and rerunning the same broader regression scope.
Neither candidate establishes full-product completion.

Focused tests cover common URL forms and origin/port attacks, explicit grant and
account selection, stale/revoked/disabled/deleted accounts, cancellation without
session creation, actual pipeline HTTP and audit, remote failure/manual retry,
post-HTTP revocation, delayed cleanup, navigation/disposal, and waiting for prior
inspection cleanup. A separate controlled model/source roundtrip opens PR #42
from a link rather than an existing PR #7 list result and preserves #7's draft.

The rendering tests retain assertions for real shared controls, editable errors,
input/button/status bounds, keyboard Escape and returned opener focus. They save
error-dialog and linked-detail frames at 526x601 and 980x740 in both themes. An
initial wrapper incorrectly bound visibility and delayed inner-dialog attachment;
the final view mounts the shared dialog directly, without weakening focus/bounds
checks or changing global dialog behavior.

Evidence uses production DI, temporary SQLite, controlled source HTTP and model
SSE, and Avalonia Headless. It is not native Windows interaction, live GitHub or
Enterprise account verification, paid-model acceptance, Release validation or a
published executable. Inbox filters, reviews/activity, viewed/pinned markers,
related stacks and confirmed remote writes remain separate implementation gaps.
