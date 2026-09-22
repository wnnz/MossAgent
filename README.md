# MossAgent

MossAgent is a Windows-first desktop coding agent built with Avalonia 12 and
.NET 10. The repository is structured as a modular monolith so model providers,
tools, browser automation, MCP, persistence, and the UI can evolve without
forming a single application-wide dependency graph.

## Current implementation

- Avalonia task workbench with persisted task history, conversation restore,
  follow-up turns, project roots, worktrees, concurrent task runs, per-task
  cancellation, and queued approval controls.
- SQLite persistence for projects, tasks, messages, proxies, providers, and
  per-provider model configuration.
- Native streaming adapters for OpenAI Responses, Anthropic Messages, and
  Gemini GenerateContent, including multi-turn function/tool calls and
  reversible provider-safe names for dotted MCP/built-in tool identifiers.
  Provider error bodies are bounded and redact API keys plus custom header
  values.
- Model discovery plus editable model configuration for context/output limits,
  reasoning effort, Temperature, Top-P, modalities, defaults, and advanced JSON.
  Long conversations use a model-aware context window that preserves system
  guidance and complete recent tool interactions.
- Guarded file read/write/search/find/copy/move/delete tools, PowerShell, Git,
  worktree, browser, terminal, browser screenshot/download, and desktop capture
  tools. Tool arguments are schema-validated before approval or execution.
- Editable provider profiles with enable/default state and non-disclosing API
  key updates.
- Embedded WebView2 with a takeover toolbar, task-scoped multi-tab sessions,
  profiles and proxy settings, plus headed Chrome/Edge and explicit CDP
  attachment. Browser navigation and controlled downloads only accept HTTP(S)
  URLs; embedded downloads are capped at 32 MB.
- MCP stdio and Streamable HTTP configuration, discovery, dynamic tool
  registration, headers, proxies, and read-only risk annotations.
- Interactive Windows ConPTY terminal, staged/unstaged changed-file diff panel,
  safe managed-worktree cleanup, and persisted tool execution/artifact audit.
- Read-only, ask-every-time, and full-access policies with an in-app approval
  prompt.
- Editable proxy profiles with HTTP/SOCKS5 connectivity diagnostics.

The binding engineering rules are in [AGENTS.md](AGENTS.md). Read that file
before making changes. The module map is in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Build and run

Requirements:

- Windows 10 19041 or newer
- .NET SDK 10
- Chrome or Edge for browser tools

```powershell
dotnet restore MossAgent.slnx
dotnet build MossAgent.slnx
dotnet test MossAgent.slnx --no-build
dotnet run --project src/MossAgent.App/MossAgent.App.csproj
```

Create a tested self-contained Windows package:

```powershell
.\scripts\publish.ps1 -Runtime win-x64 -Version 0.1.0
```

The script writes the runnable directory and ZIP archive under
`artifacts/publish`. Use `-SkipTests` only after an equivalent Release test run.

Application data is stored under `%LOCALAPPDATA%\MossAgent`. Provider API keys
and proxy credentials are currently stored in plain text in the local SQLite
database by explicit product choice. They are redacted from application errors
and should never be added to logs or diagnostics.

## Development status

The first usable foundation and primary agent loop are implemented. Provider
stream parsers, stdio/HTTP MCP, tool auditing, schema validation, worktree
lifecycle, browser tabs, and Windows self-contained packaging have deterministic
coverage. A read-only LiveAgent configuration acceptance run has also exercised
real model discovery, streaming text, and a file write/read tool loop without
printing or persisting its API key.

The next release-hardening work is installer/signing, localization resources,
full WebView2 UI automation on a deterministic local page, and end-to-end
long-running task soak testing.
