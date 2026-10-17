# MCP reconnect and catalog recovery

## Scope and design review

- App owns the settings command and publication of repository snapshots. The
  existing `IMcpConfigurationRepository`, `IConfigurationRepository`,
  `IMcpManager` and tool catalog remain the boundaries; no new runtime port,
  persistence schema, provider adapter or platform dependency is introduced.
- Reconnect already resets the configured MCP connections. It now also reads
  both server and proxy catalogs before publishing a successful refresh. It
  does not save/delete settings or execute a tool or model request.
- The reconnect command has cancellation linked to the settings lifetime.
  Existing MCP connection deadlines, tool approvals, authorization, audit and
  output limits are unchanged. Cancelling a reconnect is not a transaction
  rollback of connections already changed by the manager.
- Busy commands disable competing settings actions; an open editor blocks
  reconnect, preserving its draft. Shared control wrappers and resource labels
  remain in use. The cancel action replaces, rather than extends, the reconnect
  button's header slot. No colors, icon sizes or shadows are added per view.
- At the initial checkpoint, the largest changed C# file is 458 lines; each
  handwritten file still has one type. Architecture tests enforce the
  repository-wide file/type limits.

## Reproduced failure and correction

The old reconnect command reloads only the manager, not the visible catalogs.
All three initial regression cases fail: retry after an initial repository error
leaves an empty list, a failed read is hidden by a success message, and changed
server records never reach the page.

The corrected command reloads both snapshots and reports success only after
their reads finish. Failed/cancelled reads retain the previous snapshot and
offer another reconnect. Generation checks reject an older initial load;
lifetime cancellation and disposed-state checks suppress late publication.
Connection errors remain distinct from a repository failure or cancellation.

## Verification: 2026-10-15

- Eight deterministic command tests cover initial recovery, changed catalogs,
  connection diagnostics, cancellation during connection and repository reads,
  disposal, atomic publication after proxy-read failure, and an open draft.
- Four production-page cases in light/dark at 728/980px exercise real keyboard
  activation, busy/disabled state, Cancel and successful retry. They use Avalonia
  Headless, not native-window automation or a new desktop screenshot.
- A production-composition test persists an unavailable loopback HTTP endpoint,
  reconnects and observes removal of its stale tools, then restores the endpoint
  and reconnects. Only after an explicit synthetic approval does the recovered
  tool run. The real HTTP response, two model requests, headers, conversation,
  completed audit and unchanged final configuration are verified. No paid API,
  real user settings, arbitrary endpoint or live approval setting is used.
- The final related App run passes 73 cases, component guards 27, complete pixel
  tests 136, MCP protocol/tool tests 56 and architecture tests 7: 299 distinct
  cases, zero failures/skips. Earlier focused/isolated runs are not added again.
- The first broader 72-case run records two 30-second HTTP completion timeouts.
  Both affected branches pass the four-case isolated run and the final broader
  73-case run. Their cause is not isolated; the failed report is retained rather
  than described as repaired by rerunning.
- Solution build has zero warnings/errors; scoped formatting and whitespace
  checks pass. This is not a new complete eight-assembly run. The preceding
  complete checkpoint's allocation failure remains recorded separately in
  `UI_COMPACT_LAYOUT_ACCEPTANCE.md`.

Evidence is outside Git at `%TEMP%/MossAgent-mcp-reload-tests`: `baseline.trx`,
`mcp-regressions.trx`, `real-reconnect.trx`, `mcp-final.trx`, `visual-final.trx`,
`tools-mcp-final.trx`, `component-guards-final.trx` and `architecture-final.trx`.
Native screenshots remain user-postponed. This bounded recovery scenario does
not prove every configuration race, native recovery or whole-product/Codex
visual equivalence. Nothing is staged, committed, pushed, published or installed.

## Mutation and editor-lifetime follow-up (2026-10-15)

Eight additional baseline cases reproduce late writes/reconnects after disposal,
saved data replacing newer input, a closed editor reopening on connection failure,
and an externally removed record being silently recreated. The failed baseline
is retained in `%TEMP%/MossAgent-mcp-mutation-tests/baseline.trx`.

The App now reuses guarded async commands and its existing editor-revision base.
Preflight reads, writes, deletes and the subsequent manager reload share the
command/lifetime cancellation token. Checks between awaits prevent a new write
after cancelled preflight and prevent reconnect/publication after disposal.
A write already started can still commit; cancellation is not presented as a
rollback. The saved configuration remains inspectable after cancelled reconnect.

Catalog reads/publication and immutable editor snapshots have separate small
types. Selection feedback during catalog publication cannot reset the editor.
Canonical row/proxy references update without loading over typed fields, and a
stable new-record identity lets a later save update the same created record.
Revision ownership prevents an older result from closing/reopening a newer or
dismissed editor. Closing the dialog also clears pending delete confirmation.
Missing edit targets are rejected at preflight, not silently recreated; this is
not a cross-process compare-and-swap guarantee between validation and SQL write.

Dependencies, repository contracts/schema, tool approval/authorization/audit,
manager ownership, JIT/JSON and Windows x64 single-file publishing stay unchanged.
No direct I/O enters a view, no new static service state is introduced, and each
handwritten C# file retains one type. The main settings model is now 454 lines;
the catalog and profile factory are separate responsibilities, not partial files.

Final verification adds 22 cases: 13 deterministic mutation/draft cases, eight
production-page keyboard cases in both themes at 728/980px, and one production
stdio process case. That last case starts a real hanging MCP server and child;
disposing settings cancels the save/reconnect and releases both processes before
the service container is disposed. The committed configuration remains present,
no model request/tool execution occurs, and no late UI notification is published.

The final related App group passes 122, complete pixels 136, MCP protocol/tool
checks 56 and architecture checks 7: 321 distinct cases, zero failures/skips.
The focused and isolated results are subsets, not added again. Solution build
has zero warnings/errors; scoped formatting and tracked/new-file whitespace pass.
This is not a complete eight-assembly run or a native-window acceptance sweep.

Final reports are `%TEMP%/MossAgent-mcp-mutation-tests/app-related-final.trx`,
`visual-final.trx`, `tools-final.trx` and `architecture-final.trx`.
`mutation-page-boundaries.trx` and `owned-process-final.trx` retain intermediate
focused evidence. Existing user changes remain intact. No staging, commit, push,
release, installation, user-window operation or native screenshot occurs.
