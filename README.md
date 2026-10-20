# MossAgent

MossAgent is a Windows-first desktop coding agent built with Avalonia 12 and
.NET 10. The repository is structured as a modular monolith so model providers,
tools, browser automation, MCP, persistence, and the UI can evolve without
forming a single application-wide dependency graph.

## Product objective

Restore Codex Desktop's functionality and main workflows, not its exact pixels.
Prioritize working end-to-end behavior, safety and reliability while retaining a
consistent, usable MossAgent interface. The current capability inventory, missing
features and verification limits are in
[docs/FUNCTIONAL_PARITY_MATRIX.md](docs/FUNCTIONAL_PARITY_MATRIX.md).
The 2026-10-20 implementation inventory, verified scope, known blockers and
detailed remaining implementation plans are recorded in
[docs/DELIVERY_STATUS_AND_ROADMAP.md](docs/DELIVERY_STATUS_AND_ROADMAP.md).
Earlier visual-fidelity records remain historical evidence, not pixel-matching
requirements for product completion.

## Current implementation

- Avalonia task workbench with persisted task history, conversation restore,
  follow-up turns, project roots, worktrees, concurrent task runs, per-task
  cancellation, and queued approval controls.
- SQLite persistence for projects, tasks, messages, proxies, providers, and
  per-provider model configuration.
- Independent text drafts for tasks, project conversations and projectless chat,
  with automatic SQLite saving, restart restoration, revision-conflict checks
  and explicit save/recovery controls. Project-scoped and explicitly selected
  projectless text/static-image drafts use file-backed snapshots and the same
  revision checks without granting directory or tool access.
- Submitted UTF-8 text attachments use immutable, task/message-owned file
  snapshots with SQLite metadata, restored history chips and original-turn model
  context across follow-ups and application composition restarts. Missing or
  changed snapshots block requests with recovery guidance.
- Authorized static PNG/JPEG/GIF/WebP selection uses real bounded decoding,
  binary snapshots, metadata-only SQLite migration and draft/history restoration.
  Image-only turns send native image blocks; incompatible models retain drafts
  and disable sending. Production composition and loopback checks are recorded in
  [docs/IMAGE_ATTACHMENT_WORKFLOW_ACCEPTANCE.md](docs/IMAGE_ATTACHMENT_WORKFLOW_ACCEPTANCE.md).
  Projectless file selection, draft recovery and submitted-image history are
  covered in [docs/PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md](docs/PROJECTLESS_ATTACHMENTS_ACCEPTANCE.md).
  Composer/draft and sent/history image thumbnails use bounded background
  rendering and immutable snapshots; see
  [docs/ATTACHMENT_THUMBNAIL_ACCEPTANCE.md](docs/ATTACHMENT_THUMBNAIL_ACCEPTANCE.md).
  JPEG previews respect all eight EXIF orientations without rewriting selected
  bytes; see [docs/IMAGE_ORIENTATION_ACCEPTANCE.md](docs/IMAGE_ORIENTATION_ACCEPTANCE.md).
  Native picker/EXE restart, full-size viewing, PDF input and real-provider
  interpretation remain incomplete.
- Native streaming adapters for OpenAI Responses, Anthropic Messages, and
  Gemini GenerateContent, including multi-turn function/tool calls and
  reversible provider-safe names for dotted MCP/built-in tool identifiers.
  Gemini tool turns retain signed content parts and native call identities in
  scoped, bounded memory; opaque continuation state is not persisted or displayed.
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
- Editable proxy profiles with HTTP/SOCKS5 connectivity diagnostics. An enabled
  default proxy is preselected for new providers; explicit direct connections
  stay direct, and a disabled proxy cannot be silently replaced while editing.
- Configurable subagents with trigger guidance, per-agent model and reasoning
  settings, optional conversation sharing, live progress and persisted dialogue.
  Child tools inherit the parent task's authorization and re-enter the standard
  approval, audit, timeout, cancellation, and output-limit pipeline; recursive
  delegation is unavailable. Provider/model changes refresh the subagent editor
  without discarding drafts or silently substituting unavailable models.
- Shared visual components for all business pages, with build-time AXAML rules
  and C# analyzers prohibiting native visual controls and instance appearance
  overrides. Structural layout primitives remain available; component styling
  is maintained centrally rather than patched independently on each page.

The binding engineering rules are in [AGENTS.md](AGENTS.md). Read that file
before making changes. The module map is in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
Component usage and enforced boundaries are documented in
[docs/UI_COMPONENTS.md](docs/UI_COMPONENTS.md); native subagent acceptance and
its limits are recorded in
[docs/SUBAGENT_EXECUTION_ACCEPTANCE.md](docs/SUBAGENT_EXECUTION_ACCEPTANCE.md).

## Build and run

Requirements:

- Windows 10 19041 or newer
- .NET SDK 10
- PowerShell 7 (`pwsh`) for command tools
- Chrome or Edge for browser tools

```powershell
dotnet restore MossAgent.slnx
dotnet build MossAgent.slnx
dotnet test MossAgent.slnx --no-build
dotnet run --project src/MossAgent.App/MossAgent.App.csproj
```

Normal Debug and Release builds/runs use managed/JIT mode. Create a tested,
self-contained Windows x64 single-file package:

```powershell
.\scripts\publish.ps1 -Runtime win-x64 -Version 0.1.0
```

The script uses the `WindowsX64` profile and writes the runnable package to
`artifacts/publish/win-x64` and its ZIP archive under `artifacts/publish`.
Compilation, single-file validation and archive creation finish before replacing
existing packages. Failures preserve the previous package and archive; temporary
staging is cleaned up. Use `-SkipTests` only after an equivalent Release test run.
The `WindowsX64` publish profile produces a single `MossAgent.App.exe`, including
the .NET runtime, native libraries and Playwright driver. This is compressed
self-extracting single-file deployment, **not Native AOT**. On first launch,
required files are extracted to the .NET bundle cache (by default under
`%TEMP%\.net`; override with `DOTNET_BUNDLE_EXTRACT_BASE_DIR`). A writable cache
and executable extracted Node/native dependencies are required. Do not delete
that cache while the app or a task-owned browser is running. User data still
lives in the separate application data directory.
Trimming stays disabled to preserve runtime serialization, bindings and browser
dependencies. Debug symbols are excluded from the distributable; shared runtime
code and licenses remain inside the bundle.
The desktop project defaults to `win-x64` / `x64`; other desktop runtime targets
are rejected. Shared domain and application libraries remain platform-neutral.
Playwright build and publish outputs contain only the Windows x64 Node driver
and Windows installers. Keep its shared JavaScript package, browser registry and
licenses: these are required at runtime, even when they describe other platforms.
Browsers are not bundled; Chrome or Edge must be installed separately.

Application data is stored under `%LOCALAPPDATA%\MossAgent`. Set
`MOSSAGENT_DATA_DIR` to use a separate data directory for an isolated preview
or test run. Provider API keys and proxy credentials are currently stored in
plain text in the local SQLite database by explicit product choice. They are
redacted from application errors and should never be added to logs or
diagnostics.

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
