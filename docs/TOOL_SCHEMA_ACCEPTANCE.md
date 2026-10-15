# Tool input-schema validation

## Scope and design review

This slice replaces the incomplete handwritten assertion evaluator with
JsonSchema.Net 8.0.5 (MIT). It is a correctness and maintenance refactor, not a
claim that all schema dialects or arbitrary remote schemas are supported.
The validation entry point shrinks from 236 to 56 physical lines; explicit
security policies and compatibility handlers live in separate small types.
This does not imply a reduction in every file or total dependency footprint.
Dead string-length error resources and the old input-length branch are removed.

The five engineering questions are answered as follows:

1. **Owner/dependencies:** Tools.BuiltIn owns validation within the existing
   ToolExecutor. Domain, Application, Agent and UI dependency directions stay
   unchanged. No Avalonia, network or storage access is added to the validator.
2. **Existing contracts:** Built-in tools and MCP adapters still expose their
   schema through ToolDescriptor and run through IToolExecutor. No alternate
   execution or approval path is introduced.
3. **Safety:** validation completes before capability checks, path preflight,
   approval and invocation. Invalid schemas return `tool_schema_invalid`;
   invalid inputs return `invalid_arguments`. Cancellation returns
   `tool_cancelled` when auditing has already started. Audit-start failures
   retain the existing fail-closed `audit_unavailable` behavior. Existing
   tool-execution deadlines, artifact/output limits and approval policy remain.
4. **Size/lifetime:** each handwritten C# file contains one type. Local dialect
   handlers and their cancellation token are created for each call; no global
   registry mutation, mutable global state or unbounded schema cache is added.
5. **Tests:** assertions cover accepted and rejected inputs, pre-approval
   rejection, no tool invocation, bounded patterns/structure, cancellation,
   audit completion and the real workspace/model/MCP/SQLite restore chain.

## Compatibility and limits

- Default dialect: draft 2020-12. Explicit draft 06, draft 07, 2019-09 and
  2020-12 declarations are accepted. Mixed nested dialects are rejected.
- Common assertions now include additional-property schemas/refusal, nested
  properties, `const`, compositions, conditionals, array counts/uniqueness and
  local `$ref`. Unsupported formats/vocabularies are not a security guarantee.
- Only acyclic same-document JSON Pointer `$ref` is supported. External/file
  references, anchor references, dynamic/recursive references and nested `$id`
  resources are rejected, including in unused optional subschemas. A root
  `$id` with local pointers works without fetching a remote resource. Literal
  objects in `const` are data, not schema references.
- A per-call SchemaRegistry refuses remote fetching. Per-call handlers preserve
  Unicode scalar counting rather than grapheme counting, including combining
  marks and supplementary characters; length constraints must fit Int64.
- Schema JSON is limited to 262,144 UTF-16 code units. Policy traversal allows
  1,024 expanded schema-node visits and depth 64; reference expansion consumes
  the same budget. JsonDocument also retains its default parsing depth limit.
- Arguments are limited to 2,097,152 raw JSON UTF-16 code units, 8,192 value
  nodes and value depth 64, even if a caller parsed with a larger depth limit.
- Each pattern is at most 4,096 code units. Regex uses .NET ECMAScript with
  CultureInvariant and a **25 ms timeout per match**, including
  `patternProperties`. This is not a hard deadline for the whole validation:
  compilation/library evaluation cannot be preempted mid-call. Cancellation
  is checked around evaluation, during policy traversal and every 256 Unicode
  scalars in length evaluation. The library's pattern-properties loop itself
  does not check the token between successful matches.
- Invalid descriptors or arguments never reach approval or tool invocation.
  Errors use localized safe summaries, not raw input, remote exception text or
  pattern details. A missing root required field may show its ASCII identifier
  only when at most 48 characters long.

## Reproduction and automated evidence

Evidence remains outside Git in
`C:\Users\wzheng\AppData\Local\Temp\MossAgent-schema-validation-20261010`.

- `red/schema-red.trx`: the 38 new object/composition cases against the old
  evaluator produced 24 failures and 14 passes before the replacement.
- `green/schema-boundaries.trx`: 95 selected schema, pipeline and existing
  string/audit cases pass, including budget boundaries, malicious patterns,
  unused malformed schemas, nested IDs and cancellation.
- `green/workspace-schema-green.trx`: 10 selected workspace MCP cases pass.
  New cases run real loopback model HTTP/SSE and MCP discovery, reject root
  unknown fields, nested unknown fields and invalid `const` values, make no
  approval request or `tools/call`, deliver the failure to the model follow-up,
  persist the visible tool result/audit and restore without replay.
- `solution-build.log`: solution build succeeds with zero warnings/errors.
- `full-tests.log` and `full/*.trx`: all eight test assemblies pass, 4,239
  tests total, with no failures or skips. This is the current workspace result,
  not the earlier 4,157-test baseline. App.Tests accounts for 3,166 tests and
  takes approximately four and a half minutes.
- `format-green.log`: scoped whitespace verification succeeds.
- `code-size.log`: all 19 modified/new handwritten C# files satisfy one type,
  file <=500 lines and method <=80 lines.

## Native verification

The temporary native QA host and generated data are outside the repository.
It starts the production Avalonia App with synthetic providers and loopback
MCP/model servers, not a manually fabricated ToolResult or a paid API call.

- Log reports `QA_ELEVATED: False` (ordinary permissions).
- Computer Use clicked the actual Send button in the running workspace.
- A model-requested MCP call contained an unknown nested property. The visible
  localized tool failure and the final assistant response appeared normally;
  no approval prompt appeared.
- Log reports `code=invalid_arguments; remoteCall=False; modelRequests=2`.
- Passive Python capture is saved as `native/schema-rejected.png`; it is
  non-black and shows the tool failure and completed model follow-up.
- The native window closed through its Close button. The temporary QA process
  remained without a window and was stopped after checking its exact executable
  path. This slice does not claim successful graceful shutdown of that helper.

Follow-up on 2026-10-10 diagnosed that temporary residual process: the production
App lifetime had already returned, and the MCP test server's asynchronous cleanup
captured the stopped UI context. Context-independent loopback fixtures now pass
deterministic regressions. New native completed and approval-pending runs exit
without termination, with real persisted task/tool/audit verification; see
`WINDOW_SHUTDOWN_ACCEPTANCE.md`. The earlier evidence is retained rather than
rewritten as an originally successful helper teardown.

This is a light-theme native error-path check, not full Codex visual-fidelity
acceptance, dark-theme/resizing coverage, or acceptance against real third-party
MCP servers. General recursive/dynamic-resource schemas remain unsupported.
Windows x64, ordinary JIT debugging and single-file release settings are
unchanged; no AOT work, migrations, commits or publication are included.

## 2026-10-11 registry isolation follow-up

The original per-call-registry claim above described the intended boundary, but
BuildOptions actually defaults to SchemaRegistry.Global. Cold-start tests found
that Build wrote invocation-owned schemas to a shared non-thread-safe dictionary
and the Fetch assignment changed Global.Fetch. The old full regression and native
evidence do not prove absence of this race.

ToolSchemaDialect now explicitly creates a private SchemaRegistry for each call
and still refuses remote fetching. This avoids cross-call IDs, global retention
and concurrent dictionary corruption without locks, warm-up or retry logic.
The original validation limits and pipeline ordering remain unchanged. See
`TOOL_SCHEMA_ISOLATION_ACCEPTANCE.md` for the reproduced failure, regression tests
and independent cold-process evidence. This follow-up does not rewrite the
historical results or claim complete Codex visual fidelity.
