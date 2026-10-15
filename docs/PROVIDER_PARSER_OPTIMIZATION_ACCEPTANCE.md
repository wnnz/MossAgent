# Provider parser optimization acceptance

## Scope: 2026-10-10

- Anthropic tool blocks now retain call ID, wire name and argument buffer in one
  dictionary entry. Capture and removal no longer maintain three parallel maps.
- Compatible Chat parsing uses one event list per frame instead of allocating
  and copying another list for each selected choice. The list is not cached or
  shared between frames, requests or parsers.
- The two production files decrease from 224 to 213 physical lines. Added tests
  and this document increase total repository lines; this is not a claim that
  all repository code became smaller.

## Design and safety boundaries

- Behavior stays in Providers with the existing Application model-event port.
  No project dependency, package, database schema or public contract changes.
- Tool-name decoding and event order remain unchanged for accepted frames.
  Other choices cannot contribute text or tool calls to the selected response.
- Compatible parsing failures discard all earlier events in that frame, including
  completed calls. Terminal failures cannot flush pending calls on EOF or DONE.
- Anthropic block state stays parser-owned. Unknown indices, repeated stops,
  index reuse and interleaved calls cannot consume another block's arguments.
  Existing invalid-argument exceptions are preserved, not converted to success.
- HTTP/SSE cancellation, encoding checks and size limits remain upstream;
  authorization, approval, timeout, output limiting and audit remain downstream.
  No new execution path or shared mutable state is introduced.
- Every changed handwritten C# file contains one type. Production files are 94
  and 119 lines; the longest changed-file method is 32 lines.

## Deterministic verification

- Fifteen new characterization cases passed against the original parsers before
  the refactor. An initial test assertion incorrectly required the exact
  `JsonException` type; allowing its `JsonReaderException` subtype corrected the
  assertion without changing production behavior.
- Seventy-four targeted Anthropic/Compatible tests pass after the refactor,
  including existing HTTP streaming, cancellation, argument/count/identity
  limits, incomplete responses and safe malformed-frame rejection.
- New cases cover interleaved block ownership, replacement/reuse, independent
  parsers, frame event order, whole-frame failure and immutable prior results.
- Full solution build passes with zero warnings and zero errors. All eight test
  assemblies pass: 4,258 tests, zero failures and zero skips. Shared-component
  and headless visual tests are included; these are not native screenshot QA.
- Focused whitespace verification, one-type/file and file/method size checks,
  and tracked-file diff whitespace checks pass.

## Allocation measurements

The repository-external synthetic harness runs under .NET 10 Release JIT,
warms each scenario 2,000 times and takes five samples of 20,000 operations.
It reuses tool-name metadata and validates event shapes on every operation.
Steady allocation readings from samples 2-5 are:

| Scenario | Before bytes/op | After bytes/op | Reduction |
| --- | ---: | ---: | ---: |
| Compatible frame, one selected entry | 472 | 384 | 18.6% |
| Compatible frame, four selected entries | 928 | 576 | 37.9% |
| Anthropic parser, one tool block | 1,496 | 1,096 | 26.7% |

These are local parser allocation measurements, not whole-application memory,
latency or throughput guarantees. Timing samples fluctuate and are not used to
claim a speedup. No live provider account, paid API or native UI was exercised
for this internal refactor; markup, styling and release/JIT settings are unchanged.

## Evidence

Repository-external logs, TRX files, original parser snapshots and benchmark:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-parser-optimization-20261010-01`.

## Gemini follow-up: 2026-10-11

### Design and safety self-check

- Providers retains all behavior through the existing Application model events;
  no port, dependency, package, database, UI or publication setting changes.
- Text-only and metadata frames reuse the mapper's independently owned snapshot,
  instead of allocating and copying a second event list. This happens only after
  failure, finish-state and signed-continuation checks. No list is cached or reused
  by a later frame or another parser; previously returned events stay unchanged.
- Tool counting uses one frame-local counter instead of scanning all previously
  mapped text/tool events at every call. Thought and text parts do not consume
  the 128-call budget. Tool names/native identities are decoded once instead of
  allocating the same string repeatedly for validation and publication.
- Mixed frames retain call buffering and ordered text projection. Malformed,
  blocked, oversized or post-finish frames still fail closed, discard that frame
  and clear pending calls. Native identities, argument cloning, continuation
  signatures and whole-stream budgets retain their original checks.
- SSE cancellation, encoding/size limits and safe failures stay upstream; tool
  authorization, approval, timeout, output limits and audit remain downstream.
  Nothing calls a tool, uses a user database or invokes a paid provider.
- Changed source/test files each contain one type and remain below the 500-line
  file and 80-line method limits. No abstraction or mutable global state is added.
- Eleven new characterization cases pass against the original implementation
  before editing production. They cover snapshot identity/order, empty frames,
  signed text, mixed 128/129-call frames, late invalid parts, 512/513-character
  native identities and escaped strings that outlive JSON document disposal.
  An initial new-test compile error referenced `Name` instead of `ToolName`;
  correcting that assertion did not change product code or disable warnings.

Evidence, original source snapshots and synthetic checks remain outside Git:
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-gemini-parser-optimization-20261011`.
This is an internal optimization slice, not whole-product or visual acceptance.

### Measured allocation and focused verification

- The final Gemini filter passes 199/199 cases, including the eleven new cases
  and existing HTTP/SSE, cancellation, malformed/blocked/incomplete responses,
  call/argument/signature/identity budgets and continuation replay boundaries.
- The solution build has zero warnings and errors. Scoped whitespace formatting
  verification passes. Roslyn confirms one type in each changed C# file: mapper
  156 lines, parser 79 and new tests 156; the longest method is 30 lines.
- The subsequent complete solution run ends successfully: all eight assemblies
  pass 4,434/4,434 tests, with zero failures or skips. Counts are Agent 63, App 3292,
  Visual 8, Tools 316, Windows 137, Browser 76, Infrastructure 165 and Providers
  377. Raw TRX and the verified assembly counters remain under `regression`.
  The shared-component/Skia gates are included, not a new native UI comparison.
- A repository-external .NET 10 Release JIT harness links the two original source
  snapshots under a separate namespace and compares them with the live refactored
  types. Both use the same unchanged tool/continuation buffers. Each operation
  creates a parser, reads one synthetic frame, verifies STOP and drains completion.
  Tools/payloads are prepared outside measurement. Each scenario warms 2,000
  operations, then takes five 10,000-operation samples with per-operation event
  shape/count checks. All five allocation readings and checksums are stable.

| Synthetic parser operation | Before bytes/op | After bytes/op | Reduction |
| --- | ---: | ---: | ---: |
| Metadata frame | 992 | 960 | 3.2% |
| One text part | 1,632 | 1,504 | 7.8% |
| Four text parts | 2,472 | 2,424 | 1.9% |
| 128 interleaved text/tool parts | 259,760 | 224,576 | 13.5% |

These measurements concern this parser operation, not whole-application memory
or UI speed. Timing fluctuates and overlaps full regression work, so it is not
used to claim a latency improvement. This removes redundant processing rather
than claiming a net decrease in all source/test/documentation lines. The harness,
original copies, allocation CSV/logs and TRX stay outside the repository.
The final scoped tracked/untracked whitespace and diff checks pass; the index
remains empty and no QA host remains. All unrelated existing worktree changes
are preserved. No commit, push, AOT conversion or publish is performed. Real
remote provider/account behavior and whole-product fidelity remain unverified
by this optimization; the broader product goal remains active.

## SSE line-reader follow-up: 2026-10-11

### Design and boundaries

- Providers retains ownership through the existing SSE reader. No public contract,
  project dependency, tool execution path, database, UI or publishing change.
- Decoded chunks are scanned for CR/LF boundaries and appended as spans rather
  than character by character. Complete lines are copied directly into independently
  owned strings; unfinished segments use a lazily created, iterator-owned buffer.
- Strict UTF-8 decoding still happens before scanning each chunk. Only the initial
  BOM is removed. CR, LF, split CRLF, empty lines and incomplete final lines retain
  existing behavior. The 1 Mi-character line and 2 Mi-character event limits stay
  in force, including supplementary scalars counted as UTF-16 characters.
- Cancellation checks, immediate delivery, immutable returned strings and the
  caller's ownership of the input stream are unchanged. No shared mutable state
  or approval bypass is introduced; downstream tool safety is unaffected.
- Production code grows from 55 to 65 lines rather than adding a buffering
  abstraction. Its one method is 55 lines, below the 80-line hard limit. The new
  test file has 158 lines, one type and a longest method of 20 lines. This removes
  unnecessary per-character work and copies, not a net reduction in repository lines.

### Verification and measurements

- Thirty new characterization cases pass against the original reader before the
  production edit, then pass with the optimized reader. They cover fragmented UTF-8,
  initial/interior BOMs, all line endings and buffer boundaries, exact/excessive
  character limits, immediate delivery, cancellation and malformed UTF-8.
- The complete Providers project passes 499/499. The focused App settings/runtime
  proxy chain passes 63/63, including authenticated HTTP/SOCKS5 and direct routing.
  The solution build has zero warnings and errors. Scoped whitespace formatting,
  one-type/file, method/file limits and trailing-whitespace checks pass. This slice
  does not rerun every unrelated test project or claim new native UI acceptance.
- An external .NET 10 Release JIT harness links the original snapshot beside the
  actual new source. With seed 1739, 250 generated valid UTF-8 inputs are compared
  at six chunk sizes: 1,500 comparisons have identical output and stream ownership.
- Each measurement consumes a new synthetic stream and checks every returned line.
  Both readers warm 2,000 operations per scenario, followed by five 1,000-operation
  samples. Allocation readings include the same fixture and async enumeration cost.

| Synthetic stream | Before bytes/op | After bytes/op | Reduction |
| --- | ---: | ---: | ---: |
| 64 short lines | 16,074 | 15,970 | 0.6% |
| One 3,006-character complete line | 35,018 | 18,938 | 45.9% |
| One 8,198-character fragmented line | 82,202 | 81,698 | 0.6% |

Allocation samples are stable apart from a 0.048-byte/op fluctuation in one
fragmented-line sample. Timings are retained but not used to promise an application
speedup. These are synthetic reader measurements, not whole-process memory or UI
latency. Short and fragmented lines do not receive the complete-line allocation gain.

Evidence, original source, the comparison/measurement harness and TRX stay outside
Git: `C:\Users\wzheng\AppData\Local\Temp\MossAgent-sse-reader-optimization-20261011`.
The earlier isolated native proxy host and server close normally with exit code 0;
their partial native acceptance remains separate and incomplete. No user settings,
paid API, screenshot/UI interaction, staging, commit, push, AOT or publish is used
for this optimization. JIT debugging and Windows x64 single-file publishing remain
unchanged; the broader product goal remains active.
