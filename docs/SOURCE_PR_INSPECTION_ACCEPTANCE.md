# Version-bound PR files, comments and checks

This document preserves the historical `pr-inspection-20261018` slice and its
terminal evidence. The later inline-card/line-validation implementation and its
separate candidate are recorded in `SOURCE_PR_INLINE_COMMENTS_ACCEPTANCE.md`.
Limitations below describe this frozen slice, not a reclassification of its
1982 passing tests as evidence for later source changes.

## Implemented slice

The existing authorized-repository PR page now reads changed-file pages and
patches, conversation comments, inline comments, check runs and legacy combined
commit statuses. Select a PR, read its details, choose a content category, and
explicitly fetch that category. Pages have independent loading, cancellation,
previous/next, empty and limit states. Reading never posts a comment, opens a
server-supplied URL, downloads CI logs, checks out a repository or invokes a model.

Wide layouts reserve a narrow PR list and give remaining width to the details.
Compact layouts switch between list and details with a real back action, rather
than reserving half of a small viewport for an inactive pane. Shared responsive
columns also collapse hidden compact panes. Patch selection does not implicitly
scroll the enclosing details away from its navigation controls.

This is not complete PR parity. Inbox/URL/filter workflows, reviews/activity,
inline diff annotations and line validation, review chat, investigation drafts,
pin/viewed revision persistence, related stacks, confirmed writes, OAuth, GitLab
and cloud review remain open. Historical inline comments retain their original
commit/path/line/side metadata; displaying that metadata is not a claim that it
matches a valid line in the current patch.

## Design and safety self-check

1. Domain owns revision and result values. Application coordinates source-account
   grants and client ports; Infrastructure implements fixed HTTP endpoints and
   separate file/comment/check parsers. Separate built-in tools use the existing
   common executor. App consumes results through small view models and shared
   controls, not direct HTTP, SQL, filesystem or process calls.
2. Reuse the existing source transport, proxy selection, audit sessions, async
   prevalidation and approval/deadline/cancellation/output pipeline. A shared
   repository-access resolver preserves the older list/detail authorization
   semantics. No schema migration or new credential storage is introduced.
3. Each inspection is bound to the exact locally granted account/repository and
   the PR number/base/head already displayed. Local revision/grant validation
   happens before approval without HTTP. The reader checks live PR revisions
   before and after content HTTP and rechecks grants between requests and before
   publishing. Observed scope/account/revision changes clear all visible content
   categories and disable stale reads until fresh PR details are read.
   Navigation, selection, parent operations and disposal cancel outstanding work;
   cleanup is awaited and generations reject late results. Source scope permits
   only the five exact read tools, even without an explicit allowlist. The new
   tools remain hidden from model descriptors until review-chat isolation exists.
4. Each HTTP response keeps the existing 30-second deadline, 2-MiB decompressed
   cap, bounded JSON depth, no redirects/cookies/retries and local pagination.
   The standard tool has a 30-second deadline for the complete three-request
   inspection; each page has an outer 40-second cancellation window. App's source
   context allows 1 MiB of output. Serialized inspection output exceeding its
   context limit fails explicitly instead of returning a successful truncated
   JSON document. All new handwritten C# files contain one type and stay below
   file/method hard limits. HTTP dispatch is off the UI thread.
5. Deterministic tests cover production HTTP parsing, temporary SQLite grants,
   standard source tools and production App composition/Headless controls.
   Negative coverage includes denied approval, foreign source scope, revoked
   grants/accounts, changes to either base or head, cancellation and late cleanup,
   bounded output, invalid/duplicate/oversized data and reflected credentials.
   Shared-layout tests preserve existing ordinary columns and exercise compact
   hidden panes and the new fixed-primary variant.

## Data completeness and trust boundaries

- File requests use locally constructed `/pulls/{number}/files`, 20 files/page,
  at most 150 pages (GitHub's 3000-file ceiling). A full final allowed page is
  marked limited; an earlier page is not a promise that the whole PR was read.
- Patch states are available, missing, incomplete or too large. Missing does not
  mean binary or unchanged. Available requires consistent unified-hunk ranges
  and additions/deletions. This validates shape/counts, not source-code semantics
  or correctness. Oversized patches retain file metadata but no partial text.
  Limits are 32768 characters and 1000 newlines/file, 65536 retained patch
  characters/page. Incomplete but bounded patches are explicitly labeled.
- Comments use fixed issue or PR-comment endpoints. Each body is bounded to
  16384 characters, the page to 65536 body characters, and a page to 20 records.
  Oversized pages fail visibly rather than silently truncating commentary.
  Text is selectable plain text: no remote Markdown images/scripts/navigation.
- Check runs and legacy statuses are separate categories so legacy CI is not
  silently omitted. Endpoints use the expected head commit; returned head IDs
  must match. Only metadata is returned, never third-party log/output URLs.
- Comment/check pagination is locally bounded to 1000 pages and reports the cap.
  No authenticated request follows Link, raw/content/comment/check URLs from the
  response. Optional comment owner URLs, if supplied, must match the fixed scope.
- Before/after revision checks are not a server-side atomic snapshot. They cannot
  exclude ABA changes or prove comments/checks remained unchanged between calls.
  No immediate revocation guarantee is claimed for a request already dispatched.

## Validation candidate

Current candidate: `artifacts/pr-inspection-20261018`. Earlier source-account,
Git and `pr-reading-20261018` candidates remain frozen and are not reused as
verification of these changes. Intermediate failed build/test reports are kept;
only separately named terminal reports establish the final candidate result.

Initial comment-fixture failures came from public GitHub owner URLs paired with
an intentionally enterprise-prefixed account; fixture URLs now follow that
configured API origin/prefix. New UI markup initially referenced a nonexistent
card wrapper and a style binding without type context; both were corrected to
existing shared cards and explicit reflection binding. Full-page renders exposed
compact detail compression and selection-driven outer scrolling; the shared
layout and explicit selection-scroll behavior were corrected and tested.

Tests use synthetic credentials and controlled handlers, temporary databases and
Headless rendering. They do not verify real GitHub permissions, production TLS,
native windows/high-DPI input, actual EXE restart or a Release package. No user
window/account was operated, and no commit, push or release is part of this slice.

## Terminal validation

- Complete solution Debug build: zero warnings/errors (`build-terminal.log`).
- Five disjoint terminal reports: Tools 644, Infrastructure 557, Agent 146,
  related App 335 and visual-component 300: **1982 passed, zero failures/skips**.
  These are not cumulative totals from the intermediate focused runs or older
  candidates. The App selection covers PR/source accounts, responsive columns,
  automations, navigation, approvals and code previews; it is not all App tests.
- App's expanded selection took about five minutes. Its live test handle and
  testhost/child processes were inspected while awaiting completion; it finished
  successfully without cancellation or restart. A temporary external stack
  diagnostic was uninstalled afterward. No UI-test hang or defect was inferred
  merely from the longer runtime.
- All 2242 selected source/project/resource files and 1426 runtime files match
  their actual pre-test hashes and path sets after the terminal runs. This is
  identity verification, not a restorable source/runtime archive. No ZIP was
  created. `validation-terminal.json` contains numeric counters and scope.
- Architecture tests and a Roslyn audit of present changed/untracked C# files
  found no single-type/file/method hard-limit violations (1796 methods inspected).
  `git diff --check` passes.
- `ui-terminal` contains ten PR frames (grant dialogs, inspection content and
  full pages) plus twelve source-account regression frames. Compact light and
  ordinary dark full PR pages and the ordinary dark inspection frame were
  visually inspected. These are controlled Headless frames, not native-window
  screenshots or full-product visual acceptance.

The overall functional-parity goal remains active. The next PR work still
includes review chat, validated inline diff annotations, inbox/URL workflows and
explicit confirmed feedback, not a relabeling of this read-only slice as parity.
