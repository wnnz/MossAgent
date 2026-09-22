# MossAgent Engineering Constraints

This file is mandatory guidance for every AI agent and contributor working in
this repository. Read it before inspecting or changing code. These constraints
apply to all new code and refactors unless the user explicitly overrides one.

## Product and platform

- MossAgent is a modular Avalonia desktop coding agent targeting .NET 10.
- Windows is the first fully supported platform. Keep core contracts and domain
  logic platform-neutral; isolate Windows APIs in `MossAgent.Platform.Windows`.
- The application supports multiple projects, authorized roots, concurrent
  tasks, model providers, proxies, MCP servers, browser automation, and tools.
- Never log API keys, proxy credentials, authorization headers, raw cookies, or
  other secrets. Secrets are currently stored as plain text in SQLite by an
  explicit product decision, so diagnostics and exports require extra care.

## Required dependency direction

- `MossAgent.Domain` contains domain types and has no project dependencies.
- `MossAgent.Tools.Abstractions` contains tool contracts and may depend only on
  Domain.
- `MossAgent.Application` contains use cases and ports; it may depend on Domain
  and Tools.Abstractions, never on concrete infrastructure or UI.
- `MossAgent.Agent` implements orchestration through abstractions. It must not
  directly access Avalonia, SQLite, Playwright, or operating-system APIs.
- Providers, built-in tools, Browser, MCP, Infrastructure, and Platform.Windows
  implement ports. They must not reference `MossAgent.App`.
- `MossAgent.App` is the composition root and UI. UI code must not directly run
  SQL, access files, start processes, or make provider HTTP calls.
- Do not introduce static service locators or mutable global state.

## File and type size limits

- Prefer C# files under 250 physical lines. The hard limit is 500 lines.
- Prefer AXAML files under 300 physical lines. The hard limit is 500 lines.
- Prefer methods under 40 lines. The hard limit is 80 lines.
- Every handwritten `.cs` file must contain exactly one type declaration. This
  includes `class`, `record`, `struct`, `interface`, `enum`, and `delegate`.
- Do not declare helper, input, DTO, enum, or private nested types in the same
  file as another type. Move every type to a separately named `.cs` file.
- Split UI features into View, ViewModel, reusable controls, and services.
- Implement every agent tool as its own class. Never create a giant switch or a
  single `ToolService` containing unrelated tools.
- Split provider adapters into request building, stream parsing, event mapping,
  model discovery, and error mapping when those responsibilities grow.
- Split browser session management, DOM actions, screenshots, profiles, and
  navigation into separate types.
- Do not use `#region` to conceal oversized classes.
- Do not use `partial` to distribute a large class, except generated code,
  required native interop declarations, or normal Avalonia code-behind.
- Generated files and database migration snapshots may exceed these limits but
  must stay isolated from handwritten application logic.

## Tool execution safety

- Every built-in and MCP tool must implement the shared tool contract and run
  through the standard execution pipeline.
- The pipeline order is: lookup, schema validation, capability/path validation,
  risk classification, approval, timeout/cancellation, execution, output
  limiting/artifact storage, and audit persistence.
- A tool must never invoke another tool implementation directly to bypass the
  pipeline.
- Canonicalize paths and verify them against task-authorized roots. Account for
  `..`, symbolic links, junctions, reparse points, and case-insensitive Windows
  paths before reading or writing.
- Use atomic replacement for file writes. Detect concurrent external changes
  before overwriting a file read earlier in a task.
- Commands must run with an explicit authorized working directory, timeout,
  cancellation token, bounded output, and task-owned process tree.
- Browser actions must target the browser session explicitly attached to the
  task. Do not scan for or attach to arbitrary debugging ports.
- Desktop screenshots and access to a daily browser profile are sensitive
  operations and must obey the active approval policy.
- Unknown MCP tools are side-effecting by default. Trust read-only metadata only
  when it comes from an explicitly configured server and can be validated.

## Approval policies

- `ReadOnly`: allow file/search inspection and explicitly read-only operations;
  deny writes, shell execution, page mutation, and side-effecting MCP tools.
- `AskEveryTime`: reads may run automatically; each mutation, command, browser
  interaction, or side-effecting MCP call requires approval.
- `FullAccess`: actions inside authorized roots and the attached browser session
  may run without prompts, but path escapes, system-wide destructive commands,
  and unauthorized browser attachment remain blocked.

## Persistence and large outputs

- SQLite stores structured metadata and conversation state. Do not put large
  screenshots, downloads, command logs, or binary files in SQLite.
- Store large outputs as task artifacts; persist only metadata, paths, hashes,
  sizes, and bounded previews.
- Do not place unbounded tool output into model context. Use pagination,
  truncation, summaries, and model-aware context budgets.
- Database changes require forward migrations and tests. Never silently delete
  or reinterpret existing user data.
- Interrupted tasks are restored as interrupted and require an explicit manual
  continuation. Never replay incomplete side effects automatically.

## UI and concurrency

- Keep long-running work off the UI thread. Marshal only final observable state
  changes to Avalonia's dispatcher.
- Every running task owns its cancellation token, event stream, process tree,
  and browser session. Never reuse mutable task state across concurrent tasks.
- Event handlers and subscriptions must be disposed when a view or task closes.
- User-visible errors should be actionable and must not expose secrets.
- Use localized resources for user-facing text; Simplified Chinese is the
  initial default, but do not hard-wire product behavior to one language.

## Testing and completion

- Add or update focused tests for behavior changes. Security boundaries require
  negative tests, not only successful-path tests.
- Tool tests must cover descriptor/schema validity, approval behavior, timeout,
  cancellation, and output limits where applicable.
- File tests must cover traversal, links/reparse points, concurrent changes, and
  multiple authorized roots.
- Provider parsers should be tested with deterministic recorded JSON/SSE
  fixtures; ordinary test runs must not require paid API calls.
- Browser tests should use a local deterministic test page whenever possible.
- Before handing off a change, run the narrowest relevant tests plus a solution
  build. State exactly what was and was not validated.
- Preserve unrelated user changes. Stage explicit paths only; never use
  `git add .` in a dirty working tree.

## Required self-check before adding code

Before implementing a feature, answer these questions in the design or code
review:

1. Which module owns the behavior, and does the dependency direction remain
   valid?
2. Can the behavior be expressed through an existing port or tool contract?
3. What authorization, approval, timeout, cancellation, and output limits apply?
4. Will any file or method exceed the size limits after this change?
5. What focused tests prove both the successful path and the safety boundary?
