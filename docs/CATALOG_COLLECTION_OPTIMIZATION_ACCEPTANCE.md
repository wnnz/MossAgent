# App catalog and collection optimization

## Design self-check

- App owns both helpers. Existing configuration repository ports remain the only
  data access; dependency direction and UI control wrappers are unchanged.
- Collection synchronization retains equal rows and occurrence ordering. A shared
  prefix is skipped; append, tail removal and unchanged snapshots need no membership
  set. Mixed changes still use the existing membership/removal/move algorithm.
- Model loading filters enabled providers and matching enabled models directly from
  indexed repository snapshots, without per-provider closures and LINQ iterators.
  Output order, duplicate records, independently created picker state and stable
  agent-option label sorting remain unchanged.
- No new I/O, approvals, execution policy, timeout, task ownership, persistence,
  secrets, JSON serialization, publication or appearance behavior is introduced.
  Existing cancellation checks and cancellable repository waits remain in place.
- Each handwritten C# file contains one type. Changed methods stay below 40 lines.
- Focused tests cover every transition among 121 short duplicate-value sequences
  (14,641 pairs), nulls, equal references, move/add/remove notifications, disabled
  and foreign models, ordering, cancellation and prior-snapshot isolation.

## Baseline

The new focused tests ran against the pre-optimization implementation on Windows
x64, .NET 10 Debug/JIT. Behavioral tests passed; two allocation guards failed:

| Warm scenario | Iterations | Bytes before | Bytes after | Reduction |
| --- | ---: | ---: | ---: | ---: |
| Append/remove a tail item from 32 rows | 1,000 cycles | 1,680,000 | 192,000 | 88.57% |
| Read 16 enabled providers with 64 disabled models each | 1,000 reads | 5,048,000 | 752,000 | 85.10% |
| Synchronize an unchanged 32-row snapshot | 10,000 reads | 0 | 0 | Already allocation-free |

Measurements use thread allocation counters after 100 warm-up iterations. The
catalog fixture returns completed cached tasks, excluding repository allocation
and network/disk costs. Collection measurements include required observable
notifications; no-change synchronization was already allocation-free on .NET 10.
These are bounded microbenchmarks, not claims about total application performance.

## Verification

- Focused catalog, picker, sidebar, agent and subagent regressions: 530/530 passed.
- Full solution build: zero warnings and errors.
- `dotnet format whitespace` verification for the five changed C# files: passed.
  Scoped `git diff --check` also passed.
- Changed files: at most 176 lines, one type per file; changed methods below
  40 lines. No shared appearance or native component boundary was modified.
- Full App regressions: 3,622/3,623 passed. The sole failure was an `IOException`
  at `WorkspaceConversationRestartTests.cs:75`, while removing its temporary
  directory after the model/agent restoration assertions and shutdown succeeded.
  Three subsequent isolated runs of that class passed 3/3 each, without production
  or cleanup-code changes. Whole-suite cleanup reliability is not established;
  those retries do not convert the failed full-suite run into a pass. All three
  allocation measurements were identical in focused and full-suite runs.
- This slice does not include a fresh native-window screenshot, live provider
  calls, Release/AOT publication or completion of the separate native subagent
  lifecycle acceptance. The prior temporary lifecycle host was closed normally
  before rebuilding (exit 0, parent/child request counts 0/0).

Evidence is repository-external:
`%TEMP%\MossAgent-catalog-collection-optimization-20261011`.

## Provider/editor identity lookup cleanup (2026-10-12)

### Design self-check

- App's existing provider/model catalog owns current-snapshot identity lookup;
  repository I/O stays behind `IConfigurationRepository`. No dependency changes,
  new service, runtime package, cache or persistence format are introduced.
- Provider and model editors reuse the lookup instead of repeating capturing
  LINQ predicates. Row actions resolve the current provider once, and model
  deletion checks reuse the editor's existing target-availability validation.
- Null and absent identities return no target. First-occurrence ordering,
  disabled-provider editability and provider/model identity pairs stay unchanged;
  a missing model in the first matching group never falls back to a duplicate
  group. Moves and removals take effect immediately without cache invalidation.
- Existing catalog-read deadlines, cancellation, disposal, mutation ownership,
  stale-result rejection, draft preservation and unavailable-target checks remain
  in the owning editors. No tool, authorization, approval or secret handling changes.
- The three production files have 151, 487 and 494 lines; the test file has 167.
  Each has exactly one type. Changed methods stay below 40 lines; the largest
  existing method remains 68 lines, below the 80-line hard limit.

### Allocation baseline

The pre-change behavioral cases pass, while all four new allocation guards fail.
The final seven lookup cases pass, including the added current-order regression.
Measurements use 1,000 warm-up iterations and 10,000 synchronous checks on the
current thread, with all fixtures constructed outside the measured region.

| Warm check sequence | Bytes before | Bytes after |
| --- | ---: | ---: |
| Provider commands/availability, present target | 5,120,000 | 0 |
| Provider commands/availability, missing target | 5,120,000 | 0 |
| Model commands/availability, present target | 26,800,000 | 0 |
| Model commands/availability, missing target | 10,000,000 | 0 |

These are binding/command lookup measurements, not total UI allocations or an
end-to-end latency benchmark. Catalog loading and required observable events are
outside the measured region.

### Verification

- Full App suite: 3,694/3,694 passed, including stale and duplicate rows, ownership,
  unavailable targets, catalog refresh, draft, read deadline and disposal coverage.
- Architecture constraints: 7/7 passed. The rebuilt repository-external Roslyn
  audit confirms the file, method and one-type limits above.
- Solution build after tests finish: zero warnings and errors. An earlier build
  overlapped the full suite and failed copying its locked App DLL; that failure
  is retained separately and required no production or test-policy changes.
- Scoped whitespace verification and diff checks pass. No AXAML or shared visual
  appearance changes, fresh native-window screenshot, paid/live provider request,
  full-solution test run, commit, push or publication is claimed. JIT debugging
  and Windows x64 single-file release settings remain untouched.

Evidence and the per-turn production-file baselines are repository-external:
`%TEMP%\MossAgent-catalog-lookup-cleanup-20261012-083839`.

## Model search projection cleanup (2026-10-13)

### Design self-check

- App owns group filtering and picker projection. Existing configuration ports,
  model availability checks and shared collection reconciliation stay in place;
  there are no new dependencies or cross-editor caches.
- Group filtering visits each member once. A changed partial match lazily builds
  immutable membership; equivalent matches retain the previous group. Whole and
  empty matches retain their original semantics, including empty provider groups.
- The picker compares published groups by occurrence before building a changed
  snapshot. Unchanged results allocate no projection list; a shortened matching
  prefix only removes the tail. Mixed changes use existing observable synchronization.
- No I/O, tool execution, approval, authorization, timeout, cancellation, persistence,
  secret handling, JIT or Windows x64 single-file publication behavior changes.
  Existing tests still reject stale loads, disabled models and foreign identity pairs.
- Changed production files have 64 and 453 lines with one type each. Changed
  production methods have at most 22 lines; new test methods have at most 42,
  below the 80-line hard limit. Regressions cover duplicate row occurrences,
  shortened and expanded prefixes, immutable earlier snapshots, empty groups,
  unchanged collection notifications, selection and rendered focus.

### Allocation baseline

The new five behavioral cases pass against the pre-change implementation; all
eight allocation guards fail. The final focused picker/catalog/collection run
passes 168/168 cases, including those guards. Measurements use thread allocation
counters after 100 warm-up searches, with fixtures outside the measured region.

| Equivalent search result | Picker bytes before | Picker bytes after | Group bytes before | Group bytes after |
| --- | ---: | ---: | ---: | ---: |
| Partial model matches (`target` / `unique`) | 2,944,000 | 120,000 | 208,000 | 0 |
| Whole model matches (`shared` / `model`) | 5,920,000 | 120,000 | 456,000 | 0 |
| Empty matches (`absent` / `missing`) | 2,440,000 | 120,000 | 176,000 | 0 |
| Provider matches (`fixture` / `FIXTURE`) | 736,000 | 120,000 | 24,000 | 0 |

Each row measures 1,000 alternating synchronous searches. The picker fixture has
12 providers and 32 models per provider; the group fixture measures one provider.
Required property notifications are still included in picker allocations. These
are warm projection microbenchmarks, not total App memory or latency measurements.
Changed results still allocate their necessary immutable/observable snapshots.

### Verification

- Full App regressions: 4,067/4,067 passed, including all 13 new cases, the four
  light/dark search-focus cases and 13 shared visual-component boundary cases.
- Separate visual-layout regressions: 12/12 passed after the final solution build.
- Architecture constraints: 7/7 passed. Scoped syntax auditing confirms one type
  per file, the production sizes above and all changed methods below hard limits.
- Solution build: zero warnings and errors. Scoped whitespace verification passes
  after correcting one test initializer's line wrapping; scoped diff checks pass.
- This is not a full-solution test run. Existing native window-restore/reflow
  acceptance gaps are unchanged; passing headless tests does not resolve them.

No AXAML, shared appearance or native-window behavior is changed in this slice.
Headless shared-view focus, selection and light/dark tests pass in the focused run;
fresh native screenshots, paid/live provider requests and publication are not claimed.

Evidence and production-file baselines are repository-external:
`%TEMP%\MossAgent-model-search-cleanup-20261013-075416`.
