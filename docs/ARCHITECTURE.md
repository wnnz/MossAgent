# MossAgent Architecture

`AGENTS.md` is the binding engineering policy. This document is the concise map
of the intended system and should be kept current as modules are implemented.

## Runtime flow

```text
Avalonia UI
  -> application use case
  -> task-scoped agent runner
  -> model provider stream
  -> tool execution pipeline
  -> schema validation, policy approval, timeout, and audit start
  -> built-in, browser, Git, shell, or MCP tool
  -> bounded result and file-backed artifact metadata
  -> audit completion
  -> persisted task event
  -> next model turn and UI update
```

## Module map

| Module | Owns |
| --- | --- |
| Domain | Entities, values, enums, domain validation |
| Application | Use cases, repository ports, orchestration ports |
| Agent | Agent loop, context budget, tool-call coordination |
| Tools.Abstractions | Tool descriptors, requests, results, execution context |
| Tools.BuiltIn | Files, search, shell, Git, and screenshot tools |
| Providers | OpenAI, Anthropic, Gemini, and compatible APIs |
| Browser | Task session routing, Playwright/CDP tabs, downloads, and browser tools |
| MCP | stdio and Streamable HTTP clients and tool adapters |
| Infrastructure | SQLite, migrations, repositories, HTTP/proxy creation |
| Platform.Windows | ConPTY terminal sessions, process integration, and capture |
| App | Avalonia views, embedded WebView2 host, view models, and composition |

## Stable boundaries

- Models emit normalized agent events instead of provider-specific UI objects.
- The agent injects task-bound safety guidance and constructs a bounded context
  suffix using each model's configured context and output limits.
- Tools execute through one guarded pipeline and return bounded `ToolResult`
  values plus optional artifacts.
- Browser implementations expose one task-bound `IBrowserSession` contract;
  tab identifiers are stable only for the lifetime of that session.
- Browser downloads write through a caller-owned stream so the browser layer
  never selects arbitrary filesystem destinations.
- Large binary outputs stay in the task artifact directory. SQLite stores only
  names, paths, media types, lengths, and SHA-256 hashes.
- Persistence is accessed through application ports, not raw connections from
  UI or agent code.
- Platform-specific facilities implement portable ports and stay outside the
  domain and application projects.

When a feature does not fit these boundaries, update the architecture first;
do not work around it with cross-project references or global state.
