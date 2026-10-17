# MossAgent Architecture

`AGENTS.md` is the binding engineering policy. This document is the concise map
of the intended system and should be kept current as modules are implemented.

`PRODUCT_ACCEPTANCE_MATRIX.md` tracks the complete product objective and remaining
evidence gaps. `INTEGRATED_WORKFLOW_ACCEPTANCE.md` records native production-App
composition checks; passing an individual slice does not establish full-product
or same-state Codex pixel-equivalence completion.

## Runtime flow

```text
Avalonia UI
  -> application use case
  -> task-scoped agent runner
  -> model provider stream
  -> tool execution pipeline
  -> schema, capability and path validation, policy approval, timeout, and audit
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
| App.Analyzers | Compile-time shared visual component and appearance rules; no runtime UI behavior |

## Stable boundaries

- Conversations may have no project: a null `AgentTask.ProjectId` is a persisted
  plain-chat scope, not an implicit directory grant. Such runs advertise no tools
  or subagents and reject unsolicited tool calls. The standard execution pipeline
  also denies tools when `ToolsEnabled` is false. No terminal, browser or file
  context is attached. See `PROJECTLESS_CHAT.md` for migration and validation scope.

- Tools.BuiltIn owns common input-schema validation before capability checks,
  approval or execution, including MCP adapters. JsonSchema.Net 8.0.5 replaces
  the incomplete handwritten assertion evaluator; per-call dialect handlers
  retain Unicode-scalar string lengths and bounded .NET ECMAScript patterns.
  Schema/value budgets and cancellation checks reject oversized input, unsafe
  references and malformed optional subschemas. Only acyclic same-document
  JSON Pointer references are accepted; nested resource IDs, dynamic/recursive
  references, anchor-based references and external resolution fail closed.
  BuildOptions explicitly owns a new SchemaRegistry per invocation with remote
  fetching disabled; the library default points to Global and must not be used.
  Request schemas and Fetch delegates never modify the global library registry.
  Schema errors never echo argument values or library
  exception text. Existing audit, approval, output and persistence ports remain
  unchanged. See `TOOL_SCHEMA_ACCEPTANCE.md` for exact limits and verification,
  and `TOOL_SCHEMA_ISOLATION_ACCEPTANCE.md` for cold-start concurrency evidence.

- Providers' Anthropic parser stores each tool block's identity, name and argument
  buffer in one request-owned entry instead of three parallel dictionaries.
  Compatible Chat parsing appends selected choices directly to one frame-owned
  event list, avoiding per-choice lists and copies. Failures still discard the
  whole frame, alternate choices remain excluded and previously returned lists
  are never reused. Tool-name decoding, SSE limits, cancellation and downstream
  execution policies remain unchanged. See `PROVIDER_PARSER_OPTIMIZATION_ACCEPTANCE.md`.

- Provider adapters share failure-event sanitization; non-failure events keep their
  published identity. HTTP 407 is mapped to local authentication guidance before
  reading any proxy body, in both discovery and streaming. Credentials remain owned
  by Infrastructure's existing HTTP/SOCKS5 client factory, not by Providers or UI.
  Other response limits, decoding and cancellation remain unchanged. Deterministic
  settings-to-runtime and native success/direct-route evidence is in
  `AUTHENTICATED_PROVIDER_PROXY_ACCEPTANCE.md`. Native wrong-password/recovery is
  also verified for Gemini over HTTP; missing credentials, SOCKS5 and other-protocol
  failure/recovery retain deterministic coverage, not native coverage.

- App's proxy editor reloads the catalog before publishing a saved selection, so
  two-way ListBox feedback cannot clear its target before completion. ID/revision
  guards retain newer drafts and await configuration notifications. Availability,
  reload and command checks share an allocation-free ID lookup without a cache.
  See `PROXY_EDITOR_SELECTION_ACCEPTANCE.md` for real-page and native regressions.

- Provider, browser and MCP settings share ProxySelection's lazy option projection:
  the localized direct option comes first, followed by enabled profiles in repository
  order. Default flags and duplicate occurrences do not alter ordering. Browser
  settings build options and enabled identities in one pass without an intermediate
  profile array; each editor retains its existing selection, unavailable-proxy,
  revision and cancellation rules. No cross-editor cache or execution-policy change.

- App's shared collection synchronization skips matched prefixes and avoids
  membership sets for unchanged snapshots, appends and tail removals. Model catalogs
  filter indexed repository snapshots directly without per-provider LINQ closures.
  Row identity, duplicate order, observable deltas, model selection and cancellation
  remain unchanged. Provider/model editors share current-snapshot identity lookups
  without LINQ closures or caches; row commands resolve their target once, retaining
  first-occurrence and provider/model ownership rules. See
  `CATALOG_COLLECTION_OPTIMIZATION_ACCEPTANCE.md`.

- App's model search scans group members once and materializes only changed partial
  membership. Equivalent matches retain immutable groups without filtering iterators;
  the picker compares the published prefix before allocating a desired-group list.
  Changed lists still use shared observable reconciliation. Search ordering, duplicate
  occurrences, selection, focus, reload generations and unavailable-model rules remain
  unchanged. See `CATALOG_COLLECTION_OPTIMIZATION_ACCEPTANCE.md`.

- App's project, pinned and recent sidebar lists share indexed title/pin filtering
  and lazy navigation-row reconciliation. Equal prefixes need no temporary filter
  snapshots or row maps; changed suffixes retain duplicate occurrence order and
  authoritative task metadata. Recent tasks sort at load and insert in timestamp
  order, rather than sorting on every search keystroke; timestamp ties retain the
  existing stable-order rules. Project-list subscriptions follow collection deltas;
  source moves still refresh stable duplicate-key ordering without rescanning
  subscriptions. Notifications captured before disposal cannot republish the list.
  See `SIDEBAR_REFRESH_OPTIMIZATION_ACCEPTANCE.md`.

- Gemini text-only and metadata frames retain their mapper-owned event snapshots
  after failure, finish-state and continuation validation, avoiding a second list.
  Per-frame tool counting is linear and independent of text/thought parts; tool
  names and native identities are decoded once. Mixed frames still defer calls
  until verified completion and discard a whole frame on failure. No state is
  shared across frames or parsers. See `PROVIDER_PARSER_OPTIMIZATION_ACCEPTANCE.md`.

- Providers' bounded SSE line reader scans decoded chunks for line endings instead
  of appending every character. Complete lines use their own string directly;
  unfinished line segments use a lazy iterator-owned buffer. Strict UTF-8, leading
  BOM handling, CR/LF/CRLF, character budgets, cancellation and caller-owned stream
  lifetime remain unchanged. See `PROVIDER_PARSER_OPTIMIZATION_ACCEPTANCE.md`.

- App tool-message persistence stores only raw tool fields and dialogue messages.
  Computed visibility flags and dialogue view models are excluded from JSON, so
  saving results does not duplicate dialogue content or parse Markdown for storage.
  Presentation remains derived from raw messages; old payloads with redundant
  presentation fields still load without rewriting user data or database migrations.
  Standard JIT JSON serialization remains in use. See `SUBAGENT_EXECUTION_ACCEPTANCE.md`.

- Tool output limiting consumes the existing shared budget in one pass rather than
  validating/counting again on overflow. Complete file-change and dialogue collections
  retain their published references; only truncated prefixes are copied. Invalid records,
  whole-record priority, surrogate boundaries, late committed results and audit semantics
  remain unchanged. See `TOOL_OUTPUT_OPTIMIZATION_ACCEPTANCE.md`.

- App's tool-result persistence and live presentation share one raw-payload mapping.
  Ordinary assistant/system messages still parse Markdown; user bubbles and tool
  rows retain raw text without building hidden response blocks. Subagent answers
  keep their separate Markdown projection. Table detection rejects non-candidates
  before splitting cells, preserving existing single-column and escaped-pipe rules.
  No execution policy, stored JSON shape or shared appearance changes. See
  `UI_HOTPATH_OPTIMIZATION_ACCEPTANCE.md` for regression and allocation evidence.

- App's code-preview parser and literal search share one bounded line reader.
  It stops at the existing character/line limits instead of splitting discarded
  content; CRLF, lone carriage returns, trailing empty lines and truncation labels
  keep their previous meaning. Search scans visible lines once and resets results
  once per content replacement. No snapshot cache, file access, appearance or tool
  execution change. See `UI_HOTPATH_OPTIMIZATION_ACCEPTANCE.md`.

- App's tool progress, final result and child dialogue share indexed message-row
  lookup without captured predicates or a cross-task cache. Each event retains
  its existing first-match, call-identity and tool-name rules. Progress display
  names consume at most the first 80 Unicode scalars in a fixed local buffer;
  control removal, malformed UTF-16 replacement and localized fallback stay
  unchanged. Summary-detail checks use spans rather than copying trailing
  whitespace. See `TOOL_PROGRESS_ACCEPTANCE.md` for regressions and measurements.

- Agent's model context builds only the newest conversation groups needed by the
  current budget instead of materializing every historical turn. System ordering,
  whole-turn retention, tool-call/result grouping, opaque continuation signatures
  and independent request snapshots remain unchanged. Shared subagent context
  locates the latest twelve relevant messages by index without scanning discarded
  history or allocating a rolling queue; role labels and the bounded Unicode-safe
  tail remain unchanged. See `CONTEXT_BUILDING_OPTIMIZATION_ACCEPTANCE.md`.

- App's shared conversation timeline projects recorded timestamps and elapsed time
  from existing messages without changing persistence or model context. User turns,
  execution disclosure and final replies retain message identity; source switching
  and visual detachment release subscriptions. Range-based grouping avoids copied
  turn lists, and unchanged derived visibility/command state does not notify again.
  Message subscriptions follow collection deltas with occurrence counts; moves
  retain subscriptions and resets rebuild them. Unchanged projections reuse the
  anchor map and observable rows, creating activity snapshots only after a matched
  prefix diverges. Changed grouping prunes removed anchors before publication.
  Tool-only and cancelled turns never manufacture an empty reply. Shared controls
  and semantic styles own disclosure geometry and divider colors. See
  `CONVERSATION_TIMELINE_ACCEPTANCE.md` for reproduced failures and native evidence.

- File-write and text-replace tools report paths and before/after SHA-256 only after
  atomic commit. Whole validated records share the standard output budget; cancelled
  or timed-out results retain already committed changes. Agent collects child-tool
  records without file access. App persists optional raw metadata in the existing
  tool payload and derives a task-owned file summary using shared controls/styles.
  Preview and Git review reuse the authorized tool pipeline; Git review remains the
  current workspace diff. The task file summary opens immutable before/after snapshot
  patches through the registered filesystem.review_change tool, not the live file or
  Git workspace. Consecutive hash-linked writes retain the first before and last after
  version; discontinuities are marked incomplete and net-zero edits are omitted.
  Writes retain bounded before/after
  raw-byte snapshots in the task artifact directory, with optional filename/length/
  SHA-256 references in existing metadata. Capture shares the original atomic lock
  and version checks; the after snapshot comes from staged bytes before commit.
  Unchanged writes and staging failures do not create history; unsuccessful commits
  discard uncommitted snapshots. Readers validate names, limits, links, length and
  digest without falling back to the live file. Review uses the bounded task-owned Git
  process runner in no-index/text mode with external diff/textconv disabled. Strict
  UTF decoding, patch bounds, task switching and cancellation fail closed; private
  staging files are removed, and the UTF-8 patch is a hash-verified task artifact.
  Current-file and historical-diff tabs remain separate. Snapshot restoration uses a
  registered UI-only mutation tool and the existing byte-preserving atomic writer;
  created-file removal is a separate high-risk tool, rejected by FullAccess and
  requiring AskEveryTime approval. Both require an exact ReviewedChange and verified
  history/current versions. Inverse metadata reuses validated snapshot references.
  Absent after versions require an explicit AfterDeleted marker, so malformed legacy
  null-after records are not reinterpreted as deletions. Shared request/schema checks
  and snapshot-pair reads also serve historical review, including deletion patches.
  Undo uses a shared confirmation notice, owned cancellation and a separate App service
  calling execution/persistence ports. Current read-only selection, running tasks,
  incomplete history and missing snapshots disable it. FullAccess created-file removal
  tightens only that request to AskEveryTime; committed records use an independent
  bounded save token even after interruption. Save failure retains the same history
  identity for persistence-only retry and prevents further undo or normal exit.
  The shutdown overlay exposes an explicit retry, never a default force exit.
  Pending and successful cleanup share their task; failed/cancelled cleanup can be
  retried without restarting models or tools or clearing unrecoverable run failures.
  Window ownership rejects duplicate attempts and late completion after disposal.
  See `SHUTDOWN_RECOVERY_ACCEPTANCE.md` for storage-recovery and native evidence.
  Restore/remove now verify current versions under the writer's shared gate, avoiding
  redundant unlocked reads competing with Windows replacement. This is not a multi-file transaction
  or an OS-level cross-process compare-and-swap. See `FILE_CHANGE_UNDO_ACCEPTANCE.md`.
  See `FILE_CHANGE_SUMMARY_ACCEPTANCE.md`, `FILE_CHANGE_SNAPSHOT_ACCEPTANCE.md`
  and `TASK_SNAPSHOT_REVIEW_ACCEPTANCE.md`.

- File-summary subscriptions follow message collection deltas; unrelated text and
  tool progress do not rebuild the summary. Equivalent file records retain their
  rows and collapsed visible snapshot. Reordering keeps conversation-order last-write
  semantics; removed and disposed rows cannot refresh the view. Git branch discovery
  uses symbolic HEAD so newly initialized repositories need no commit, with the same
  bounded runner for detached-HEAD fallback. No approval or publishing-policy change.

- App's repository-status projection owns every Git metadata read through the
  existing operation lifetime and Git port. Refresh generations reject obsolete
  results and reentrant stale starts; a pending selection shows unknown status
  instead of another project's branch. Shutdown and disposal cancel and join all
  reads, including a noncooperative port, before releasing the workspace. Repeated
  stop calls share one completion. Metadata errors use localized unknown labels,
  never raw exception messages; null-branch and worktree-path semantics remain
  unchanged. Library waits do not capture the caller's UI context; final status
  publication uses cancellable UI dispatch so closing can retract a queued update.
  No command, approval, persistence or native-process implementation is added to
  UI. See `REPOSITORY_STATUS_LIFETIME_ACCEPTANCE.md`.

- File-summary line statistics use the registered UI-only read-only
  `filesystem.change_statistics` tool, through the standard execution pipeline.
  Historical review and numstat share bounded task-owned diff staging and cleanup;
  statistics validate immutable snapshots without reading current files or creating
  patch artifacts. App merges refreshes, queries serially, cancels changed task or
  authorization contexts and waits for owned work at shutdown. Missing or incomplete
  history hides totals instead of inventing zeros; explicit retry only queries
  unavailable rows. Totals use one scan and publish only changed values, preserving
  resolved rows and their immutable array when just the completeness flag changes.
  Shared cards render per-file and aggregate statistics, with file-path accessible
  button names. See `FILE_CHANGE_STATISTICS_ACCEPTANCE.md` for focused, full and
  native verification; this is not a claim of full-product visual parity.

- File-summary typography, its compact plus/minus document glyph and the more-files
  chevron belong to shared FileChangeVariants. AppActionButton exposes validated
  trailing-icon spacing for scoped style overrides instead of trying to overwrite
  constructor-owned child margins; existing actions keep their default spacing.
  Per-file paths retain their immutable-snapshot commands and accessible names.
  See `FILE_CARD_FIDELITY_ACCEPTANCE.md` for native screenshots, narrow/wide geometry
  and multi-scale raster checks; card content is not substituted by a mockup.

- Model streaming and model discovery use separate Application ports. `IModelProvider`
  owns protocol selection and streaming only; settings retain `IModelDiscoveryService`
  for discovery, paging and provider/proxy validation. Adapters no longer carry an
  unused model-list projection or depend on discovery just to satisfy that contract.
  Gemini request composition lazily indexes assistant call identities once per build,
  preserves the first ordinal match (including a missing native identity), and appends
  consecutive tool responses directly to one content group without cloning JSON trees.
  The index is never shared between requests; signed content and security limits stay
  unchanged. See `GEMINI_CONTINUATION_ACCEPTANCE.md` for deterministic regression evidence.

- App.Tests and Infrastructure.Tests link the same stateless `SqliteTestPool`
  source from `tests/Shared`; neither test assembly depends on the other.
  Cleanup uses the production connection factory with an explicit test data root
  and retires only that database's pool. Outer workspace roots and nested `data`
  directories remain distinct. Tests retain default pooling and parallelism;
  teardown must not clear other fixtures' pools. Missing roots are rejected.
  See `SQLITE_TEST_POOL_ISOLATION_ACCEPTANCE.md` for cross-fixture evidence.

- Provider routing tests also link one bounded `Socks5TestHandshake` fixture from
  `tests/Shared`, checking protocol negotiation, CONNECT command, host and port.
  The real App configuration/discovery/streaming path covers HTTP and SOCKS5 for
  Responses, compatible Chat and Gemini, including edited endpoints, explicit
  direct routes and disabled-proxy rejection without fallback. This does not add
  another production proxy implementation or transfer test assembly dependencies.
  See `SOCKS5_PROVIDER_ACCEPTANCE.md` for native settings and loopback evidence.

- App's generic and non-generic guarded async commands share execution-state
  notifications, cancellation and disposal through the existing non-generic
  implementation. Typed entry points retain Toolkit's typed parameter validation
  without extra boxing; object entry points retain the existing null/type checks.
  Disabled, running and disposed commands still reject execution. Callback
  exceptions, owned execution tasks and per-editor operation deadlines are unchanged.

- App's project, pinned and recent task lists share task-row publication and
  selection helpers. Equal immutable snapshots retain their row instances even
  after position changes; changed snapshots receive fresh rows. Task and project
  publication share a remove/insert/move reconciler rather than resetting entire
  collections, including repeated references and independent duplicate task rows.
  Identical task snapshots avoid building row queues. Filtering, ordering and
  pinning remain owned by each list. See `SIDEBAR_RECONCILIATION_ACCEPTANCE.md`.
  Recent tasks include non-pinned conversations from the current project as well
  as other projects; project/draft navigation does not refilter or replace those
  rows. Recent-task deduplication uses `MaxBy`, retaining the first snapshot on
  tied timestamps. This changes no persistence or execution policy. See
  `RECENT_SIDEBAR_ACCEPTANCE.md` for current Codex reference and native evidence.

- App's selected-project task search publishes changed positions in one pass,
  retaining equal immutable snapshots without resetting the observable results.
  Sidebar search updates project subscriptions from collection deltas; moves
  preserve subscriptions, reset rebuilds them, and repeated references remain
  observed until their last removal. Ordering, matching, task metadata and owner
  disposal remain unchanged. No global collection semantics or execution policy
  changes. See `SEARCH_REFRESH_OPTIMIZATION_ACCEPTANCE.md`.

- App's task-owned message presentation lazily projects subagent dialogue. Repeated
  reads retain their row array and parsed Markdown; progress reuses unchanged
  role/content rows while changed rows receive independent snapshots. Raw tool
  payloads no longer own a presentation getter, so record equality, JIT JSON,
  persisted dialogue flags and task isolation are unaffected. No shared cache or
  appearance changes. See `SUBAGENT_DIALOGUE_PROJECTION_ACCEPTANCE.md`.

- App's streaming subagent dialogue creates one final array per progress event,
  without an intermediate list. Earlier snapshots remain untouched, and only a
  trailing partial reply from the same role is replaced. Sidebar activity sorting
  does not construct unused manual-rank dictionaries; project subscription insertion
  uses the existing set's result instead of a second lookup. No rendered geometry,
  task ownership or execution policy changes. See `UI_HOTPATH_OPTIMIZATION_ACCEPTANCE.md`.

- Agent's shared subagent context retains a bounded text tail while composing,
  instead of building full selected message bodies and then discarding the prefix.
  The last 12 relevant messages, role labels, historical-observation warning,
  newline order, 12,000-character limit and surrogate-safe cutoff stay unchanged.
  Native continuation metadata is still excluded. See `SUBAGENT_EXECUTION_ACCEPTANCE.md`
  for reference-equivalence and large-message allocation regression tests.

- App's project navigation publishes task summary and activity metadata from task
  mutations, not from search filtering. Project updates publish fallback activity
  only when an empty project's creation time changes. Search still updates rows,
  selection and visibility without triggering unrelated project sorts. Shared
  navigation styles combine identical state selectors without changing their
  setters. Native task-title baseline correction remains scoped to task text;
  hit areas and project-folder geometry stay unchanged. See
  `SIDEBAR_REFRESH_OPTIMIZATION_ACCEPTANCE.md` for regression and pixel evidence.

- Sidebar compose geometry belongs to App's shared vector/style resources.
  Its optical icon correction and icon-label spacing preserve the existing hit
  area and section rhythm, without changing global icon rendering or command
  routing. Native reference bounds and isolation/keyboard tests are recorded in
  `SIDEBAR_COMPOSE_ACCEPTANCE.md`.

- App's shared icon renderer accepts optional multilayer Drawing resources,
  reusing its bounded uniform-scale/center layout. Geometry-only icons retain
  their existing paint path. File-preview copy/image glyphs use optical variants
  without reducing button hit areas; the Explorer badge is a shared vector, not
  a separate file-opening implementation. Media drawings are allowed only inside
  resource owners by the visual-component gate; native controls remain rejected.
  See `PREVIEW_ICON_ACCEPTANCE.md` for Skia pixels and ordinary native evidence.

- App's chat picker, agent/subagent editors, and automation pages read enabled
  models through one `EnabledModelCatalog` using `IConfigurationRepository`.
  The reader checks provider ownership and cancellation; each consumer retains
  its own draft, selection, timeout and stale-result guards. Agent editors share
  `AgentModelOption` rather than maintaining duplicate option types.
  Each agent/subagent editor owns an `AgentModelSelection` instance for preferred
  provider/model identity and catalog publication. Equal catalogs retain their
  object references; unavailable choices never fall back to another model, and
  returning choices restore by both identities. Settings selection remains
  in-memory; conversation-specific choices have separate persistence. Reload version,
  cancellation, disposal and draft-revision checks remain in the owning editor.

- App's `AppModelPickerMenu` owns the quick/list composition and keyboard entry;
  `ModelPickerFlyoutView` is only its shared-component host. Existing picker
  bindings, commands and appearance variants remain unchanged. Deferred focus
  targets the search field, selected model row or quick navigation action only
  for the current attached, visible, enabled and open picker. Context replacement,
  closing and detachment invalidate queued transfers and release subscriptions.
  Automatic focus uses programmatic navigation; real Tab navigation retains its
  focus adorners. Markup and analyzer gates protect content, templates and geometry.
  No model I/O, persistence or tool pipeline moves into the component. See
  `MODEL_SELECTION_ACCEPTANCE.md` for native input and regression evidence.

- App's shared settings dialog owns attachment-scoped ancestor visibility and
  enabled-state subscriptions. Returning to a retained settings page restores an
  available last dialog field without changing drafts or TextBox selection policy;
  hidden, closed and detached owners invalidate queued transfers. A new open
  session resets only its template-owned body before visibility and autofocus,
  keeping the first field's label visible; retained open pages and nested scrollers
  are not reset. Shared appearance and modal scope stay unchanged. See
  `PROVIDER_DIALOG_NATIVE_ACCEPTANCE.md` and `SETTINGS_DIALOG_REOPEN_ACCEPTANCE.md`.

- App's proxy, browser, agent/subagent editors and task mutations share
  `SettingsOperationLifetime` for per-owner cancellation and operation deadlines.
  The model picker reuses the same component instead of maintaining another
  linked-token/deadline implementation; each reload still cancels its predecessor
  and rejects stale results. Cancellation or disposal never cancels another picker.
  Each owner keeps its own lifetime; caller cancellation never cancels sibling
  operations, and writes retain ownership until storage returns.
  The owner token remains stable, readable and cancelled after disposal; consumers
  reuse it without maintaining another cached lifetime token.
  The model picker retains immutable reasoning steps per selected snapshot
  instead of allocating a fresh list on every binding read.
  Its provider groups also keep immutable membership snapshots while rows retain
  observable selection. Whole-group search matches reuse the current group, and
  partial matches share rows without intermediate list copies. Each source group
  retains at most one immutable partial result; equal searches reuse it. Filtered
  groups use the existing collection synchronizer, avoiding list resets and
  preserving rendered row identity. Catalog reloads create new source groups, so
  retained search results cannot resurrect removed or disabled models. Explicit and Agent
  selections share one guarded current-row lookup, retaining their distinct event
  semantics; availability binding reads need no LINQ iterators or closures. See
  `MODEL_PICKER_OPTIMIZATION_ACCEPTANCE.md` for focused regression evidence.

- Application's `ModelReasoningPolicy` exposes stable immutable effort snapshots
  without per-query boxing, lowercasing model IDs or concatenating family suffixes.
  Default effort selection resolves capabilities once. App's model settings reuse
  one catalog refresh/notification sequence after reads and mutations, and resolve
  current editable rows once while preserving draft, lifetime and failure guards.

- Application owns an immutable, embedded `Data/model-capabilities.json` snapshot
  and exact, protocol-aware capability lookup. New model imports fill context,
  output, image/PDF flags and recommended effort from this offline catalog;
  existing rows and unknown aliases are never rewritten or guessed. App's shared
  `AppEditableChoice` supports selection and typing across model/agent editors.
  Compatible protocols allow bounded custom effort tokens; native protocol
  mappings retain their restrictions. The maintenance-only download script uses
  public metadata without provider credentials; discovery adds no extra network
  destination. See `MODEL_CAPABILITY_DATA.md` for provenance and limitations.

- Tools.Abstractions owns optional, metadata-only pipeline phase notifications;
  Tools.BuiltIn emits them without altering validation, approval, timeout or audit
  order. Agent forwards task/call-scoped phases through a bounded stream alongside
  subagent dialogue and retains execution ownership until cancellation/cleanup return.
  App updates one existing tool row from request checking through approval, execution
  and final result. Phase notifications are transient and contain no arguments, output
  or credentials; only existing final tool events are persisted. Observer failures must
  not approve, reject or otherwise change tool execution. See `TOOL_PROGRESS_ACCEPTANCE.md`.

- Tools.BuiltIn's executor owns a per-call deadline using the same `TimeProvider`
  for its timer and monotonic elapsed-time checks. It rechecks cancellation and
  elapsed time before invocation and after the owned tool returns, including when
  cancellation callbacks were delayed or a tool ignored its token. Late results
  retain bounded dialogue and created-artifact metadata and complete the audit as
  interrupted, never as success. Caller cancellation takes precedence over timeout.
  Started work remains owned until return; this does not undo side effects,
  preempt native calls or move
  the deadline ahead of approval. Windows tests use explicit deadline/gate signals
  and publish process readiness atomically rather than racing partial PID writes.
  Its separate output limiter shares one character budget across summaries,
  content and dialogue. Unchanged results and retained messages are reused without
  cloning; truncation preserves artifact metadata and Unicode surrogate pairs.
  Budget checks stop at the limit rather than summing an unbounded dialogue into
  a potentially overflowing integer. Audit completion still runs for reused results.

- App's file-preview tabs reuse the standard preview-file tool for original paths
  and Application's task-artifact reader port for already-audited snapshots.
  Tools.BuiltIn owns authorized, bounded snapshot creation; Infrastructure verifies
  task directory, links, length, digest and content type. The UI decodes memory only.
  Task/scope changes and tab closure cancel reads and reject late results. Shared
  file/code components own bounded tab geometry, image fitting, syntax, diff colors
  and gutters, protected by markup/analyzer gates. See `FILE_PREVIEW_ACCEPTANCE.md`
  for format/resource limits and native verification boundaries.
  Each tab also owns bounded rendered-snapshot search and image zoom state.
  Shared code/image controls own highlighting, scrolling and fit-on-resize geometry;
  search/zoom never reread files or invoke a separate execution path.
  Code preview owns a per-instance gutter transform over its existing single
  scroller. Horizontal scrolling moves code while line numbers and diff markers
  stay fixed; the opaque gutter shares its row's semantic/search surface and
  blocks hits on code behind it. Vertical layout remains one row tree, avoiding
  independently scrolling or mismatched line lists. Search navigation preserves
  horizontal position; content replacement resets scroll state. See
  `CODE_PREVIEW_GUTTER_ACCEPTANCE.md` for native pixels and remaining fidelity limits.
  Its clipboard controller copies only authorized in-memory snapshots/paths and
  rejects late notices after ownership changes. Showing an original file in Windows
  Explorer uses a task-scoped tool through the existing pipeline, with repeated path
  validation and an owned STA Shell operation; it never launches the file itself.
  Started clipboard/Shell operations cannot be revoked and remain owned until return.
  The shared breadcrumb prioritizes the measured filename over parent segments.
  Compact file tabs reserve their close-action slot without showing inactive chrome.
  File-tab close glyphs use a shared six-DIP, one-DIP-stroke muted variant while
  retaining the 24-DIP action slot and existing hover/focus/command behavior.
  Image zoom is an owned flyout, closed on context, tab, visibility, enablement or
  attachment changes; default preview no longer allocates a zoom strip or empty
  notice row. Search and zoom continue using the existing in-memory snapshot.
  The shared preview switches to a compact toolbar below 400 DIP, retaining copy
  and search/zoom while collecting the existing reveal/refresh commands in an
  owned menu. Fixed columns avoid reentrant grid-definition changes and unused
  trailing spacing. Shared styles own the isotropic surface shadow, 14-DIP action
  icons and eight-DIP image clipping; no business view supplies these dimensions.
  See `FILE_PREVIEW_FIDELITY_ACCEPTANCE.md` for pixel evidence and remaining gaps.
  File selection has separate, task-owned coordination with a five-minute human
  browsing deadline; it supplies paths only and reuses the same authorized preview
  pipeline with its existing 30-second read timeout. Noncooperative pickers retain
  ownership until return and cannot populate a new task with a late selection.
  Shared workspace panes own inspector expansion without destroying the primary
  content or losing preferred splitter width. The conversation header can open
  the inspector directly in full view; the ordinary inspector and preview headers
  share its restore command. Opening uses the existing two-way, conversation-owned
  panel state. Returning from preview to sources, hidden/disabled owners and
  detachment restore normal layout; active tabs remain visible after resizing.
  Shared headers refresh derived state only for properties they consume; the
  inspector reuses its instance-owned tab list and existing command parameters
  for selection, rather than allocating a new list on every layout notification.
  See `FILE_PREVIEW_WORKFLOW_ACCEPTANCE.md` for interaction and safety evidence.

- Application's automation scheduler uses repository, runner, lease and time-provider
  ports. The App lifecycle explicitly starts it after startup and cancels/awaits it
  before workspace shutdown. Infrastructure owns the exclusive data-directory lease
  and transactional version-nine migration. Revision checks and durable per-plan
  claims prevent overlapping runs and stale edits; interrupted runs are not replayed.
  App's background runner resolves the exact configured project/provider/model and
  reuses task-scoped agent execution without consuming the current draft or selection.
  Plans default to paused/read-only and only run while the application is open.
  Scheduler shutdown rejects new work and cancels owned/queued port operations
  before waiting for them, avoiding a cancellation-versus-gate deadlock. Concurrent
  stop/dispose calls share completion. Noncooperative ports, started execution and
  terminal persistence keep the lease until return; late durable claims are cancelled
  without invoking the runner. Failed finalization retains the durable claim and
  reports only the safe persistence error code rather than replaying uncertain work.
  App's separate history view/model reads at most 50 records through existing ports,
  distinguishes loading/failure/empty states and maps failure hints to localized text
  instead of echoing stored error codes. Selection, page visibility, mutation blocking
  and catalog locking invalidate late navigation; pending noncooperative reads retain
  opening ownership until returning. Shared thin-scrollbar variants reserve content
  gutters. This changes no schedule schema, execution policy or model routing.
  See `AUTOMATIONS_INSPECTOR_ACCEPTANCE.md` for safety, tests and native visual limits.
  App's separate plan-list model coalesces reads, preserves the last successful
  snapshot on failure and distinguishes loading, failure, refreshing and empty states.
  Scheduler notifications request a catch-up read after the current operation;
  the entire chain shares a 30-second cooperative deadline. Noncooperative ports
  retain read ownership until returning; disposal cancels and rejects late results.
  Current snapshot reference identity gates row commands, while failed/loading reads
  block mutations and history navigation, never Stop. Refresh preserves newer
  selection and revalidates pending removal targets; retry retains editor drafts.
  Shared notice/empty-state controls use localized safe errors without another
  execution path or schema change. See `AUTOMATION_LIST_ACCEPTANCE.md`.
  That record also distinguishes actual production loopback schedule execution
  from read-only restoration on later UI builds; restoring terminal runs never
  establishes that the newer UI binary executed them again.
  Plan editing and mutations use App-owned operation state and guarded commands.
  Catalog/write work has linked command/lifetime cancellation and a 30-second
  cooperative deadline; read-back retains the busy owner and uses the list's own
  deadline. Noncooperative ports keep ownership until return, and cancellation
  never claims to roll back a write or stop a run that already started. Editor
  context/draft revisions reject late publication. A successful save records its
  committed identity before read-back, preventing duplicate creation on retry;
  current stored revision/content must match before rebasing or closing a draft.
  Shared operation notices expose progress/cancel and the editor's enabled binding
  explicitly reads the owning dialog model. See `AUTOMATION_MUTATION_ACCEPTANCE.md`.
  Editor initialization calculates its schedule preview once after loading fields,
  retaining individual field notifications and draft/context revisions. History
  rebuilds use one bounded projection after the selection reset, without a second
  reset or layered LINQ iterators. See `AUTOMATION_LIST_ACCEPTANCE.md`.
- App's shared appearance previews own theme-scene geometry and semantic colors.
  Theme buttons keep their existing preference commands. The read-only synthetic
  diff uses two independent shared horizontal scrollers, never project file reads
  or an execution path. Preview-specific theme resources do not override the code
  inspector. See `SETTINGS_PREVIEW_FIDELITY_ACCEPTANCE.md` for native pixels and limits.
- The read-only appearance diff keeps native line/page/thumb scrolling, with
  shared filled-arrow buttons and a six-DIP thumb. Its real text determines
  horizontal extent; there is no forced minimum causing blank scrolling.
  Zero-range bars retain visible muted arrows but disable interaction and hide
  the thumb. Preview-only syntax resources live in ThemePreviewVariants and do
  not replace the inspector's CodeKeyword/CodeType palette. See
  `PREVIEW_SCROLL_SYNTAX_ACCEPTANCE.md` for reference-crop and native evidence.
- App's shared inspector header and empty state own tab/action geometry, localized
  labels, selected/disabled states and empty-content presentation. Diff, audit and
  terminal view models reject late results after task/selection changes and dispose
  subscriptions. Terminal start/write/stop use the standard tool executor and captured
  task context, not the platform session manager directly. Missing browser views show
  an empty state rather than an unusable toolbar. Markup and analyzer gates continue
  to reject native visual controls and appearance/composition replacement.
  Platform.Windows registers each terminal owner before asynchronous ConPTY creation.
  Repeated starts share creation; stop/disposal reject new work and retain late
  creation, connection cleanup and in-flight writes until return. Porta.Pty's owned
  Job handles process-tree lifetime; connection closure keeps output draining.
  Stateful UTF-8 decoding preserves arbitrary pipe boundaries and isolated observers
  cannot terminate the reader. See `TERMINAL_LIFECYCLE_ACCEPTANCE.md` for real process
  evidence and remaining terminal rendering, backpressure and native shutdown limits.
  App's shared terminal pane owns localized pending/cancelling/refusal states and
  preserves a separate pipeline operation owner per task until return. Switching
  tasks cancels old scopes without blocking independent tasks or accepting old results.
  Its locked output projection coalesces one pending dispatcher callback and caps both
  queued and displayed text at 200,000 UTF-16 units, with explicit truncation notices.
  Shared variants own geometry and theme-aware disabled danger actions; markup and
  analyzer gates reject terminal composition replacement. See `TERMINAL_PANE_ACCEPTANCE.md`
  for native component evidence and remaining VT/full-workspace limits.
  Tools.Abstractions additionally defines an optional metadata-only terminal state source.
  Windows publishes revisioned, session-owned snapshots and retains natural termination
  until output delivery and connection/Job cleanup return. Completed metadata is capped;
  cleanup failures retain ownership and block replacement. App coalesces state callbacks,
  revalidates snapshots before tool admission and keeps operation notices separate from
  session state. Shared empty-state content uses bounded available width so title changes
  cannot remain clipped to a prior intrinsic width. See `TERMINAL_STATE_ACCEPTANCE.md`
  for native natural-exit evidence, reproduced title clipping and verification limits.
  App additionally parses the untruncated producer stream through a managed VT core.
  Only explicitly admitted task scopes are cached (eight screens, 128 history rows
  each); session-tagged output, scope identity and revisions reject stale data.
  Opaque host/graphics strings are discarded without accumulating their payloads.
  A single background projection worker coalesces immutable grid frames, and shared
  controls draw only visible cells with a bounded shaping cache. Frame publication
  revalidates screen/session ownership; task switching and alternate-screen changes
  restore separate scroll positions. Reserved thin scrollbar gutters keep the last
  row and cursor visible. The older 200,000-unit text projection remains diagnostic,
  not the render/parser input. No terminal responses or host callbacks bypass tools.
  Revisioned terminal metadata now carries successfully applied grid dimensions.
  Session-bound write/resize ports validate task, session and canonical working
  directory; both tools check authorized paths before approval and again on execution.
  Resize is side-effecting and retains native ownership through timeout/stop cleanup.
  App coalesces measured dimensions through the same tool pipeline, preserving VT
  content and bounding primary/alternate cell caches under variable geometry.
  Shared viewport events route text and special keys through a separate, bounded,
  task-owned input coordinator. Classic/application cursor and Win32 input modes
  follow the immutable frame; rejected/cancelled input is discarded, never replayed.
  Noncooperative writes retain ownership and visibly block same-task replacement;
  independent tasks remain usable. PowerShell starts with explicit UTF-8 console
  encodings so combining/supplementary output does not degrade to the system code page.
  Fixed shared toolbar height avoids busy-action/measurement feedback. App's terminal
  selection copies bounded immutable rendered cells, including graphemes, wide cells,
  history and soft-wrap metadata; changed selected rows or owner/grid changes clear it.
  Explicit user clipboard gestures reuse Avalonia's API, never expose a model clipboard
  port, and retain noncooperative operations until return. Paste rejects embedded control
  sequences, confirms newlines, captures scope/session/modes and enters the same input
  pipeline as an exclusive, bounded operation. Win32 paste sends press/release records;
  VT envelope characters are not allowed to escape from clipboard content. Shared menus,
  notices and selection brushes own presentation. The viewport caches its owned screen
  and scroller after markup loading, instead of repeating namescope lookups on every
  input/layout event. Scroll restoration and layout publish candidate geometry once
  per path, using one measured visible rectangle rather than repeated translations.
  Avalonia input-method clients present bounded, underlined preedit locally and expose
  viewport-clamped candidate geometry without surrounding terminal text. Only committed
  TextInput reaches the existing input pipeline; owner/scope/command changes and lost
  eligibility invalidate old clients. VT responses and physical Windows IME acceptance
  remain separate. See `TERMINAL_IME_ACCEPTANCE.md`, `TERMINAL_CLIPBOARD_ACCEPTANCE.md`,
  `TERMINAL_VT_ACCEPTANCE.md` and
  `TERMINAL_INTERACTION_ACCEPTANCE.md` for evidence
  and native/full-workspace verification boundaries.
  Diff review reuses the standard status/diff tools, lists individual untracked
  files and explicitly requests single-file no-index patches without staging them.
  Git uses literal paths, readable Unicode headers and disabled external/textconv
  helpers; command-local CRLF warning suppression keeps valid read-only patches
  successful while real stderr errors remain failures. Refresh/filter changes retain
  snapshot-owned selection where listed, rebind equal records and update counts.
  Localized loading/failure/empty messages stay outside code content. See
  `DIFF_REVIEW_ACCEPTANCE.md` for real Git/native evidence and remaining fidelity gaps.
  Audit rows use a shared semantic component with local-time/elapsed metadata and
  allowlisted localized failure hints instead of raw stored diagnostic codes.
  Refresh preserves the current snapshot-owned selection, including newer user
  choices made during a pending read, and rebinds equal-valued record replacements.
  Visible history is task filtered/deduplicated/capped at 200; artifacts additionally
  require execution ownership and display at most 50. These are UI limits, not a
  bounded artifact repository query. Snapshot opening reuses the existing preview
  reader and leaves the task draft unchanged. See `AUDIT_INSPECTOR_ACCEPTANCE.md`
  for negative tests, native interaction evidence and remaining Codex/DPI gaps.

- App's shared `AppText` computes opt-in single-line overflow during arrangement;
  navigation styles apply a fade only to overflowing task titles. Fixed task line
  height keeps mixed-script labels centered with their actions without replacing
  user font preferences or moving project/group anchors. No port, tool policy or
  persistence change; see `SIDEBAR_TASK_TEXT_ACCEPTANCE.md` for raw-pixel evidence
  and remaining typography/DPI differences.
- App shared `AppNavigationRail` owns the left rail's actions, localized names,
  measured geometry and flyout lifetime. Views supply existing commands and help
  business content. Missing commands/content disable entries, CanExecute remains
  authoritative, and ownership changes, hiding, disabling or detachment close menus.
  Shared styles and markup/analyzer guards protect composition and appearance.
  Task activity animation is isolated in its own icon theme so its initialization
  cannot replace unrelated icons' transforms. No port, persistence or tool-policy
  changes; see `NAVIGATION_RAIL_COMPONENT_ACCEPTANCE.md` for evidence and limits.
- Serialization stays in its owning module and uses ordinary runtime JSON metadata.
  Model tool payloads, MCP envelopes, persisted header/root collections and browser-tab
  metadata retain their contracts. App components and AXAML use ordinary runtime
  bindings through shared visual controls. No persistence migration, port, authorization
  or tool-policy change is required.
- Desktop builds and runs use managed/JIT mode. The Windows x64 publish profile creates
  a self-contained, compressed single-file package without trimming, bundling the .NET
  runtime, native libraries and Windows Playwright driver. Publishing stages and validates
  the executable and archive before replacement and preserves previous packages on
  failure. Runtime dependencies are extracted to the .NET bundle cache; user data stays
  in the separate application data directory. See the README for publishing and cache use.
- Browser's Playwright factory retains ownership across SDK initialization, launch,
  CDP connection and page creation until each non-cancellable SDK operation returns.
  Cancellation rejects handoff and awaits cleanup of owned contexts or newly created
  pages, while borrowed CDP contexts and existing pages remain untouched. Runtime
  disposal follows cleanup even if owned context closure fails. This reuses existing
  browser ports and tool policy, with no persistence migration; native evidence and
  cancellation limits are recorded in `BROWSER_EXECUTION_ACCEPTANCE.md`.
- Browser settings coalesce proxy-catalog notifications while reads or writes own
  the editor. Accepted operations await queued refreshes before returning; save also
  drains notifications received during its pre-write catalog check. Pending refreshes
  block new saves, preserve dirty browser/profile fields and unavailable proxy identity,
  and keep failed refreshes unavailable until recovery. Lifetime cancellation rejects
  late results, and noncooperative writes retain ownership until storage returns.
  Disposal discards queued work. This reuses existing ports and changes no tool policy,
  browser session routing or persistence schema.

- App shared `AppResponseContent` and `AppResponseTable` own main/child response
  block templates, table geometry and bounded code/table scrolling. Views provide
  parsed blocks, table data and the existing clipboard command; the compact child
  variant belongs to shared styles. Literal tool/status/task content stays unchanged.
  Semantic paragraph spacing is applied to the shared text-block root, not an
  unreachable wrapper-type selector on generated presenters. Inline code remains
  selectable native runs with shared typography and theme-aware backgrounds; style
  updates preserve selection and stop tracking replaced runs. Markdown rendering
  enumerates paragraphs without an intermediate array and reuses container creation
  for links. All shared selectable outputs reuse the native SelectableTextBlock theme
  without changing concrete style keys. Inline-code rectangles are drawn from UTF-16
  text-layout ranges before native selection and glyphs, so rounded backgrounds do
  not cover the selection or change measurement/copy text. Markup and analyzer rules
  protect code-font, background geometry and selection tokens. Shared composed-surface
  ownership is checked once per rule instead of maintaining three duplicate type lists.
  See `RESPONSE_CONTENT_FIDELITY_ACCEPTANCE.md` for pixel evidence and remaining gaps.
  Missing commands disable code copy, while command CanExecute and keyboard behavior
  remain authoritative. Derived display blocks are excluded from serialized tool
  payloads. Markup guards and `MOSSUI002` reject business composition and geometry
  replacement. No application port, tool policy, model routing or migration changes;
  see the 2026-10-07 section of `SUBAGENT_EXECUTION_ACCEPTANCE.md`.

- Numeric fields use App's shared `NumericFieldVariants.axaml` and `AppRepeatButton`,
  retaining NumericUpDown parsing, repeat, keyboard and range handling. The component
  owns centered small arrow geometry, hit areas, shared theme brushes and localized
  accessible names. `ValidateSharedRepeatButtons` and `MOSSUI002` reject business
  geometry overrides, including native owner-qualified property elements. No port,
  tool policy or persistence change; see `NUMERIC_FIELD_COMPONENT_ACCEPTANCE.md`.
- Proxy settings mutations reuse App's `SettingsEditorViewModel`,
  `SettingsMutationRunner` and guarded commands through `IConfigurationRepository`.
  Captured identity/revision protect newer drafts; dirty fields survive reloads and
  removed targets cannot be recreated. Mutation ownership includes reload and catalog
  notification. Lifetime/command cancellation and a 30-second cooperative deadline
  reach storage; non-cooperative writes keep ownership until returning. Safe localized
  errors and existing shared status variants distinguish storage, reload and
  post-commit notification failure. No migration or tool-policy change; see
  `PROXY_EDITOR_MUTATION_ACCEPTANCE.md` for rejection and native persistence evidence.

- App shared `AppConversationHeader` owns conversation heading/status allocation,
  action geometry and menu lifetime. Views supply heading/menu business content,
  semantic state and existing commands; owned hosts inherit the business DataContext.
  Long startup warnings and running activity ellipsize inside finite columns instead
  of displacing actions. Missing targets or commands disable entries, CanExecute
  remains authoritative, and ownership changes or detach close the menu. The chat
  inspector entry is hidden while docked; the inspector's existing close action
  remains available. Shared styles, markup guards and `MOSSUI002` own appearance
  and composition. No port, tool policy or persistence change; see
  `CONVERSATION_HEADER_COMPONENT_ACCEPTANCE.md` for failed baselines and native limits.

- App shared `AppConversationTab` owns tab selection/options geometry, localized menu
  labels, explicit command targets and popup lifetime. Views supply semantic state and
  existing commands; popup bindings do not depend on a Window view model. Missing
  targets/commands and CanExecute disable entries. Scope changes, hiding, disabling
  and detachment close the popup and retire subscriptions. Menu dismissal runs after
  button command execution and rejects obsolete callbacks after reopening. The owned
  menu focuses its action for keyboard use. Workspace disposal also retires its tab
  model's catalog subscriptions and commands. Markup/analyzer gates protect composition
  and geometry. Closing a tab never archives/deletes its conversation. This changes
  no port, tool policy or persistence; see `CONVERSATION_TAB_COMPONENT_ACCEPTANCE.md`.

- Workspace agent selection belongs to App's `WorkspaceAgentPickerViewModel`,
  through existing configuration repository ports and the shared model picker.
  It preserves the preferred identity when an agent or its model becomes unavailable,
  resolves stale menu targets against the current snapshot, and distinguishes explicit
  model choices from catalog refreshes. Loading or failed reads block new sends,
  never the running task's Stop action; captured requests remain unchanged.
  Linked cancellation, a 30-second read deadline and generation checks reject late
  publication. Shared composer controls present safe notices and a bounded agent menu.
  No migration or tool-policy change; see `WORKSPACE_AGENT_SELECTION_ACCEPTANCE.md`.

- App's `WorkspaceConversationPreferences` owns task-specific model, reasoning and
  preferred Agent identities through Application's `IConversationPreferenceRepository`.
  Infrastructure's transactional version-eleven migration adds a separate SQLite
  table without changing task metadata or conversation history; provider/model/Agent
  deletion never cascades away a preference. Only identities, canonical effort values
  and the Agent-default source flag are stored, never credentials or message content.
  Missing choices remain visible and block sending until explicitly repaired or
  replaced. Agent defaults track current configuration; explicit reasoning overrides
  survive refresh, task switching and restart. A retained identity needs no fabricated
  Agent snapshot when its configuration is absent. Model/Agent/conversation reads
  share the existing per-owner deadline component; generations reject stale reads.
  Writes are serialized and coalesced per task, and normal shutdown drains owned
  writes. Noncooperative writes keep ownership until storage returns. Reads fail closed
  with localized notices; write failure retains the in-memory choice for explicit retry.
  `WorkspaceBackgroundRunCoordinator` owns the extracted scheduled-run startup and
  captures its exact model preference without changing execution or approval policies.
  See `CONVERSATION_PREFERENCE_ACCEPTANCE.md` for regression and restart evidence.

- App shared `AppComposerContextBar` owns the composer's project, branch and agent
  presentation, bounded adaptive columns and flyout lifetime. Views bind semantic
  data and existing commands; the project picker inherits business DataContext.
  Late agent loading changes existing column definitions instead of replacing the
  collection, keeping Avalonia's measurement subscriptions current. Missing data,
  picker content or commands disable entries; command CanExecute remains authoritative.
  Shared styles own icon, spacing, surface and menu geometry, while markup guards
  and `MOSSUI002` reject composition and dimension overrides. This changes no port,
  tool policy, persistence or provider request. See `COMPOSER_CONTEXT_COMPONENT_ACCEPTANCE.md`
  for reproduced overflow, rejected-path tests and ordinary-permission native evidence.

- App shared `AppComposerInput` owns focused Enter/Ctrl+Enter submission and leaves
  Shift+Enter to TextBox newline editing. It preserves the existing input template
  and IME client, guards active preedit and held-key repeats, and invokes only the
  bound existing command. The composer disables this path during preparation/run;
  Stop remains an explicit button rather than a chat-wide keyboard side effect.
  Hidden, disabled, readonly, detached, unfocused, blank or command-ineligible inputs
  cannot submit. No tool, approval, provider or persistence contract changes; see
  `COMPOSER_KEYBOARD_ACCEPTANCE.md` for loopback, native and IME validation limits.

- Project editing uses a dedicated bounded flyout view with a scrolling directory
  body and fixed field/action regions. Shared variants own its appearance;
  keyboard focus reveals row actions and remains inside the editor. The sidebar
  create trigger explicitly resets edit identity. Busy/disposed command guards
  preserve an in-flight draft, while close retains unsaved input. Composition
  exposes the existing service registrations for deterministic port substitution
  without changing production defaults. See `PROJECT_EDITOR_VIEWPORT_ACCEPTANCE.md`.

- Project editor mutations capture the project identity and draft revision before
  storage. A completed older snapshot still refreshes the catalog, but cannot clear
  a newer draft or another editor context. App-owned `GuardedAsyncRelayCommand`
  checks CanExecute before both invocation paths, preventing a rejected reentry
  from cancelling the existing operation inside the toolkit. Editor lifetime and
  command cancellation reach the existing workspace/folder-picker ports; saves
  request cancellation after 30 seconds. Owner disposal rejects late publication
  and detaches subscriptions. Validation uses allowlisted failure categories and
  localized hints; storage/picker exceptions never appear verbatim in the UI.
  No migration or tool-authorization change; see `PROJECT_MANAGEMENT_ACCEPTANCE.md`.
- App shared `AppProjectNavigationRow` owns the sidebar project header, bounded
  label, expanded folder state, independent select/expand actions and summary
  flyout lifetime. Views supply semantic data, existing commands and summary
  business content; the owned content host forwards the business DataContext.
  Shared variants own dedicated project-folder geometry, optical alignment,
  stroke and spacing without changing global folder icons. Markup guards and `MOSSUI002`
  reject content, padding, dimensions and alignment overrides. Missing targets or
  commands disable actions; CanExecute remains authoritative. Target, DataContext,
  command or content replacement, unavailable ancestors, command rejection and
  detachment close the summary and reset drag ownership. Tree subscriptions are
  removed on detachment. No application port, persistence or tool-policy change;
  see `PROJECT_ROW_COMPONENT_ACCEPTANCE.md` for regressions, pixels and native limits.
- App's sidebar uses one navigation viewport for pinned tasks, project groups and
  recent tasks. `NavigationActionVariants.axaml` owns group rhythm, list insets,
  title fading, the light rounded scroll thumb and action/status allocation.
  The header uses the existing pinned-list availability to lift only the pinned
  section title while preserving task/project positions and the no-pin layout;
  see `SIDEBAR_HEADING_ACCEPTANCE.md` for native and complete-window evidence.
  `AppTaskNavigationRow` reserves its trailing action slot only while running,
  hovered, focused or showing its menu, and closes menus on target replacement
  or detachment. The activity marker derives from each task snapshot; selection
  does not imply execution. Brush and animation value objects are not visual
  controls; native buttons, text, shapes and scroll viewers remain forbidden.
  Existing catalog commands, authorization, persistence and JIT packaging are
  unchanged. See `SIDEBAR_FIDELITY_ACCEPTANCE.md` for validation and remaining gaps.
- The sidebar's shared scroll variant cancels Fluent's inherited panning-thumb
  transform and owns a six-DIP rounded visual inside a twelve-DIP hit area.
  Its viewport extends to the sidebar edge while matching content padding keeps
  row widths unchanged. Line-arrow chrome is hidden; page actions, wheel input
  and thumb dragging still use the framework's existing scrolling behavior.
  Dedicated theme brushes do not change global separators or other scroll viewers.
  No port, persistence, authorization or packaging change is involved. Headless
  checks cover transformed bounds, hover/pressed chrome, disabled input, fitting
  content and drag scrolling; native pixel evidence and limits are in
  `SIDEBAR_FIDELITY_ACCEPTANCE.md`.
- Content, thin navigation lists and terminal viewports share
  `ScrollBarVariants.axaml` for transparent tracks, six-DIP rounded visuals and
  twelve-DIP hit areas. Feature styles retain only palette and behavioral
  differences; diff previews keep their sixteen-DIP horizontal bar and arrow
  buttons. Existing template parts still own scrolling, auto-hide and disabled
  input; settings-page scrollbar visibility remains unchanged. Production-window
  comparison and regression limits are in `CONTENT_SCROLLBAR_ACCEPTANCE.md`.
- App's shared project row owns thresholded native dragging and themed insertion
  feedback. An in-process payload is limited to one sidebar's reorder scope and
  current project identities; external data and stale/disabled targets are rejected.
  The shared navigation viewport owns edge scrolling, stopping on drop, invalidation
  and detach without treating an internal child transition as a viewport exit.
  Hidden or disabled rows, viewports and ancestors reject late dragging and stop
  timer scrolling; drag events and timer ticks share the same availability checks.
  Reordering uses the existing sidebar-preference port, guarded command ownership
  and atomic persistence, preserving selection and expansion without changing the
  catalog source collection. Current source/target identities resolve in one pass;
  unchanged insertion positions return before building an ID order or saving.
  No tool policy, migration or packaging change is involved. See
  `SIDEBAR_PROJECT_DRAG_ACCEPTANCE.md` for native input and restart evidence.
- App shared `AppSidebarSearch` owns the search trigger, flat input, clear action,
  empty-result notice and popup lifetime. `WorkspaceSidebarSearchViewModel` adapts
  the existing catalog search and aggregates project, pinned and recent results,
  rather than treating the selected project's task list as the entire catalog.
  Ctrl+K only intercepts a visible, enabled, attached sidebar; Escape keeps the
  filter. Generation checks reject queued focus after closure, context replacement
  or detach, and guarded restoration does not steal another editor's focus.
  Missing or unavailable clear commands remain disabled; disposed presentation
  state rejects late mutations and removes catalog subscriptions. Shared styles,
  markup guards and `MOSSUI002` own composition and geometry. No repository port,
  tool policy, migration, model request or JIT packaging change is involved; see
  `SIDEBAR_SEARCH_COMPONENT_ACCEPTANCE.md` for native evidence and fidelity limits.
  Header bell/search geometry is supplied by semantic shared variants, scoped to
  direct header glyphs so search popup clear actions retain their own appearance.
  The general icon resources and 32-DIP header hit areas remain unchanged. Markup
  tests accept only unconditional typed variants backed by existing geometry
  resources; hover-only or unresolved defaults do not satisfy the icon contract.
- App shared `AppWorkspacePanes` owns workspace columns, floating context geometry
  and the docked inspector's retained preferred width. Views supply pane instances
  and bind visibility; `AppPaneSplitter` wraps Avalonia's pointer and keyboard
  resizing. Shared styles own minimum widths, default proportion and handle size.
  View models do not compute columns or reconstruct task/browser panes on resize.
  Column subscriptions follow visual-tree lifetime; temporary width clamping does
  not overwrite the user's preference. Final arrangement recalculates shared geometry
  from actual available width, since native maximization can arrange a wider pane
  than its earlier measured constraint. Its template is measured again at that
  width before arrangement, so adaptive child controls do not retain an obsolete
  compact/wide mode even when the outer columns look correct. `AppBrowserViewport` also observes ancestor
  bounds, visibility and transform placement while attached, so a floating-to-docked
  transition updates native presentation even when the viewport's local bounds do
  not change. Detachment removes every ancestor subscription and parks the view;
  approval cancellation restores the latest placement without replacing the session.
  Markup guards and `MOSSUI002` reject geometry
  and composition overrides. This changes no tool policy, authorization, provider
  request or persistence contract. See `WORKSPACE_PANE_COMPONENT_ACCEPTANCE.md` for
  layout, lifecycle, rejected-path tests and ordinary-permission native evidence.
- App shared `AppProjectPrompt` and `AppApprovalPolicyButton` own the empty-session
  heading and composer permission presentation. Views bind semantic data and the
  existing project/policy commands, not child controls or dimensions. Shared styles
  own bounded labels, centered icons and menu appearance; compiler and markup guards
  reject composition/action overrides. Neither component changes approval policy,
  authorizes tools, accesses files or introduces a persistence migration. Missing
  commands are unavailable and command CanExecute remains authoritative. Focused
  regressions cover narrow/wide layout, resize, live bindings, rejection and ownership;
  see `WORKSPACE_PROMPT_COMPONENT_ACCEPTANCE.md` for passive native pixels and limits.
  The heading selects inline or stacked layout from its styled children's measured
  widths, not a fixed viewport breakpoint. Existing bounds and project-name trimming
  remain shared styles; the project button is retained through reflow. See
  `PROJECT_PROMPT_RESPONSIVE_ACCEPTANCE.md` for regression and native resize evidence.
- Tool approval presentation keeps its existing identity-bound decisions and queue.
  App shared variants constrain the card to the available viewport; only its body
  scrolls, while decision buttons remain visible. No policy, tool execution, native
  browser ownership or persistence boundary changes. Shared action rows preserve
  explicit accessible names and align internal icon, label and badge geometry.
  Async Headless regressions use an awaiting dispatch adapter, with exception and
  cancellation probes and a source gate against async Action callbacks. See
  `TOOL_APPROVAL_LAYOUT_ACCEPTANCE.md` for reproduced failures, native pixels and limits.
- AppApprovalButton captures command and request identity at gesture start, retaining
  it across held Enter and overlapping activation keys. A cancelled request cannot
  transfer a held mouse/keyboard decision to its successor through a rebound
  CommandParameter. The two decision controls share AppButton styling and native
  activation; VM identity checks and execution policies stay unchanged. See
  `APPROVAL_GESTURE_IDENTITY_ACCEPTANCE.md` for the failing baseline, rendered file/
  audit integration and passive native comparisons, not automated native approval.
- MainWindow owns one global approval surface outside the retained settings/workspace
  page switchers, below the shutdown overlay. Full-page settings opt into Continue
  keyboard navigation through AppPageSwitcher; other secondary pages keep Cycle.
  Requests do not close settings, steal editor focus or resolve automatically.
  Identity-bound queue/cancellation and browser presentation blocking are unchanged.
  See `GLOBAL_APPROVAL_PRESENTATION_ACCEPTANCE.md` for production-window regressions
  and passive Python captures of synthetic pending requests, not tool execution
  or same-state Codex approval pixel equivalence.
- Avalonia packages are aligned at 12.1.3, the first patch containing the Headless
  startup/queue-shutdown fixes. A session's consumer task is assigned before it
  starts, and cancellation joins the consumer before closing its queue. Immediate
  disposal, rejected post-disposal dispatch and fresh application scopes after
  completed/failed callbacks have focused regressions. No retry, exception
  suppression, reflection workaround or shared mutable test session is used.
- Provider/model writes and discovered-model imports share the App catalog's
  `SettingsMutationRunner`. It prevents overlapping configuration mutations,
  forwards the editor lifetime token, and turns storage failures into localized
  safe errors without publishing exception text. Editors track draft revisions;
  a late successful write cannot clear a newer or changed draft. Proxy validation
  rejects a draft that changed while awaiting the repository. Deleting another
  model does not reset the current editor. New drafts retain one identity through
  retries. A successful first commit attaches that identity without reloading or
  overwriting fields edited while saving; refreshed catalogs can then reject a
  deleted target instead of recreating it. Failure callbacks publish only to the
  current live draft revision. Failed saves refresh the catalog without declaring
  success or replaying the write. A persisted draft identity is attached from the
  successful read without replacing edited fields; failed reads disable retry
  until a successful refresh. SQLite model updates prioritize the stable row ID
  and enforce its provider ownership. Changing the API model ID rejects collisions
  and rolls back default changes instead of merging another row; new identities
  retain the existing provider/API-ID upsert behavior. This reuses configuration
  ports and
  shared controls, with no migration or tool-policy change. See
  `CONFIGURATION_MUTATION_ACCEPTANCE.md` for rejected-path tests and native input.
- Provider and model settings share one App-owned catalog snapshot through existing
  configuration repository ports. All reads finish before publication; loading,
  safe failure and successful-empty states are distinct. Previous rows and open
  editor drafts survive failed or overlapping refreshes, but mutations require a
  current successful snapshot and matching provider/model identity. A removed target
  cannot silently rebind or resurrect itself. Catalog reads, proxy reloads and
  save-time proxy validation reuse `SettingsOperationLifetime` with the unchanged
  30-second default. Async waiting is bounded even for noncooperative reads;
  cancellation/version checks also reject completed tasks returned after cancellation,
  which `WaitAsync` alone cannot reject. This cannot preempt a synchronously blocked
  repository call. Disposal prevents late notices or writes; discovery also cancels
  with its settings owner. Subscriptions are released with their owners.
  Discovery imports pass the provider to a static model factory instead of
  allocating a capturing factory per fetch. Provider-row fetch commands forward
  the owned asynchronous operation without another async wrapper.
  App's separate `ModelDiscoveryOperation` owns discovery/import coordination and
  localized safe failure mapping. Page operation results and the model editor's
  validation/save error have independent presentation; discovery or another row's
  deletion cannot replace that editor error. New/reset/closed editors clear only
  their draft status, while retained-page navigation preserves it. Draft revisions
  continue rejecting late mutation errors. No configuration port or schema changes.
  The page reuses shared Controls and Styles, with no new persistence schema,
  concrete infrastructure dependency or tool-policy exception. See
  `PROVIDER_CATALOG_ACCEPTANCE.md` for focused tests, native pixels and limits.
- Proxy, MCP, agent and subagent list presentation belongs to App shared
  `AppSettingsItemRow`. Its finite-width title, detail, metadata, badge and optional
  target-bound actions are composed once; shared styles own centered 24-DIP actions,
  compact rows and two-line descriptions. Business views bind data and existing
  commands, without replacing composition or dimensions. MCP keeps the existing
  safe endpoint summary; no credentials are added to visible text or tooltips.
  This reuses Controls and Styles without a new application port, database migration,
  or change to authorization, approval, timeout, cancellation and output policies.
  One type per file and size limits remain enforced; focused regressions cover layout,
  command targets, disabled state, safe presentation and rejected appearance overrides.
  See `SETTINGS_ITEM_COMPONENT_ACCEPTANCE.md` for native evidence and validation limits.
- Provider headers and model settings rows belong to App shared
  `AppProviderSettingsHeader` and `AppModelSettingsRow`. They own finite-width
  label columns, truncation, accessible actions and target-bound command parameters;
  shared styles own aligned 24-DIP action buttons. Missing data/commands disable
  actions, while the existing command CanExecute rules still apply. Views bind
  data and existing settings commands rather than rebuilding the row. Compile
  guards reject composition and dimension overrides. This changes no port,
  persistence, provider request or tool policy; see `PROVIDER_ROW_COMPONENT_ACCEPTANCE.md`
  for rendered overflow evidence, page/command regression and native limits.
- Task failures use a stable, allowlisted Domain category, not persisted provider
  messages or arbitrary error codes. Application classifies known codes and
  exception types; App supplies localized actionable hints through the shared
  `AppTaskFailureNotice`. Its wrapping, icon alignment and card appearance belong
  to Controls and shared Styles, with C#/AXAML guards against instance overrides.
  Infrastructure stores the category through the existing atomic workspace port
  and V7/V8 forward migrations; legacy failures retain a safe generic fallback.
  V8 expands the category constraint for provider blocking and output limits in
  one transaction while preserving prior failure records and workspace data.
  Resume clears the category, and own cancellation remains cancellation. Tool
  authorization, approval, timeout and output policies are unchanged; restoration
  never replays a failed run. See `TASK_FAILURE_ACCEPTANCE.md` for safety,
  preservation, layout and ordinary-permission native evidence.
- Agent owns the lifetime of a child execution that streams progress through a
  bounded channel. A consumer break, event-presentation failure or iterator
  disposal cancels only that child with a linked token, then waits for the existing
  tool executor to finish cleanup and audit. Parent and concurrent task tokens are
  not cancelled by this local cleanup. Normal cancellation still publishes the
  final bounded tool result before stopping. This reuses existing contracts and
  approval, timeout, output and persistence policies, without platform or UI access.
  Focused regressions cover abandoned previews, presentation exceptions, audit
  draining and shared-runner task isolation; no new schema or replay is introduced.
- Persisted tool results resume as provider-neutral `ToolObservation` data,
  not native calls with fabricated arguments. Application owns the bounded,
  Unicode-safe observation format; App reuses the workspace repository and rejects
  foreign-task history and unknown roles before model dispatch. Providers encode
  observations as ordinary user data and cannot emit native calls from that role.
  Agent context budgeting retains a clipped recent history group when possible,
  preserves complete native call groups, and refuses metadata that cannot fit.
  Shared subagent context includes labeled observations only when sharing is
  enabled; instructions treat file and tool results as untrusted, non-replayable
  data. Stored tool records and UI presentation are unchanged, with no schema or
  migration change and no new credential or argument persistence. Tool policies
  still apply to every newly requested operation. Focused tests cover legacy data,
  role/task rejection, protocol serialization, budgets, Unicode and real local
  file-tool-to-SQLite-to-follow-up requests, including read-only refusal.
- Attachment presentation belongs to App shared `AppAttachmentChip`; composer
  and message views bind file labels and an optional removal command. The control
  owns alignment, truncation, accessible text and command visibility; shared styles
  own sizing and the symmetric surface shadow. Compile guards reject business
  replacement of content, padding or dimensions. This reuses existing attachment
  commands, adds no file access or persistence migration, and changes no tool
  authorization, approval, timeout, cancellation or output policy. Focused tests
  cover owned bindings, removal availability, read-only messages, long labels,
  light/dark layout and negative boundary cases; each type stays in its own file
  and methods and files remain within the engineering hard limits.
- Task creation/resume and their user message, final status and assistant text,
  and assistant/tool event segments use the atomic `SaveTaskWithMessagesAsync`
  repository port. Infrastructure owns the SQLite transaction and rejects
  cross-task batches before writing; App does not issue SQL. No schema change or
  migration is needed. A committed assistant cursor prevents duplicate text if
  later presentation fails, and equal message timestamps preserve insertion order.
  The composer clears a draft only after durable preparation and only if it still
  equals the captured input. It consumes only captured attachments; status updates
  of the same task cannot clear the next draft's attachments. Safe, localized
  preparation errors and send/pending/stop labels use shared controls and styles.
  Tool authorization, approval, timeout, cancellation and output budgets remain
  unchanged. Focused tests cover rollback, task isolation, cancellation, retry,
  concurrent typing, UI state and interrupted/cancelled conversation recovery;
  keep implementation types and methods within the existing hard limits.
- Embedded browser sessions carry the owning task ID through launch options.
  Each live session owns its pane, tabs and native views; selecting a task only
  selects presentation state and never launches a browser. Background sessions
  cannot replace the selected task's address, tabs or toolbar commands.
  A window-level shared `AppBrowserHost` keeps every native view attached and
  arranged, including before its inspector is opened. `AppBrowserViewport`
  supplies the selected inspector rectangle; other views remain laid out outside
  the window's content bounds, without hit testing or keyboard focus. Hiding
  the inspector or opening settings parks all views without recreating sessions.
  Pending approval also blocks native presentation, so child HWNDs cannot cover
  the approval surface. The window coordinator dispatches presentation updates
  onto the UI thread and unsubscribes on disposal without touching UI objects
  from the service-container disposal thread; tool approval policy is unchanged.
  The Windows backend's composited modes return synthetic frames, so they are
  not a substitute for live embedded hosting. Session release removes only its
  own native views and matching pane; stale release cannot clear a replacement.
  This remains App presentation behavior, uses the existing session factory
  contract and tool pipeline, and changes no authorization or persistence policy.
  Focused tests cover task routing, inactive/background layout, selection and
  release isolation. Native acceptance must cover two local synthetic tasks,
  hidden-inspector startup, tab switching and independent cancellation.
- Full-page settings navigation uses the shared `AppPageSwitcher`. It retains
  workspace and editor instances, hides the inactive page from keyboard and
  accessibility navigation, and owns initial focus, restoration, and stale
  focus callback rejection. UI page switching does not change tool policies,
  repository state, or task execution ownership. Focus selection uses one lazy
  visual-order traversal, preserving remembered targets and the first usable
  fallback while stopping at the first available field rather than materializing
  the full page. See `PAGE_FOCUS_OPTIMIZATION_ACCEPTANCE.md` for allocation,
  Headless resize, native settings/inspector evidence and unresolved layout limits.
- Automatic page/drawer focus targets must fit every ancestor scroll viewport.
  Offscreen remembered fields fall back to a visible eligible control rather than
  causing BringIntoView to hide the page heading. Visible remembered editors keep
  their scroll/draft, while explicit user focus can still scroll to later fields.
  This is shared App focus policy, not a native resize/DPI workaround; see
  `SETTINGS_VIEWPORT_FOCUS_ACCEPTANCE.md` for reproduction and validation limits.
- Browser toolbar and selection-aware horizontal scrolling belong to App shared
  controls; views supply address, commands, and selected item rather than child
  controls or instance sizes. Navigation callbacks capture their owning native
  view because NativeWebView forwards backend senders; replaced and detached
  views cannot update current address or history commands. Event delegates are
  unsubscribed on detach. These changes reuse existing session and command ports,
  add no persistence or authorization policy, and keep files and methods within
  the hard limits. Focused tests cover two-way binding, Enter, narrow layout,
  component ownership rejection, bounded scrolling and stale navigation events.
  Stateful component binding tests use an isolated Headless UI dispatcher;
  native browser acceptance still requires a running ordinary-permission window.
- Manual browser actions share the existing `IToolExecutor` pipeline with agent
  tools, including navigation, history, tabs and `browser.devtools`. App's
  `BrowserPaneActionsViewModel` owns busy/cancel state and a five-minute total
  lifetime including approval; the executor retains its own execution timeout.
  Contexts must match their owning task and route to its attached session, never
  a replacement lazy session. Revoking context or replacing a session cancels
  pending work and rejects stale results; non-cooperative execution remains busy
  until it returns. Tab selection follows actual session metadata, restoring
  the bound selector after denial. Missing contexts disable actions instead of
  falling back to direct native calls. Developer-tool support is an optional
  browser port capability; unsupported sessions return a safe localized error.
  Read-only tab metadata and native events remain presentation projections.
  Focused tests cover policy, audit, ownership, lifetime and bound selection;
  native acceptance uses synthetic local tasks without provider calls or accounts.
- Models emit normalized agent events instead of provider-specific UI objects.
- Normal window close must keep the UI dispatcher alive while task cancellation,
  final conversation persistence and browser release finish, then dispose startup
  discovery and the service container. App owns this lifetime coordination and
  reuses existing task, repository and cancellation contracts; no tool pipeline,
  authorization or database schema change is needed. Shared dialog controls own
  pending/error presentation. Task registration is closed before final draining,
  including task creation already in progress. Persist partial assistant text on
  cancellation. Focused tests must cover concurrent runs, creation/close races,
  duplicate close requests, safe failure presentation and persisted cancellation;
  keep each new type in its own file and methods below the existing hard limits.
- Model catalog presentation belongs to App and reads through the existing
  configuration repository port. Reloads publish a complete snapshot, discard
  stale completions, and retain the user's provider/model identity and explicit
  reasoning choice. A disabled or deleted selection does not silently fall back;
  loading or failed reads block new sends without clearing the draft. Running
  tasks retain their Stop action and already captured model configuration.
  Menu commands resolve stale identities to current records before publishing
  parameters, and disposal prevents late catalog publication. Shared action
  components own the composer selector, model rows and quick-menu layout.
  This introduces no persistence migration, tool capability or approval change.
  Focused tests cover overlapping reads, unavailable selections, safe errors,
  disposal, real settings-to-SQLite-to-loopback requests and light/dark layout.
- Application's immutable `ModelReasoningPolicy` owns protocol/model-specific
  effort capabilities and normalization. Settings, agent/subagent editors and
  the composer reuse it; provider execution rejects unsupported Gemini efforts
  before creating an HTTP client, and request building repeats the check.
  Existing invalid configuration remains visible and requires explicit correction;
  no migration or silent fallback rewrites user choices. App's shared
  `AppChoiceComboBox` and `AppDiscreteSlider` separate semantic selection from
  native list/range coercion so dynamic options cannot overwrite a draft.
  Slider input subscriptions follow visual attachment lifetime and shared tick
  layout owns endpoint alignment. Safe failure classification reuses existing
  persistence. See `MODEL_REASONING_CAPABILITY_ACCEPTANCE.md` for loopback/native
  evidence, supported-family boundaries and the non-clean full-suite result.
- The shared `AppEditMenu` owns titlebar editing commands, aligned shortcut rows,
  valid same-window text targets, selection restoration during menu focus, and
  subscription disposal. It uses Avalonia's editor and clipboard operations,
  rejects password copying and read-only mutations even on direct execution,
  and does not restore a selection onto externally replaced text. This is App
  presentation behavior, not an agent tool or persistence operation; capability,
  approval, timeout, cancellation and output policies are unchanged. Compile
  guards reject business replacements of its content, flyout, commands and sizes.
- Model discovery reads each HTTP response as a stream and limits each JSON page
  to 2 MiB before parsing; Gemini discovery also caps the number of pages.
  Its separate `GeminiModelDiscoveryParser` owns model and pagination metadata.
  Explicit generation methods must include `generateContent` or
  `streamGenerateContent`; non-generative models are filtered before deduplication.
  Missing optional capability metadata retains compatibility, while malformed
  explicit metadata rejects the read without echoing the response body. This does
  not rewrite manually configured models or change routing and cancellation.
- Provider SSE error codes and messages are redacted and bounded at the provider
  boundary before they become user-visible model failures.
- Gemini owns validated, bounded content snapshots, including signed thought,
  text, function-call and empty trailing parts in their original order. Only a
  selected candidate's valid STOP followed by validated stream completion releases
  calls and its continuation. Invalid signatures, identities, late content or limits
  discard the whole batch; Gemini 3 requires a signature on the first function part.
  Consecutive parallel results share one user content block, preserving native IDs.
  Application's immutable `ModelContinuation` is provider-neutral and scoped by
  provider, protocol and model identity. Main and child Agent loops retain it only
  in memory and account for it as non-trimmable context metadata. It never enters
  tool parameters, shared child text, UI, audit or SQLite, and generic diagnostics
  and serialization omit its opaque payload. Restoration uses existing tool
  observations, not native signed calls, and never replays work. No new database
  schema or approval exception is introduced; see `GEMINI_CONTINUATION_ACCEPTANCE.md`.
- Providers share a strict, incremental UTF-8 SSE reader: each physical line is
  limited to 1,048,576 UTF-16 code units before appending, including ignored fields
  and comments; each joined data event is limited to 2,097,152 code units including
  separator newlines. Fixed byte/character buffers preserve split UTF-8 sequences,
  CR/LF framing and a leading UTF-8 BOM without waiting for EOF to deliver an event.
  Limit/encoding failures emit one safe ModelFailed, not raw payloads; cancellation
  remains cancellation. The transport owner disposes the response stream.
  Non-success HTTP bodies are streamed with a 65,536-byte limit regardless of
  Content-Length; oversized or invalid UTF-8 bodies are not shown or logged.
  The shared provider SSE entry point checks HTTP status before handing any data
  to protocol parsers and owns the success-stream lifetime, including cancellation
  and early enumeration disposal. Adapters retain ownership of the HTTP response,
  client, request headers, payload and protocol terminal semantics. Internal SSE
  frames are immutable values instead of one heap object per event. The raw Stream
  overload remains non-owning for callers that manage their own stream lifetime.
  This reuses IModelProvider and changes no tool, approval, task or database policy.
  See `PROVIDER_STREAM_LIMIT_ACCEPTANCE.md` for protocol, workflow and native evidence.
  Real pinned-TLS loopback regressions also cover fixed-length prefix delivery,
  completion, cancellation, early disposal and certificate rejection across the
  four adapters, without changing production certificate validation. The separate
  local plaintext HTTP withholding observation remains open; independent Python
  transport evidence is in `FIXED_LENGTH_STREAM_ACCEPTANCE.md`.
- Compatible chat streams isolate choice 0 and reject incomplete or malformed
  tool batches before execution. Empty/null tail tool metadata after finish is
  harmless; nonempty late calls and text remain invalid. Settings-to-loopback
  HTTP-to-tool/audit-to-SQLite/reopen tests exercise the production chain without
  paid requests. See `COMPATIBLE_STREAM_ACCEPTANCE.md` for runtime evidence;
  the shared framing boundary also rejects oversized lines and multiline events
  before the accumulated tool batch can execute.
- Per-model advanced JSON can add protocol-specific parameters, but cannot
  replace system messages, tools, model identity, or the output and reasoning
  controls managed by the model editor. The same rule is applied when building
  requests from previously saved profiles; compatible chat models can select
  `max_completion_tokens` only within their configured output limit.
- The agent injects task-bound safety guidance and constructs a bounded context
  suffix using each model's configured context and output limits.
- Tools execute through one guarded pipeline and return bounded `ToolResult`
  values plus optional artifacts. Summary, content, and dialogue roles/text share
  the task's output-character budget before audit and model-context insertion.
  Declared string-length constraints are checked before approval using Unicode
  scalar counts; malformed or inverted length bounds fail closed. File content
  and replacement text may be empty or whitespace without relaxing path checks.
  Cancelling a pending approval is classified as `tool_cancelled`, not an
  approval-service failure. Approval exceptions without caller cancellation
  remain `approval_failed` and never expose raw exception text. The executor
  checks cancellation again immediately before invoking any tool, including
  a non-cooperative tool after an approval/cancellation race.
- MCP reload limits initialization and tool discovery to 15 seconds per server,
  disposes failed clients, and preserves caller cancellation. Connection errors
  and unexpected tool exceptions use safe summaries rather than raw exception text.
  Reload ownership includes waiting for the reload gate and configuration reads.
  Shutdown rejects new reloads, cancels running/queued operations and awaits their
  return before disposing clients, removing only MCP tools and releasing the gate.
  Noncooperative configuration reads retain ownership and cannot start late servers.
  Repeated disposal shares one completion task. The optional time-provider seam
  keeps production on system time with the same 15-second default; tests advance
  deadlines after binding real server descendants instead of racing process startup.
  See `MCP_EXECUTION_ACCEPTANCE.md` for shutdown evidence and remaining limits.
- MCP registration identities include the persisted server profile GUID and a
  digest of the exact remote tool name. Normalized labels are descriptive, not
  sufficient identities. Discovery validates a complete server batch before
  publishing adapters; duplicate identities fail closed and dispose the client.
  Remote calls preserve original case and punctuation. Legacy ambiguous names
  are not aliased or rebound, and historical records are not rewritten. See
  `MCP_TOOL_IDENTITY_ACCEPTANCE.md` for compatibility and end-to-end evidence.
- MCP settings expose persistent localized names for transport-specific inputs
  and typed error severity through the shared header/footer appearance variants.
  Explicit reconnect refreshes registration without replaying failed tool calls.
  Production Headless UI recovery, real HTTP/approval/audit, cancellation, denied
  calls and operable restored history are recorded in `MCP_UI_RECOVERY_ACCEPTANCE.md`;
  this does not establish automatic transport resumption or native pixel parity.
- stdio MCP connections use the portable process launcher with owned standard
  input and per-server environment snapshots. Each connection owns a Windows
  kill-on-close Job independently of shell commands. Failed or cancelled startup,
  parent exit, reload, and disposal reclaim its descendants. Protocol frames are
  serialized and limited to 2,097,152 characters; stderr is drained without being
  copied into tool results. Cancelled sent requests send an advisory MCP
  cancellation notification and remove their pending response entry.
- MCP transport and process cleanup continuations do not depend on a UI
  synchronization context. Initial MCP discovery starts on a background task
  after the main window opens. The shared startup state prevents premature agent
  runs without clearing the composer or blocking settings navigation. Completion
  is posted to the UI dispatcher; safe warnings link to MCP settings. Application
  exit cancels and awaits startup before disposing the service container, without
  waiting for queued UI callbacks. Database initialization and composition still
  happen before window creation; this is not a fully asynchronous bootstrap.
- Streamable HTTP MCP bounds each request and response to 2 MiB of bytes.
  JSON and SSE responses use strict UTF-8 and validated JSON-RPC envelopes;
  incremental SSE completes on the matching result without waiting for EOF.
  Progress and heartbeat bytes share the response budget. Cancellation attempts
  a bounded advisory notification; failed initialization or connection disposal
  cancels in-flight requests and best-effort terminates an assigned HTTP session.
  Automatic redirects are disabled to avoid forwarding configured headers to
  another endpoint. No selected MCP proxy means no implicit system proxy.
  Remote protocol and tool-error bodies never become persisted failure summaries.
- MCP settings expose stdio arguments and environment variables or HTTP URL,
  headers, and proxy according to the selected transport. Saving drops inactive
  transport fields; malformed JSON and credential-bearing URLs are rejected
  without echoing configuration secrets into status messages.
- `AuthorizedRoots` alone grant filesystem access. `WorkingDirectory` only
  resolves relative paths and never becomes an implicit authorized root.
- Path-aware tools validate every requested path before approval and resolve it
  again immediately before execution, including the interactive terminal's
  working directory. Rejected paths are audited without prompting the user.
- Text reads return a full-file SHA-256 and retain bounded, task-owned version
  metadata for the current run; context copies used by child tools share only
  their parent task's version store. Cross-task store reuse is rejected.
  Writes and exact replacements stage a separate file in the target directory,
  revalidate authorization and the original version before atomic replacement,
  and update the observed version only after committing. Existing targets use
  replacement; new targets use non-overwriting rename. An application-owned,
  bounded lock set serializes overlapping writes through the shared writer.
  Exact replacement retains the detected text encoding and rejects files over
  16 MiB. Model instructions require explicit `expectedSha256`, which also
  protects a version observed before the current run. Known conflicts surface
  as `file_changed` and require a fresh read rather than automatic retries.
  This is optimistic concurrency, not a filesystem transaction against hostile
  processes that alter path bindings between the final check and rename.
- Subagents inherit the parent task's authorization, approval, timeout, and
  cancellation context. Child tool calls re-enter the same executor and audit
  pipeline; recursive delegation is not available to child models. Saved child
  model identity/provider and reasoning effort are checked against current
  protocol/model capabilities before starting a child run. A later configuration
  edit cannot silently substitute or invoke an unsupported child; rejection is
  actionable, localized and audited. See `SUBAGENT_RUNTIME_CONFIGURATION_ACCEPTANCE.md`.
  Child membership checks do not rebuild descriptor projections; parent model
  turns bypass no-op delegation filtering while retaining independent snapshots
  and current dynamic registrations. See `AGENT_TOOL_CATALOG_OPTIMIZATION_ACCEPTANCE.md`.
  Progress
  appears in the active conversation, while the final bounded dialogue is
  persisted with the parent tool result. Child text is previewed before its
  model turn completes; previews are coalesced into one reply row, limited to
  1024 characters and throttled at 64-character increments. The progress channel
  is bounded to 256 entries; final dialogue replaces previews and is persisted
  only after the normal tool output limiter runs.
- Cancelling a subagent stops its model/tool execution but drains the bounded
  progress channel until the executor returns its audited, limited terminal
  result. That result is persisted before cancellation propagates; no next
  model turn or queued tool starts. Tool history preserves cancellation and
  timeout error codes, while terminal run events clear any orphaned live cards
  without claiming that an unfinished tool succeeded.
  Interrupted child dialogue is carried by `ToolRunInterruptedException`;
  the normal executor still classifies cancellation versus timeout, applies
  the shared output budget, and completes audit persistence before delivery.
- The App projects persisted subagent dialogue into task, status, tool, and
  reply rows for display. This presentation does not change the stored dialogue
  or bypass the parent tool's output limits.
  Tool detail viewports share a bounded-height, vertical-only scroll variant so
  wrapped prose and long paths receive finite width. Code-block viewports retain
  horizontal scrolling. Detail surface spacing belongs to the shared style;
  content, persistence, tool policy and output limits are unchanged. Headless
  layout tests load the real App styles and fonts without application startup,
  and cover long unbroken text at narrow/wide widths in both themes.
- Subagent model choices are App-owned presentation snapshots loaded through
  the existing configuration repository and model-catalog change callback.
  Reloading retains editor drafts and the preferred provider/model identity;
  unavailable choices are removed without selecting a different model. A load
  generation rejects late snapshots, and save reloads the catalog before
  validation. Query failures preserve the draft and report a safe message rather
  than saving a stale selection. The window unsubscribes its catalog callback
  on disposal. This changes no authorization, approval, timeout, cancellation,
  output-limit or persistence contract, and adds no database migration.
  Focused tests cover settings-to-SQLite catalog changes, failed and out-of-order
  loads, unavailable selection recovery, stale-save refusal and disposal;
  affected files and methods remain within the engineering hard limits.
- Subagent editor writes capture immutable drafts and stable identities before
  awaiting storage. Shared mutation and guarded-command ownership reject overlap
  without cancelling accepted work; late results cannot replace newer drafts or
  another editor context. Lifetime and command cancellation reach repository ports,
  with a 30-second cancellation request and bounded reads. Noncooperative writes
  retain ownership until they return, rather than claiming rollback or forced exit.
  Refresh failures preserve drafts but block mutations; removed targets cannot
  be resurrected. Canonical snapshot selections explicitly notify bound controls,
  while unchanged collections retain item instances and scroll placement. Shared
  controls own appearance; structural footer rows give action and feedback finite
  width. No migration or tool-policy change; see `SUBAGENT_EXECUTION_ACCEPTANCE.md`
  for failed baselines, bound-view tests, native input and verification limits.
- Custom-agent settings use the same App editor revision, guarded commands and
  mutation runner through existing configuration ports. Immutable clicked drafts
  retain stable identities; late save/delete results cannot overwrite new edits
  or another context. Model-catalog notifications reload model options without
  resetting agent fields. Failed reads retain prior rows but block writes, and
  only available, completed snapshots can prove an editing target was removed.
  Workspace-notification failure is distinguished from a committed write and
  can be retried by refresh without another save. Lifetime cancellation rejects
  late editor publication; noncooperative writes retain mutation ownership until
  returning. Shared controls own the finite-width action and feedback presentation.
  No migration or tool-policy change; see `AGENT_CONFIGURATION_ACCEPTANCE.md` for
  actual settings-to-HTTP requests, persisted replies, native input and limits.
- Browser implementations expose one task-bound `IBrowserSession` contract;
  tab identifiers are stable only for the lifetime of that session.
  CDP attachment creates a task-owned page; borrowed contexts expose only that
  task's attached/created pages and their descendant popups. Disposal waits for
  in-flight creation and closes owned pages/runtime state once, never the attached
  browser, borrowed context or unrelated pre-existing pages. A shared CDP context
  still shares cookies/login/storage; page isolation does not imply storage
  isolation. See `CDP_TASK_ISOLATION_ACCEPTANCE.md` for deterministic and real
  settings-to-standard-tool-pipeline evidence and remaining native limits.
  `CDP_AGENT_WORKFLOW_ACCEPTANCE.md` extends the same production ports through
  Agent/Responses conversation, targeted cancellation and restored operable UI
  history using a local scripted provider and test-owned headless Chrome.
- CDP endpoint policy lives in Domain and is enforced again by the browser
  factory before Playwright starts. HTTP discovery bypasses system proxies,
  rejects redirects and oversized responses, and validates the discovered
  WebSocket host and port before any browser connection.
- Task browser sessions are created lazily and released when a run completes,
  fails, or is cancelled. Released handles cannot start a new browser; a resumed
  run receives a fresh session. Existing CDP connections are limited to explicit
  loopback endpoints without URI credentials.
- Each embedded session owns an operation queue covering navigation, DOM work,
  screenshots, downloads, tab reads and tab mutations. Disposal rejects pending
  and new work, waits for the active operation, and performs native cleanup once.
  Pane commands use that session contract rather than independently navigating
  its native view. Tab creation, switching and closing commit on the UI thread;
  a cancelled unpublished view is disposed without changing the active tab.
  Tab-list refreshes carry a session and generation check, so late success or
  failure cannot repopulate a closed session or overwrite a newer refresh.
  Each task has its own embedded pane; the shared native host retains the views
  and parks inactive presentation without reparenting. App's separate inspector
  view model retains tab, docking and floating-card state by task identity, with
  project-specific draft state. Record replacements and background scheduled
  runs cannot overwrite that presentation. Archive drops the task's retained
  state; disposal unsubscribes and rejects later mutations. This UI state is
  in-memory, not a persistence or approval-policy change. See
  `INSPECTOR_TASK_STATE_ACCEPTANCE.md` for the regression and native boundaries.
  Two-session native DOM/screenshot/independent-release acceptance remains
  required before claiming a completed concurrent-browser capability.
- Browser downloads write through a caller-owned stream so the browser layer
  never selects arbitrary filesystem destinations.
- Large binary outputs stay in the task artifact directory. SQLite stores only
  names, paths, media types, lengths, and SHA-256 hashes.
- Persistence is accessed through application ports, not raw connections from
  UI or agent code.
- Proxy settings notify the provider editor after save, deletion or refresh.
  Provider proxy-only reloads preserve drafts and desired routing identity;
  unavailable choices are not replaced with direct access or another proxy.
  Load generations reject older snapshots, and save revalidates the selected
  enabled proxy through the configuration port. The window unsubscribes on disposal.
  The configuration repository atomically refuses deletion of a proxy referenced
  by a provider, browser configuration or MCP server. It reports `ProxyInUseException`
  rather than letting foreign-key cleanup silently change the route to direct.
  This preserves existing data and changes no schema, tool approval, authorization,
  timeout, cancellation or output contract. App owns editor synchronization;
  Application owns the port failure and Infrastructure owns transactional checks.
  Focused tests cover drafts, missing and disabled choices, reload ordering,
  safe errors, reference protection and real loopback discovery/streaming requests.
- Pinned conversations live in an additive SQLite table and are loaded across
  projects into a separate sidebar section. Archived conversations are hidden
  from that section without deleting their pin, so restoring preserves it.
- Workspace page navigation follows explicit, validated catalog navigation
  requests, including selecting the current conversation and opening an empty
  draft. Catalog refreshes and background task metadata updates do not switch
  pages. `WorkspaceNavigationController` owns history command availability;
  `WorkspacePageNavigationViewModel` disposes its navigation subscriptions.
  Shared `AppEmptyState` variants keep fixed-height inspector notices readable.
  See `WORKSPACE_NAVIGATION_ACCEPTANCE.md` for native reproduction and regressions.
- Appearance preferences use an application store port and an infrastructure
  JSON adapter; theme and code-font saves preserve unrelated settings and
  replace the file atomically. The appearance page reports save failures.
- Light and dark theme palettes persist separately in that JSON store. Hex
  colors must maintain a readable foreground/background contrast; the App
  updates theme brushes on the UI dispatcher and can restore default colors.
  Appearance exposes independent light and dark palette editors so switching
  the active theme does not discard either editor's unsaved color draft.
- UI, content, and code fonts persist separately for light and dark themes.
  Existing global UI and code font choices remain as fallbacks until each theme
  is customized; content defaults to the active UI font. Theme changes update
  shared font resources, while conversation prose and code spans use separate
  typography variants.
- Windows-first Default UI and uncustomized content use Segoe UI, matching the
  installed Codex system-font stack. Explicit bundled Inter remains selectable;
  existing enum identities and saved defaults are not migrated or rewritten.
  Layout tests use Skia fonts with headless windowing, not substituted BareMinimum
  system-font metrics. See `DEFAULT_UI_FONT_FIDELITY_ACCEPTANCE.md` for failing
  baselines, native screenshots, persistence audits and remaining reference limits.
- Platform-specific facilities implement portable ports and stay outside the
  domain and application projects.
- `shell.run` launches through the platform-neutral process contracts in
  `Tools.Abstractions.Processes`. The Windows implementation creates each
  process atomically inside its own kill-on-close Job Object, with an explicit
  inherited-handle allowlist, before resuming its first thread. Parent exit,
  cancellation, timeout, and disposal terminate and await the remaining job
  processes before output readers and command ownership are released. Each
  command has its own job; descendants do not outlive a completed shell call.
  Launch failure never falls back to an unowned process. Git commands and
  stdio MCP connections also use this launcher with their own operation or
  connection jobs; PTY and browser lifetimes remain separate implementations.
- Parent standard-I/O endpoints use asynchronous, current-user-only, single-instance
  named pipes; child endpoints remain synchronous and are the only inherited ends.
  This avoids worker-pool starvation while shell/Git/MCP wait for idle output.
  Stream transfer preserves endpoint ownership, while the launcher keeps its
  explicit inherited-handle allowlist and per-operation kill-on-close Job.
  No tool policy, timeout, output, audit or process port changes are required.
  Deterministic old/new starvation probes, cancellation/inheritance regressions
  and production-composition/native-restore evidence are recorded in
  `ASYNC_PROCESS_PIPE_ACCEPTANCE.md`.
- Git tool execution requires the existing owned process launcher and retains
  the standard schema, path, approval, timeout, output, and audit pipeline.
  Infrastructure branch queries and managed worktree operations share
  `GitProcessRunner`, which drains both streams, retains at most 16,384 characters
  per stream, and enforces a 30-second deadline. Normal exit, cancellation, and
  timeout reclaim hook descendants and observe both readers before returning.
  Truncated branch output is not reported as a valid branch. Cleanup does not
  roll back worktree or branch mutations and never replays cancelled operations.
  See `GIT_PROCESS_OWNERSHIP_ACCEPTANCE.md` for real Git/hook evidence and limits.
- Each active task owns an isolated run state and cancellation source. Agent
  event presentation crosses the Avalonia dispatcher before touching UI-bound
  observable collections, while tool approvals are serialized through a
  cancellation-aware queue.
  Event presentation dispatches to Avalonia only when an application host exists
  and the caller is off the UI thread; headless workflow acceptance uses the same
  presenter without waiting on an unstarted UI message loop.

## Visual component boundary

- Field metrics belong to App shared `Styles/FieldVariants.axaml`: text, numeric
  and selection fields share a 32-DIP minimum and centered content; multiline
  configuration and prompt editors select semantic variants instead of page-local
  heights or alignment. Composer growth remains bounded by its shared variant.
  `ValidateSharedFieldSizing` and `MOSSUI002` reject business overrides through
  markup, property elements, C# assignment and styled-property mutation. Widths
  remain structural; values, bindings, read-only state and commands remain
  functional. This reuses existing components and changes no module dependencies,
  authorization, approval, timeout, output limits or persistence contracts.
  Focused tests cover both themes, narrow/wide layout, multiline growth, compact
  browser exceptions, allowed functional data and rejected appearance overrides;
  handwritten types and methods remain within the engineering hard limits.

- Conversation-header inspector actions reuse the existing panel command and
  `AppIconButton` state-dependent geometry. Labels, tooltips and accessibility
  names follow the open state through localized resources. Shared header
  variants own compact geometry, context selection and the separator; views
  do not override appearance. Closing the panel preserves the user's floating
  context-card preference. This changes no task or persistence behavior.
  See [CHAT_HEADER_ACCEPTANCE.md](CHAT_HEADER_ACCEPTANCE.md) for actual layout,
  regression and native evidence, including incomplete final native interactions.

- The main shell keeps a 52-DIP navigation rail beside a 264-DIP project
  sidebar; each surface has its own theme brush and shared control variants.
  The active home icon uses the shared filled foreground variant, while the
  sidebar header, pinned section, and project heading follow the same vertical
  rhythm as the desktop reference.
  The shared titlebar is 44 DIP in both workspace and settings, with a 36-DIP
  control band and an 8-DIP header-colored bottom inset, without a divider.
  Settings replace the workspace rail with a full-width 276-DIP sidebar. Its
  back action returns to the workspace independently of the workspace panel
  toggle. The titlebar omits the panel button in settings.
  The settings content begins with a rounded top-left corner;
  standard pages share a maximum 768-DIP content column, with 20- and 34-DIP
  minimum side insets so narrow windows retain the reference card width.
  `SettingsLayoutVariants.axaml` applies this geometry to both standard and
  provider pages. The appearance page shows three full-size theme choices,
  a default-visible diff sample, and independent light/dark palette cards with
  UI, content, and code font editors.
  Accent choices apply immediately to the active
  light or dark palette and persist separately; invalid or unreadable colors are
  rejected without replacing saved settings.

- Workspace task titles use the existing repository port's dedicated rename
  transaction, changing only title and timestamp while returning authoritative
  status, approval, worktree, pin and failure metadata. Task/project identity and
  expected title must match; active, archived and missing targets are rejected
  without replacing the editor draft or recreating a task. App metadata mutations
  retain ownership through persistence and publication; rename/archive block
  starting only their target task, not other tasks or stopping an active run.
  Owner cancellation and the existing cooperative deadline remain in force.
  No schema or tool-policy change; see `TASK_TITLE_PERSISTENCE_ACCEPTANCE.md`
  for reproduced failures, transaction rollback and verification limits.
- App views use the shared controls in `MossAgent.App.Controls` for buttons,
  icons, text, inputs, cards, selectors, checkboxes, toggles, lists, numeric
  inputs, sliders, selectable and rich text, scroll viewers, and flyouts. Reusable
  visual controls belong in this namespace; `Views` contains only `AppWindow`
  and `AppUserControl` hosts. Direct Avalonia visual
  controls in App AXAML are disallowed by an allowlist in
  `NativeVisualControlUsageTests` and the App project's build-time
  `ValidateSharedVisualControls` target, including new markup directories.
  `ValidateSharedVisualNamespaces` also rejects controls from other AXAML
  namespaces, so a third-party visual cannot bypass the shared wrapper boundary.
  A new visual primitive needs a wrapper and a shared style before it can be
  used in a view. See [UI_COMPONENTS.md](UI_COMPONENTS.md) for the component
  catalogue, examples, extension workflow, and enforcement rules.
  `MOSSUI001` applies the same rule to C# construction and inheritance during
  compilation, including third-party visual controls; only wrapper code in
  `MossAgent.App.Controls` and structural layout primitives are exempt.
  Composed `AppUserControl` wrappers retain Avalonia's `UserControl` style key;
  shared descendant styles target a stable class on the wrapper root instead
  of its concrete type selector.
  The embedded browser's third-party native view is created through
  `AppEmbeddedBrowserView`; its surrounding surface still uses shared App styles.
  `EmbeddedBrowserPane` hosts that view directly as an `AppBorder.Child`, rather
  than through a templated content control, so dynamically attached native views
  receive valid measure and arrange passes. The Windows application embeds
  `app.manifest` with the Windows 10/11 supported-OS declaration required by
  Avalonia native child-window attachment, per-monitor DPI awareness, and
  `asInvoker` privileges. Native hosting never requires application elevation.
  See [BROWSER_EXECUTION_ACCEPTANCE.md](BROWSER_EXECUTION_ACCEPTANCE.md) for the
  isolated native-runtime verification and its remaining limits.
- Views and reusable control markup cannot define local `Style` or
  `ControlTheme` declarations. `ValidateNoViewLocalStyles` enforces this at
  build time; visual and interaction variants belong in `Styles/`.
  `MOSSUI002` also rejects direct C# mutations of a control's local `Styles`
  collection, including index replacement and collection-interface casts;
  application-level shared style composition and wrapper internals remain valid.
- Shared metadata and tool activity status use theme-aware foreground roles and
  bounded header geometry. Approval's task-control adapter exposes the existing
  stop command only for a live request belonging to the selected running task;
  queue identity and disposal guards prevent stale gestures from becoming a
  different task action. It changes no execution or approval policy. See
  `UI_METADATA_POLISH_ACCEPTANCE.md` for contrast, compact stop and Headless
  production-workflow evidence and its native/pixel-equivalence limits.
- Local component resources cannot shadow shared appearance tokens.
  `ValidateSharedComponentResources` rejects visual-resource declarations,
  resource imports and resource-dictionary replacement in business AXAML;
  converters and data templates using shared controls remain allowed.
  `MOSSUI002` rejects business C# writes to control resources, including aliases,
  merged dictionaries and theme dictionaries. Read-only queries, component
  internals and application-level appearance services remain allowed.
  This is an App compile-time boundary, introduces no runtime port or policy,
  and changes no authorization, cancellation, persistence or output handling.
  Focused negative tests cover both markup and C# bypasses without changing
  rendered styles; component consistency is not pixel-fidelity acceptance.
- Shared appearance cannot be overridden through native base-class property
  elements such as `Button.Background` on an `AppButton`.
  `ValidateInheritedAppearancePropertyElements` rejects this bypass at build
  time while retaining behavior and layout properties such as `Button.Flyout`.
- Icon-and-label actions use `AppActionButton` so their icon size, gap, baseline,
  text color, and hover color follow the same button style. Optional status
  badges stay inside that control, including disabled sidebar actions. Views
  do not use font glyphs as action icons.
- Sidebar pinned, project, and recent conversations use `AppTaskNavigationRow`.
  The component owns selection, title truncation, the shared option button, and
  its action menu. Every action receives the row's task parameter; pin labels
  reflect that task, not the selected conversation. Task metadata operations
  reuse `IWorkspaceRepository`, keep unrelated selection intact, and check the
  target's run state. Views cannot replace the row's content or dimensions.
  The menu closes when its context, target or action command changes, its row
  becomes unavailable or detaches, or an ancestor hides. Ancestor subscriptions
  follow visual-tree lifetime; title/selection updates preserve the current menu.
  See `TASK_MENU_LIFETIME_ACCEPTANCE.md` for focused and native evidence.
- App's `WorkspaceSidebarProjectsViewModel` and `WorkspaceSidebarProjectList` own
  project ordering without mutating the catalog's source collection. Activity order
  uses complete task snapshots, including pinned and search-hidden tasks. Manual
  order uses stable project identities and preserves selected/expanded row instances.
  Sorting preferences load independently from catalog/model startup; failures disable
  sorting writes, not task creation. Commands keep ownership until storage returns,
  use lifetime cancellation and a ten-second cooperative deadline, and reject late
  completion after disposal. The Application `ISidebarPreferenceStore` port is implemented
  by Infrastructure's atomic, ordinary runtime JSON `sidebar-settings.json` store.
  Corrupt preferences are never overwritten; appearance settings and SQLite schema
  are unchanged. The project-section menu contains sorting, not conversation mutations.
  Project summaries offer real up/down moves; shared project rows also support native
  before/after dragging and navigation-viewport edge scrolling. The same preference
  port saves manual order and restores it on restart. See
  `SIDEBAR_PROJECT_DRAG_ACCEPTANCE.md` for verified behavior and remaining native limits.
- App's workspace catalog, project snapshots and conversation tabs share typed,
  indexed identity lookups. Project editing finds the current collection index
  without copying the project list. Tabs reuse reconciliation's selected-row
  lookup rather than scanning again; unchanged rows retain their identity and
  collection notifications. Lookups use complete current snapshots, including
  pinned and search-hidden tasks, and preserve first-occurrence/owning-project
  rules without a cache. No I/O or execution policy changes. See
  `WORKSPACE_LOOKUP_OPTIMIZATION_ACCEPTANCE.md` for regression and allocation evidence.
- Icon-only actions use `AppIconButton`, which owns the icon geometry, shared
  hit area, foreground binding, tooltip, and accessible label. Size differences
  belong in shared component variants rather than per-view icon markup. Its
  alternate geometry follows a bound state for expand/collapse actions, so
  views do not stack two icons inside a generic button. Window chrome actions
  use the same component and size variant; maximize/restore switches geometry
  without replacing the button. Component tests reject generic icon-only buttons.
  Caption variants retain 46x36 Arrow-cursor hit areas while the close icon uses
  Size=10, ordinary maximize Size=9, and minimize/restore Size=8. Ordinary maximize
  uses flat line caps and a shared half-pixel translation for solid 96-DPI edges;
  its alternate pseudo-class restores the existing restore-glyph presentation.
  Their shared control theme disables
  keyboard focus so pointer caption actions retain the active editor's focus
  and dialog Escape route instead of introducing titlebar Tab stops. Commands
  and accessible labels remain unchanged. Focus preservation, disabled commands
  and native model-editor resize/Escape evidence are recorded in
  `WINDOW_CAPTION_FOCUS_ACCEPTANCE.md`. The shared close-hover rule uses
  `#E81123` with a white glyph. Deterministic checks and version-specific native
  evidence, including unresolved capture mismatches, are separated in
  `WINDOW_CAPTION_FIDELITY_ACCEPTANCE.md`.
  The titlebar's rounded-square panel glyph uses Size=10 with stroke 1, preserving
  the existing 32x30 hit area. Its Skia regressions belong to the separate pixel
  assembly rather than mixing renderer/font implementations in the App test host.
  Native measured bounds and replay are in `WINDOW_SIDEBAR_ICON_ACCEPTANCE.md`.
- Action menus opt into `AppFlyout.DismissOnButtonClick` so selecting an action
  closes the popup after its command runs. Search, editor, and model-picker
  flyouts remain open for multi-step interaction. The empty-chat project link
  and composer context share a searchable project picker backed by the catalog's
  selection command. Its create action opens the existing project editor after
  the picker closes, avoiding competing flyouts.
  Shared navigation variants pair hidden opacity with disabled pointer hit testing,
  reveal actions on row hover or keyboard focus, and retain the anchor while its
  flyout is open. Revealing an action never enables a disabled command. These are
  App presentation rules; task authorization and persistence are unchanged.
- The conversation header renders open tasks through `WorkspaceConversationTabsView`.
  `WorkspaceConversationTabsViewModel` follows catalog selection, keeps tabs across
  projects, updates titles from catalog task changes, and removes archived tasks.
  Closing a tab only closes its UI entry; it does not archive or delete the task.
  Selecting a tab routes through the catalog's existing task-selection command.
  One selected tab uses a borderless, bounded `AppIconLabel` heading instead of a
  tab chip; multiple tabs retain the selection-aware tab strip while a task is
  selected. Drafts hide both heading and strip without closing existing tabs;
  task selection and history navigation restore the same retained tab instances.
  See `DRAFT_CONVERSATION_HEADER_ACCEPTANCE.md` for native and regression evidence.
  The common task menu can close the selected tab, including the single-tab case.
  Header geometry and typography remain shared variants. `AppWindow` configures
  grayscale text rasterization once, rather than per-view rendering overrides.
- Rich-text view controls inherit `AppSelectableText` rather than native
  `SelectableTextBlock`; a component test enforces this boundary for new views.
- Settings navigation uses `AppSettingsNavButton`; its icon-label layout and
  toggle theme are shared rather than repeated in the settings view.
  `AppActionButton` and `AppSettingsNavButton` both compose `AppIconLabel`,
  keeping their icon size, text alignment, and spacing in one component.
  Non-interactive icon/text metadata can use `AppIconLabel` directly; its
  truncation and alignment stay shared while purpose classes in `Styles/`
  choose the icon size, text size, and color. `ValidateSharedComposedControls`
  prevents views from replacing those controls' content or overriding the
  icon-label layout; functional flyouts remain allowed.
- Layout and framework plumbing such as `Grid`, `StackPanel`,
  `ContentPresenter` and templates may remain native; page and window hosts use
  `AppUserControl` and `AppWindow` wrappers.
  they are not interchangeable visual primitives. Layout primitives cannot own
  backgrounds, borders, corners, shadows, or foreground colors in views;
  `ValidateLayoutPrimitivesRemainStructural` checks this during the App build.
  Use `AppBorder` with a shared surface class for those appearances. Flyout surfaces use
  `AppFlyout` and `AppCard`, including the same border and four-sided shadow.
  List rendering and content hosting use `AppItemsControl` and
  `AppContentControl`; their inherited Avalonia themes remain intact.
- Shared colors, dimensions, focus and hover states, and card shadows belong
  in `App.axaml`; reusable control themes live in `Styles/ComponentVariants.axaml`
  and card, popup, and field variants live in `Styles/SurfaceVariants.axaml`.
  Card and dropdown popup shadows reference the single `SharedSurfaceShadow`
  resource. Shared shadow setters must use that resource or `None`; the App
  build rejects independently defined shadow values and property elements.
  Card content insets use `card-inset-*` variants in
  `Styles/CardInsetVariants.axaml` rather than page-local `Padding`, so card
  spacing stays consistent across settings and flyouts.
  Inspector overview metadata uses shared grouping variants in
  `Styles/InspectorVariants.axaml`; title/status, source heading and source rows
  retain separate layout rhythms under the shared inspector card inset.
  Existing commands and draft state stay view-model-owned. Native light/dark,
  narrow/wide evidence and ROI measurement limits are documented in
  `INSPECTOR_OVERVIEW_FIDELITY_ACCEPTANCE.md`.
  Shared text hierarchy, settings headings, and sidebar typography live in
  `Styles/TypographyVariants.axaml`.
  View text uses shared `type-*`, `weight-*`, and `line-*` classes rather than
  inline font size, weight, family, or line height; the usage test checks that
  every class has a matching shared style. This includes selectable Markdown
  and syntax-highlighted code text.
  Appearance theme thumbnails use one 80-by-60 `AppThemePreview` and reusable
  `AppThemePreviewScene` artwork, not three independently styled mockups.
  `Styles/ThemeThumbnailVariants.axaml` owns miniature conversation geometry and
  ink; `Styles/ThemeModePickerVariants.axaml` owns interactive choice appearance.
  System mode clips two real scenes into 40-by-60 light and dark halves.
  Each scene separates the noninteractive inner-screen shadow from the clipped
  rounded screen content. Both share bounds/corners; only the shadow sibling owns
  SharedSurfaceShadow, preserving the existing artwork and choice hit area.
  `THEME_THUMBNAIL_SHADOW_ACCEPTANCE.md` records the clipping regression, native
  frames, historical-only metrics and postponed restart/current-reference gates.
  `THEME_THUMBNAIL_ARTWORK_ACCEPTANCE.md` records native 96-DPI crop comparisons
  and 100/125/200-percent headless compositor checks, not whole-page parity.
  The static appearance diff sample uses `AppDiffPreview` and the same code-font
  resource and shared theme-preview styles; it is not presented as an action.
  Views and reusable controls do not define local styles, including layout-only
  styles. New variants belong in the shared `Styles/` files rather than a view.
  Icon-label compositions use bounded Grid columns and leading-aligned,
  ellipsized text, avoiding infinite horizontal measurement and the centering
  of capped labels. `AppActionButton` reserves separate columns for a badge
  and optional trailing geometry. Sidebar and project-picker actions and composer
  selectors reuse it, preserving their commands, flyouts, selection, and accessible
  names. `Styles/ActionButtonVariants.axaml` owns their semantic sizing and spacing.
  Icon-label truncation limits belong in shared variants as well;
  `ValidateSharedLabelSizing` and `MOSSUI002` reject per-instance `LabelMaxWidth` overrides.
  View markup must not set literal colors or override shared text foregrounds
  and icon strokes; add semantic theme brushes and component classes instead.
  Status warning/error/success and approval badge colors follow this rule in
  both light and dark themes.
  Card and structural border colors, radii, and shadows, plus field colors, borders,
  typography, and padding, must not be overridden on individual view instances.
  Structural `AppBorder` surfaces also use named shared variants; the App build
  rejects inline backgrounds, borders, corners, and shadows in view markup.
  Content padding and sizing used solely for layout remain view-owned.
  The selected project's composer context is a full-width strip above the
  input card using the shared `composer-context-surface` variant. The project
  and agent selectors remain interactive; branch status is separate, passive
  metadata rather than part of the project selector's click target. At window
  heights below 720 DIP, `ComposerResponsiveVariants.axaml` keeps a 20-DIP bottom
  inset and shortens the context strip and input card so the composer remains visible.
  Composer, multiline prose and system-prompt inputs share wrapped, vertical-only
  editing in FieldVariants; single-line and JSON/code inputs retain their separate
  horizontal editing behavior. Composer height remains bounded at 180 DIP.
  The shared checkbox template owns a transparent full-bounds hit surface while
  preserving its centered glyph, native keyboard/three-state behavior and disabled
  state. See `INPUT_HIT_TARGET_ACCEPTANCE.md` for reproduced failures and native
  light/dark, narrow/wide and unsent-draft verification.
  Its theme-default keyboard adorner uses a non-interactive rounded AppBorder and
  semantic focus brush. Focus does not change the check-surface border thickness,
  avoiding glyph shifts from layout rounding at fractional DPI. Local focus
  ownership remains respected; see `CHECKBOX_FOCUS_APPEARANCE_ACCEPTANCE.md`.
  Full settings pages use the shared `settings-page-scroll` variant so their
  inner scrollbar is hidden without disabling wheel or keyboard scrolling;
  modal and conversation scroll surfaces retain their own behavior.
  `AppIcon` size is
  selected through shared `icon-micro`, `icon-small`, `icon-large`, `icon-hero`,
  or `icon-brand` classes; the default size is 14. Views must not set numeric
  icon dimensions inline. Icon-only button hit areas use the shared `icon`,
  `icon-compact`, `icon-micro`, and `icon-copy` variants; window controls and
  navigation buttons have their own shared dimensions. Icon-only buttons render
  `AppIcon` geometry rather than font-dependent Unicode glyphs.
  The collapsed context card floats above the chat. Its measured height offsets
  the scroll viewport only when the viewport is too narrow to place the message
  column beside the card; wide conversations begin below the header. Opening
  the inspector reserves a fixed right column so the
  composer and messages reflow
  instead of being covered. The composer model selector truncates within a shared width
  variant at the minimum supported window size.
  Wrappers that retain a Fluent `StyleKeyOverride` expose an `app-*` class;
  shared styles match that class instead of the wrapper type. A custom
  `ControlTheme` must target the retained native style key.
  `AppComboBox` also styles its Fluent-generated popup and option states through
  `Styles/ComboBoxVariants.axaml`, avoiding an unthemed native dropdown.
  View buttons must not set colors, typography, radii, dimensions, or padding
  inline or through AXAML property elements; use a shared `AppButton` size or
  purpose variant instead. The App build rejects native AXAML controls
  (including shapes), appearance overrides on styled controls, and page-local
  visual styles. Functional property elements such as button flyouts and item
  templates remain allowed.
  `MOSSUI002` rejects per-instance C# appearance assignments, direct styled-property
  writes, bindings and `ClearValue` resets, and page-local style/theme construction
  outside `Controls`. Functional resets and structural layout sizing remain allowed.
  Shared component implementations own their appearance; view code selects classes
  and sets functional properties, data bindings, and structural layout only.
  The shared `danger` button variant resolves theme-specific palette resources
  from `DangerButtonResources.axaml`. Existing disabled treatment, commands and
  click geometry remain shared; settings pages do not supply their own colors.
  `NativeVisualControlUsageTests` additionally guards direct C# construction in App code,
  including namespace aliases. `NativeVisualControlInheritanceTests` rejects native
  visual subclasses outside `Controls`; only structural layout primitives remain exempt.

The workspace shell separates its outer frame shadow from an inner rounded
content clip. Only the outer window page switcher allows shadow rendering above
the workspace row; nested page-switcher clipping is unchanged. Shared card
shadows remain on SharedSurfaceShadow, while the measured SharedWindowFrameShadow
variant is restricted by the build guard to the exact shared frame selector.
See `WINDOW_FRAME_SHADOW_ACCEPTANCE.md` for pixel/native evidence and the
reproduced, still-unrepaired native restore clipping defect.
`NATIVE_RESIZE_DIAGNOSTIC_PROGRESS.md` separates failed original-binary captures
from healthy telemetry and fixture-specific screenshot-anchor checks; no
diagnostic hooks or forced-layout workaround are part of the product.

`NATIVE_RESIZE_MESSAGE_PROGRESS.md` records repository-external raw native message
observations without changing handling, layout or product dependencies. Transient
Bounds lag inside resize callbacks is not treated as a settled failure; an actual
DPI-transition/failed-state event history is still required before a root-cause fix.

Settings uses the same navigation rail and rounded frame/clip as the workspace,
with shared sidebar/title/search variants. Return keeps the active conversation
and draft; automation/inspector navigation leaves settings before showing its
destination. Settings selection overrides hidden workspace-page selection and
owner visibility closes rail flyouts. `SETTINGS_SHELL_ALIGNMENT_PROGRESS.md`
separates deterministic rendered checks from still-missing native comparisons.
MainWindowViewModel also subscribes to the catalog's explicit ConversationRequested
event and unsubscribes on disposal: successful task/menu/history navigation leaves
settings, while rejected requests and background catalog refreshes do not. Settings
search filters navigation and empty group headings without selecting a content
page or clearing an unsaved editor. `SETTINGS_NAVIGATION_INTERACTION_ACCEPTANCE.md`
records keyboard, negative-path and state-preservation checks.
`NATIVE_SETTINGS_INTERACTION_PROGRESS.md` adds versioned native navigation,
unsent-draft and 96-DPI theme/size replays without model calls. The shared settings
sidebar title uses Bold at its existing 18px/28px geometry; bounded native crops
separate title ink from unchanged search/selection anchors. Failed-state geometry
and an external Headless event simulation narrow, but do not repair, the native
resize defect. No diagnostic hooks or forced-layout code enter the application.

Provider/model edit-session boundaries are recorded in
`PROVIDER_MODEL_EDITOR_SESSION_ACCEPTANCE.md`. Selected-item commands reuse the
catalog-validated row entry points. Opening a new session on the same selected
item reloads saved fields and clears discarded validation; an already-open draft
and ordinary settings-page navigation remain intact. No shared dialog, port,
repository, revision guard, dependency or execution-policy boundary changes.

Shared workspace panes distinguish explicit inspector expansion from effective
full-width presentation. An open inspector automatically takes the host when the
configured primary/inspector minimum widths and splitter cannot fit; widening
restores the retained split without replacing controls or clearing the primary
draft. Headers consume effective state, while preview cleanup consumes explicit
expansion intent to avoid repeated layout-derived toggles. Compact restore is
disabled; the existing close action remains usable. This is shared App layout,
not a tool, persistence or dependency change. That slice retained MainWindow's
minimum pending navigation/settings/composer adaptation.
`NARROW_WORKSPACE_LAYOUT_ACCEPTANCE.md` records deterministic compact coverage,
ordinary-size native replay and the remaining reference/DPI acceptance limits.

Caption palette and geometry live in shared WindowCaptionResources and
WindowCaptionVariants. The caption variant loads after generic ghost-button
styles so normal/hover foreground cannot silently fall back to muted navigation
icons. Disabled, close-hover, Arrow cursor, non-focusability and existing caption
commands retain their boundaries; no per-view appearance or new native port is
added. The icon button exposes its existing alternate geometry state as a
pseudo-class; the caption-only maximize variant does not affect other icons.
`WINDOW_CAPTION_CONTRAST_ACCEPTANCE.md` separates dedicated palette, render-scale
quantization checks, ordinary-maximize rasterization and native replay from
remaining same-state reference and native high-DPI limits.

AppNavigationPanes now owns the shared workspace/settings navigation layout.
Its 264-DIP sidebar and 360-DIP primary minimum determine a 624-DIP host
breakpoint. Compact mode uses a temporary drawer instead of changing the user's
wide-window sidebar preference. Both shells retain their real controls, drafts
and inspector expansion intent. A drawer blocks underlying pointer hit testing
and primary keyboard traversal without disabling business controls. Selection,
outside left click and unconsumed Escape dismiss it and restore eligible primary
focus. Root/ancestor subscriptions and deferred focus are invalidated on detach,
replacement and unavailable hosts. The same titlebar toggle targets the active
shell, including settings; the header tint follows actual navigation visibility.

The automation page is a sibling of the retained conversation navigation layout,
not its primary pane. It uses the full content frame beside the navigation rail
without changing the conversation's sidebar preference. Its titlebar navigation
toggle and sidebar tint are hidden; settings retain their own navigation rules.
`AUTOMATION_PAGE_LAYOUT_ACCEPTANCE.md` records theme/size/return-path tests and
the user-postponed native screenshot boundary.

MainWindow permits 526x601 after real-consumer compact checks. AppResponsiveColumns
stacks automation list/history when their configured columns cannot fit, preserving
their instances and independent scrolling. Settings dialogs share width/height
constraints through ModalAvailableExtentConverter, also used by approval cards;
their fixed footers remain outside the scrollable body. These changes stay in App
layout/styles and invoke no tools, provider requests or new persistence behavior.
`COMPACT_NAVIGATION_ACCEPTANCE.md` records native 96-DPI replay and the remaining
same-state reference, separate native clipping/high-DPI and product boundaries.

Project navigation retains its complete task snapshot while a separate App-owned
preview exposes five existing task rows by default. Expansion is per project and
session; search exposes all matches, and a newly selected hidden task is revealed
after filtering/loading permits it. The preview uses the existing collection
coordinator, does not clone rows or add subscriptions, and never truncates history
or repository data. Shared button typography and localized labels remain in their
existing owners. `PROJECT_TASK_PREVIEW_ACCEPTANCE.md` records native replay,
component measurements, regression status and remaining reference/DPI limits.

Project rows with summary flyouts retain the compact navigation drawer while
opening their contextual actions; summary-free selections retain normal dismissal.
Global approval presentation temporarily yields to either compact drawer without
changing request identity, queue, cancellation, browser blocking or execution policy.
UI-to-tool authorization and snapshot/recovery checks are recorded in
`PROJECT_AUTHORIZATION_UI_ACCEPTANCE.md`; native picker and same-state reference
validation remain separate.

When a feature does not fit these boundaries, update the architecture first;
do not work around it with cross-project references or global state.

Settings form rows share AppSettingsRow's adaptive two-column/stacked layout,
retaining controls and drafts across resizing. Catalog empty states and compact
page headings use existing shared components; scrolling and feedback wrapping
remain shared style decisions. `ALL_PAGES_UI_POLISH_ACCEPTANCE.md` records the
cross-page theme/viewport review and its native/reference acceptance limits.
