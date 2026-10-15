# Search refresh optimization

## Design and required self-check

- App owns synchronous in-memory search projection and subscriptions. Existing
  view models and collection contracts remain; no module dependencies change.
- The existing task-search result collection can publish changed positions rather
  than clearing and rebuilding equal snapshots. Source ordering, title matching,
  pinned metadata and duplicate snapshots retain their current meaning.
- Sidebar search can observe collection deltas instead of detaching and attaching
  every project after each insertion, removal, replacement or move. Reset still
  rebuilds subscriptions; duplicate group references must stay observed until
  their final occurrence is removed. Disposal releases every subscription.
- No I/O, model requests, tool calls or storage migration is introduced. Existing
  authorization, approval, cancellation, timeout, output and audit ports are not
  changed or bypassed. No new global state, service, package or cache is needed.
- The two production files remain below 250 lines with one handwritten type each;
  methods must remain below the 80-line hard limit.
- Focused tests cover equivalent snapshots, filtered-out changes, metadata
  replacement, ordering, duplicate entries, all collection actions, reset and
  disposal. Existing headless workspace tests cover real search/clear bindings,
  unchanged draft/selection and zero model requests in both themes.
- JIT debug and Windows x64 single-file publishing remain unchanged. Existing
  dirty work is retained; no commit, push or publish is requested.

## Verification

- Twelve new task-search cases cover snapshot identity, hidden mutations,
  changed metadata, delta notifications, reordering, duplicates and six query
  variants with mutation sequences. Before the production change, six fail on
  redundant resets/replacement and six reference-behavior cases pass.
- Five new subscription cases pass before and after the refactor: duplicate
  references, replacement with an existing group, move, reset and owner isolation.
  Existing add/remove/replace, pinned/recent results and disposed-command cases
  remain passing. Removed groups and disposed owners cannot publish late results.
- The final focused search/sidebar/task-navigation run passes 300/300 cases.
  Actual headless workspace search and clear interactions pass in both themes,
  retaining selected project/task and draft text without model requests.
- The solution build passes with zero warnings and errors. All eight test
  assemblies run serially with their existing internal parallelism: 4813/4813
  pass, with zero failures or skipped cases. Counts are Agent 83, App 3478,
  Visual 8, Browser 76, Infrastructure 171, Windows 137, Providers 377 and Tools 483.
- Scoped `dotnet format whitespace --verify-no-changes` and repository
  `git diff --check` pass. Roslyn audits confirm one type per changed/new C# file,
  85/114 production lines, 175/116 test lines and a longest method of 33 lines.
- No native window or screenshot comparison is run for this optimization-only
  slice. No visual markup, shared appearance, provider configuration or publishing
  setting changes. Headless interaction coverage is not native pixel acceptance.

## Allocation evidence

A repository-external .NET 10 Windows Release harness links either the saved
original task-search source or the new source, using the same Domain and Toolkit
assemblies. Each workload starts with 200 immutable synthetic tasks, warms up
256 calls, then measures 1000 calls with `GC.GetAllocatedBytesForCurrentThread`.
The filtered workloads retain 100 rows; inputs exist before measurement. Original
and new outputs have identical counts and ordered ID/title/status SHA-256 checksums.

| Workload | Before bytes/call | After bytes/call | Reduction | Result notifications before/after |
| --- | ---: | ---: | ---: | ---: |
| Equal task upsert | 14552 | 88 | 99.40% | 201000 / 0 |
| Filtered-out task upsert | 7437.44 | 86.528 | 98.84% | 101000 / 0 |
| Equivalent query with changed outer whitespace | 7452 | 92 | 98.77% | 101000 / 0 |

These are allocation measurements for the specified unchanged-result workloads,
not whole-app memory or latency measurements. Query/command-state notifications
remain; only unnecessary result-collection events disappear. Sidebar subscription
savings are not included in this table. An initial harness used a platform-neutral
target and emitted CA1416 warnings; the final harness targets Windows and builds
with zero warnings/errors. Reported measurements come from the final run.

Sources, benchmark projects, saved original source, logs, patches, audits and TRX
files remain outside Git in
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-search-optimization-20261011-143413`.
Final counters and measurements are `full-counters.json` and
`allocation-comparison.json`. Existing dirty work is retained and the index remains
empty. This slice does not claim full-product visual parity or complete the wider
MossAgent goal.
