# Context building optimization acceptance

## Scope and boundaries

`ModelContextWindowBuilder` reads conversation turns from newest to oldest and
materializes only the groups it examines before reaching the context budget.
It no longer builds arrays and a group list for discarded older history.
System messages still reserve the same budget and precede conversation messages
in their original order. The first turn that cannot fit still stops selection;
the builder never skips it to include an older, smaller turn.

`SubagentConversationContextBuilder` finds the newest twelve relevant messages
by index, then renders them in conversation order. It keeps the same eligible
roles, whitespace filtering, untrusted observation labels, 12,000-character
limit, truncation marker and surrogate-pair boundaries. Empty context returns
null without constructing a string builder.

Both changes belong to Agent and use existing model contracts. There are no new
dependencies, caches, persistence formats, provider parameters or UI styles.
Authorization, approvals, cancellation, tool execution, signature ownership and
JIT/Windows x64 single-file publishing settings are unchanged.

## Regression evidence

- The three focused context/continuation suites pass 33/33 tests.
- New cases cover mixed system placement, duplicate message identity, independent
  snapshots, empty/system-only input and an older turn with oversized metadata.
- Signed continuation content remains intact; oversized latest metadata remains
  rejected. Existing tool-call/result and Unicode truncation tests still pass.
- Before production edits, the new allocation cases failed at 117,616 bytes for
  2,000 old turns and 667,064 bytes for 10,000 old turns. Both now satisfy the
  32,000-byte per-build ceiling.
- A suffix-access probe drops from 10,036 reads to at most 72 reads when twelve
  eligible messages, interleaved with excluded messages, follow discarded history.
- A repository-external comparison of both original implementations against the
  new implementations matches 1,000/1,000 deterministic mixed histories, including
  tool metadata, signed continuations, exceptions and several context sizes.
- The solution builds with zero warnings/errors. All eight test assemblies have
  passing results, totaling 5,511/5,511 tests. The initial parallel run timed out
  in one Windows shell-process ownership case; its three-case suite and the full
  137-case Windows assembly pass on isolated reruns without code changes there.
- Scoped whitespace verification and diff checks pass. All five touched C# files
  contain one handwritten type and remain below 250 physical lines.

## Local allocation probe

Release-mode measurements on the same synthetic inputs:

| Case | Before bytes/call | After bytes/call |
| --- | ---: | ---: |
| Model context, 10,000 old turns, narrow budget | 667,100 | 4,512 |
| Model context, twelve old turns, all fit | 2,264 | 1,800 |
| Shared subagent context, 10,000 old turns | 27,296 | 26,824 |

The narrow-budget case reduces temporary allocations by about 99.3%; the system
message pass still visits all input messages. Shared-context output allocation
remains bounded while locating the eligible suffix no longer visits discarded
history. These are local fixture measurements, not universal latency guarantees.

The comparison project and copied baseline implementations remain outside the
repository under `%TEMP%/MossAgent-context-optimization-20261012-191707`.
No new paid-provider, account or native visual acceptance is claimed by this
pure in-memory refactor.
