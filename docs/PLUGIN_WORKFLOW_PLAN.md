# Plugin and skill workflow plan

Status: design only. No plugin directory, installation, enablement or runtime
integration is implemented by this document. MCP settings alone do not establish
plugin support. Command filesystem confinement remains the higher-priority blocker.

## Verified reference semantics (2026-10-19)

Official sources read directly: [plugin packaging](https://developers.openai.com/plugins/build/plugins)
and [App Server](https://learn.chatgpt.com/docs/app-server).

- New portable packages use root `plugin.json`, the Agent Plugins 1.0 schema,
  `skills/`, root `mcp.json` and optional assets. Portable MCP entries explicitly
  declare their transport `type`; renaming legacy `.mcp.json` is insufficient.
- `extensions.com.openai`, when present as an object, replaces the entire
  `.codex-plugin/plugin.json` compatibility overlay. Their OpenAI settings are not
  merged. Root portable identity/components remain canonical. Legacy compatibility
  is a separate import path whose behavior must be tested explicitly.
- Public directory, local/repository marketplaces, registered connector mappings,
  MCP authentication and optional tool UI have distinct distribution, account and
  runtime requirements. A local package importer does not complete those workflows.
- Installation/enablement does not automatically trust bundled hooks. The current
  hook definitions require review. Package manifests describe authentication and
  must not contain credentials such as OAuth client secrets or bearer tokens.
- App Server marks `plugin/list`, `plugin/read`, `plugin/install` and
  `plugin/uninstall` under development and says production clients must not call
  them yet. They are not a supported shortcut to the official directory. This
  limitation does not establish that another public directory API is available.

## Ownership and required self-check

1. Domain owns immutable package identity, component descriptions, source/revision
   and trust state. Application owns discovery/install/enable/remove use cases and
   ports. Infrastructure owns bounded parsing, source fetching, staged package
   storage and persistence; MCP retains its transport implementation. App owns
   shared controls and localized lifecycle/error feedback. No UI SQL/process/file
   access, reverse dependency or global registry is introduced.
2. Reuse the shared tool contract/executor and existing model/context budgets.
   A plugin is an origin of skills/tools, not an alternative executor. Every MCP
   operation and any supported hook command must retain its source identity and
   run through authorization, approval, cancellation, deadline, output and audit.
3. Package paths are relative to their package root. Reject absolute paths,
   traversal, links/reparse escapes, archive collisions and extraction bombs.
   Installation grants no project roots or browser attachment. Stdio launch/hook
   execution also depends on the unresolved command sandbox; do not integrate a
   launch path that silently inherits arbitrary host file or credential access.
4. Split manifest formats, catalog sources, extraction, immutable installed
   versions, task activation snapshots, trust and recovery into separate types.
   Preserve single-type files and existing file/method limits. Configuration uses
   forward migrations, revision checks and bounded metadata rather than binaries.
5. Test valid/invalid portable and legacy manifests, whole-overlay precedence,
   path/link/archive denial, cancellation/partial install rollback, version and
   trust conflicts, disabled/revoked components, task isolation, exact selected
   model/skill context, shared MCP execution, audits and restart without replay.

## Delivery sequence and completion evidence

First implement an explicit local package workflow with source preview, validation,
immutable installed copy, enable/disable, component inventory, actionable failures
and uninstall preserving user-owned data. Mark that slice as local-only while
marketplaces, public directory, OAuth, registered apps, hooks and optional tool UI
remain separately visible gaps. Do not label unsupported components as enabled.

Then connect selected skills to bounded model instructions and plugin MCP tools to
the task's normal catalog/pipeline. Persist the exact installed version and reviewed
trust revision. Define disable/uninstall behavior for active tasks before allowing
those mutations; do not change a running task's components behind its back. Recovery
may clean staged files and show interrupted state; it never executes an interrupted
hook, tool, setup instruction or install script automatically.

Finally add and verify catalog/distribution and authentication routes where public
interfaces and permissions permit. Keep account-dependent unavailable routes
explicit. Completion requires installation through actual skill/tool invocation,
failure/cancellation/recovery, independent tasks, native shared UI and security
negative cases; manifest parsing or a same-named button is insufficient. Live
accounts, simulated transports, Headless UI and native interaction remain distinct
evidence categories.
