# Agent tool catalog optimization acceptance

## Design self-check

1. Agent owns model-visible tool projection and child delegation restrictions.
   Keep the existing Application/Tools.Abstractions dependency direction; no
   concrete catalog dependency or UI change in production.
2. Reuse `IToolCatalog` and `IToolExecutor`. Read the current catalog each model
   turn and child invocation, without caching dynamically registered MCP tools.
3. Child requests still enter the standard executor for schema, capability/path,
   approval, timeout/cancellation, output limits and audit. Reject recursive
   delegation case-insensitively before forwarding. Preserve the task identity,
   authorized roots, shared file-version store and parent cancellation token;
   strip only conversation/delegation/progress fields already excluded before.
4. Change two small existing methods, without introducing production types or
   state. New tests retain one handwritten type per file and remain below the
   size limits. Do not change JIT, JSON, packaging or persistence.
5. Prove current-catalog membership, removal/registration, recursive/unknown-tool
   rejection, executor forwarding, approval/output limiting, collection fallback,
   duplicate/order retention and earlier request independence after replacement
   or in-place catalog updates. Guard allocations
   and redundant copying with deterministic regressions, not timing assertions.

## Changes

- `SubagentToolGateway.ExecuteAsync` checks the requested name against the
  unprojected catalog after excluding `subagent.run`. It no longer builds a whole
  filtered descriptor array just to perform a membership test. The model-visible
  child descriptor projection remains unchanged.
- When delegation is enabled, `AgentRunner` directly copies the catalog rather
  than creating a capturing LINQ predicate that accepts every descriptor. Keep
  independent request snapshots: `IReadOnlyCollection` does not guarantee that
  another catalog implementation cannot mutate its underlying list or array.
  Disabled delegation retains the same case-insensitive filtering, order and
  duplicates, using a non-capturing predicate.
- The runner reads the catalog once per turn. Child calls use the current
  catalog, so registration/removal does not leave stale cached permissions.
  Published arrays are not mutated by either method.

## Failing baseline and correction

The child allocation baseline failed: 128 membership checks allocated 300,032
bytes, versus 274,432 bytes for the old filtered membership expression alone.
An initial attempt to reuse list snapshots was rejected after two focused
failures proved that aliasing a mutable catalog can rewrite earlier requests.
The final implementation keeps the necessary snapshot copy and removes only
the no-op filtering around it. No catalog ownership assumption is added.

The allocation regression
requires child execution, including its context clone, to allocate less than
half of that redundant projection baseline on the same thread after warmup.
This demonstrates removal of that allocation, not an application-wide speedup
or a benchmark of catalog generation inside the real `ToolRegistry`.

## Validation scope

The final focused `Subagent|AgentDescriptorProjection|Architecture` run passed
84/84 cases, including 16 new catalog/gateway regressions. The final Agent test
assembly passed 112/112. Scoped whitespace verification and the final solution
build passed, with zero build warnings/errors.

The Roslyn audit
found one type per changed/new C# file; the new test methods are at most 28 lines.
Existing long methods remain below the 80-line hard limit.

The first solution-wide run passed the other seven assemblies, but an unrelated
sidebar allocation assertion observed 1,408 bytes instead of zero. Its isolated
six-case rerun passed without changing sidebar code. That first App run was
manually stopped after 5 minutes 40 seconds (3,451 passed, one failed), so it is
not reported as a completed full-suite acceptance. Standalone diagnostic App
tests subsequently passed 3,739/3,739. An overlapping rebuild encountered the
test worker's DLL locks; after the worker exited, the final solution build passed.
The final-build App assembly then passed 3,739/3,739 with diagnostic timeout
enabled and no dump generated. No sidebar or test-runner implementation was
changed to obtain the passing reruns.

Completed passing assembly runs in this slice: Agent 112, App 3,739, App.Visual
8, Browser 76, Platform.Windows 137, Infrastructure 196, Tools 483 and Providers
516 (5,267 cases in total). These are completed assembly runs, not a claim that
the interrupted first solution-wide command returned success.

This slice changes no visual component or native interaction. It does not claim
new screenshot, real-account, paid-provider or pixel-parity acceptance. The
ongoing product goal and preceding native evidence remain separate.
